// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Difficulty
{
    public abstract class DifficultyCalculator
    {
        /// <summary>
        /// The beatmap for which difficulty will be calculated.
        /// </summary>
        protected IBeatmap Beatmap { get; private set; } = null!;

        private Mod[] playableMods = null!;
        private double clockRate;

        private readonly IRulesetInfo? ruleset;

        /// <summary>
        /// A yymmdd version which is used to discern when reprocessing is required.
        /// </summary>
        public virtual int Version => 0;

        protected DifficultyCalculator(IRulesetInfo? ruleset)
        {
            this.ruleset = ruleset;
        }

        /// <summary>
        /// Asynchronously calculates the difficulty of the beatmap and score.
        /// </summary>
        /// <param name="mods">The mods that should be applied to the beatmap.</param>
        /// <param name="workingBeatmap">The beatmap to calculate the DiffFormance attributes of.</param>
        /// <param name="score">The optional score information to calculate the Performance Attributes of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A structure describing the difficulty of the beatmap.</returns>
        public Task<(DifficultyAttributes DiffAttribs, PerformanceAttributes? PerfAttribs)> CalculateAsync(IEnumerable<Mod> mods, IWorkingBeatmap workingBeatmap, ScoreInfo? score, CancellationToken cancellationToken)
            => Task.Run(() => Calculate(mods, workingBeatmap, score, cancellationToken), cancellationToken);

        /// <summary>
        /// Calculates the difficulty attributes of the beatmap with no mods applied.
        /// </summary>
        /// <param name="workingBeatmap">The beatmap to calculate the DiffFormance attributes of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A structure describing the difficulty of the beatmap.</returns>
        public DifficultyAttributes CalculateDifficultyAttributes(IWorkingBeatmap workingBeatmap, CancellationToken cancellationToken = default)
            => Calculate(Array.Empty<Mod>(), workingBeatmap, null, cancellationToken).DiffAttribs;

        /// <summary>
        /// Calculates the difficulty of the beatmap using a specific mod combination.
        /// </summary>
        /// <param name="mods">The mods that should be applied to the beatmap.</param>
        /// <param name="workingBeatmap">The beatmap to calculate the DiffFormance attributes of.</param>
        /// <param name="score">The optional score information to calculate the Performance Attributes of.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A structure describing the difficulty of the beatmap.</returns>
        public (DifficultyAttributes DiffAttribs, PerformanceAttributes? PerfAttribs) Calculate(IEnumerable<Mod> mods, IWorkingBeatmap workingBeatmap, ScoreInfo? score, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            preProcess(mods, workingBeatmap, cancellationToken);

            var skills = CreateSkills(Beatmap, playableMods, clockRate);

            if (!Beatmap.HitObjects.Any())
                return CreateAttributes(Beatmap, playableMods, score, skills, clockRate);

            foreach (var hitObject in getDifficultyHitObjects())
            {
                foreach (var skill in skills)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    skill.Process(hitObject);
                }
            }

            return CreateAttributes(Beatmap, playableMods, score, skills, clockRate);
        }

        /// <summary>
        /// Calculates the difficulty of the beatmap using all mod combinations applicable to the beatmap.
        /// </summary>
        /// <remarks>
        /// This can only be used to compute difficulties for legacy mod combinations.
        /// </remarks>
        /// <returns>A collection of structures describing the difficulty of the beatmap for each mod combination.</returns>
        public IEnumerable<DifficultyAttributes> CalculateAllLegacyCombinations(IWorkingBeatmap workingBeatmap, CancellationToken cancellationToken = default)
        {
            var rulesetInstance = ruleset?.CreateInstance() ?? null;

            foreach (var combination in CreateDifficultyAdjustmentModCombinations())
            {
                Mod? classicMod = rulesetInstance?.CreateMod<ModClassic>();

                var finalCombination = ModUtils.FlattenMod(combination);
                if (classicMod != null)
                    finalCombination = finalCombination.Append(classicMod);

                yield return Calculate(finalCombination.ToArray(), workingBeatmap, null, cancellationToken).DiffAttribs;
            }
        }

        /// <summary>
        /// Retrieves the <see cref="DifficultyHitObject"/>s to calculate against.
        /// </summary>
        private IEnumerable<DifficultyHitObject> getDifficultyHitObjects() => SortObjects(CreateDifficultyHitObjects(Beatmap, clockRate));

        /// <summary>
        /// Performs required tasks before every calculation.
        /// </summary>
        /// <param name="mods">The original list of <see cref="Mod"/>s.</param>
        /// <param name="workingBeatmap">The beatmap to be processed for DiffFormance</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private void preProcess(IEnumerable<Mod> mods, IWorkingBeatmap workingBeatmap, CancellationToken cancellationToken = default)
        {
            playableMods = mods.Select(m => m.DeepClone()).ToArray();

            // Only pass through the cancellation token if it's non-default.
            // This allows for the default timeout to be applied for playable beatmap construction.
            Beatmap = cancellationToken == default
                ? workingBeatmap.GetPlayableBeatmap(ruleset, playableMods)
                : workingBeatmap.GetPlayableBeatmap(ruleset, playableMods, cancellationToken);

            var track = new TrackVirtual(10000);
            playableMods.OfType<IApplicableToTrack>().ForEach(m => m.ApplyToTrack(track));
            clockRate = track.Rate;
        }

        /// <summary>
        /// Sorts a given set of <see cref="DifficultyHitObject"/>s.
        /// </summary>
        /// <param name="input">The <see cref="DifficultyHitObject"/>s to sort.</param>
        /// <returns>The sorted <see cref="DifficultyHitObject"/>s.</returns>
        protected virtual IEnumerable<DifficultyHitObject> SortObjects(IEnumerable<DifficultyHitObject> input)
            => input.OrderBy(h => h.BaseObject.StartTime);

        /// <summary>
        /// Creates all <see cref="Mod"/> combinations which adjust the <see cref="Beatmaps.Beatmap"/> difficulty.
        /// </summary>
        public Mod[] CreateDifficultyAdjustmentModCombinations()
        {
            return createDifficultyAdjustmentModCombinations(DifficultyAdjustmentMods, Array.Empty<Mod>()).ToArray();

            static IEnumerable<Mod> createDifficultyAdjustmentModCombinations(ReadOnlyMemory<Mod> remainingMods, IEnumerable<Mod> currentSet, int currentSetCount = 0)
            {
                // Return the current set.
                switch (currentSetCount)
                {
                    case 0:
                        // Initial-case: Empty current set
                        yield return new ModNoMod();

                        break;

                    case 1:
                        yield return currentSet.Single();

                        break;

                    default:
                        yield return new MultiMod(currentSet.ToArray());

                        break;
                }

                // Apply the rest of the remaining mods recursively.
                for (int i = 0; i < remainingMods.Length; i++)
                {
                    (var nextSet, int nextCount) = flatten(remainingMods.Span[i]);

                    // Check if any mods in the next set are incompatible with any of the current set.
                    if (currentSet.SelectMany(m => m.IncompatibleMods).Any(c => nextSet.Any(c.IsInstanceOfType)))
                        continue;

                    // Check if any mods in the next set are the same type as the current set. Mods of the exact same type are not incompatible with themselves.
                    if (currentSet.Any(c => nextSet.Any(n => c.GetType() == n.GetType())))
                        continue;

                    // If all's good, attach the next set to the current set and recurse further.
                    foreach (var combo in createDifficultyAdjustmentModCombinations(remainingMods.Slice(i + 1), currentSet.Concat(nextSet), currentSetCount + nextCount))
                        yield return combo;
                }
            }

            // Flattens a mod hierarchy (through MultiMod) as an IEnumerable<Mod>
            static (IEnumerable<Mod> set, int count) flatten(Mod mod)
            {
                if (!(mod is MultiMod multi))
                    return (mod.Yield(), 1);

                IEnumerable<Mod> set = Enumerable.Empty<Mod>();
                int count = 0;

                foreach (var nested in multi.Mods)
                {
                    (var nestedSet, int nestedCount) = flatten(nested);
                    set = set.Concat(nestedSet);
                    count += nestedCount;
                }

                return (set, count);
            }
        }

        /// <summary>
        /// Retrieves all <see cref="Mod"/>s which adjust the <see cref="Beatmaps.Beatmap"/> difficulty.
        /// </summary>
        protected virtual Mod[] DifficultyAdjustmentMods => Array.Empty<Mod>();

        /// <summary>
        /// Creates <see cref="DifficultyAttributes"/> to describe beatmap's calculated difficulty.
        /// </summary>
        /// <param name="beatmap">The <see cref="IBeatmap"/> whose difficulty was calculated.
        /// This may differ from <see cref="Beatmap"/> in the case of timed calculation.</param>
        /// <param name="scoreInfo">The optional ScoreInfo to calculate PerformanceAttributes with.</param>
        /// <param name="mods">The <see cref="Mod"/>s that difficulty was calculated with.</param>
        /// <param name="skills">The skills which processed the beatmap.</param>
        /// <param name="clockRate">The rate at which the gameplay clock is run at.</param>
        protected abstract (DifficultyAttributes, PerformanceAttributes?) CreateAttributes(IBeatmap beatmap, Mod[] mods, ScoreInfo? scoreInfo, Skill[] skills, double clockRate);

        /// <summary>
        /// Enumerates <see cref="DifficultyHitObject"/>s to be processed from <see cref="HitObject"/>s in the <see cref="IBeatmap"/>.
        /// </summary>
        /// <param name="beatmap">The <see cref="IBeatmap"/> providing the <see cref="HitObject"/>s to enumerate.</param>
        /// <param name="clockRate">The rate at which the gameplay clock is run at.</param>
        /// <returns>The enumerated <see cref="DifficultyHitObject"/>s.</returns>
        protected abstract IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate);

        /// <summary>
        /// Creates the <see cref="Skill"/>s to calculate the difficulty of an <see cref="IBeatmap"/>.
        /// </summary>
        /// <param name="beatmap">The <see cref="IBeatmap"/> whose difficulty will be calculated.
        /// This may differ from <see cref="Beatmap"/> in the case of timed calculation.</param>
        /// <param name="mods">Mods to calculate difficulty with.</param>
        /// <param name="clockRate">Clockrate to calculate difficulty with.</param>
        /// <returns>The <see cref="Skill"/>s.</returns>
        protected abstract Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate);
    }
}
