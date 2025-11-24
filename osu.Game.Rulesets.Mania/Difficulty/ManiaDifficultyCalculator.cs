// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.MathUtils;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaDifficultyCalculator : DifficultyCalculator
    {
        private const double difficulty_multiplier = 0.125;
        private const double chordjack_multiplier = 1.0;
        private const double chordstream_multiplier = 1.0;
        private const double speedjack_multiplier = 1.0;
        private const double speedstream_multiplier = 1.0;

        private const double peak_norm = 4.0;

        private readonly bool isForCurrentRuleset;

        public override int Version => 20251121;

        public ManiaDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
            isForCurrentRuleset = beatmap.BeatmapInfo.Ruleset.MatchesOnlineID(ruleset);
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            if (beatmap.HitObjects.Count == 0)
                return new ManiaDifficultyAttributes { Mods = mods };

            HitWindows hitWindows = new ManiaHitWindows();
            hitWindows.SetDifficulty(beatmap.Difficulty.OverallDifficulty);

            var chordJack = skills.OfType<ChordJack>().Single();
            var chordStream = skills.OfType<ChordStream>().Single();
            var speedJack = skills.OfType<SpeedJack>().Single();
            var speedStream = skills.OfType<SpeedStream>().Single();

            // double chordJackSkill = chordJack.DifficultyValue() * chordjack_multiplier;
            // double chordStreamSkill = chordStream.DifficultyValue() * chordstream_multiplier;
            // double speedJackSkill = speedJack.DifficultyValue() * speedjack_multiplier;
            // double speedStreamSkill = speedStream.DifficultyValue() * speedstream_multiplier;

            double combinedRating = combinedDifficultyValue(chordJack, chordStream, speedJack, speedStream) * difficulty_multiplier;

            ManiaDifficultyAttributes attributes = new ManiaDifficultyAttributes
            {
                StarRating = combinedRating,
                Mods = mods,
                MaxCombo = beatmap.HitObjects.Sum(maxComboForObject),
            };

            return attributes;
        }

        private static int maxComboForObject(HitObject hitObject)
        {
            if (hitObject is HoldNote hold)
                return 1 + (int)((hold.EndTime - hold.StartTime) / 100);

            return 1;
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            var sortedObjects = beatmap.HitObjects.ToArray();
            int totalColumns = ((ManiaBeatmap)beatmap).TotalColumns;

            LegacySortHelper<HitObject>.Sort(sortedObjects, Comparer<HitObject>.Create((a, b) => (int)Math.Round(a.StartTime) - (int)Math.Round(b.StartTime)));

            List<DifficultyHitObject> objects = new List<DifficultyHitObject>();
            List<DifficultyHitObject>[] perColumnObjects = new List<DifficultyHitObject>[totalColumns];

            for (int column = 0; column < totalColumns; column++)
                perColumnObjects[column] = new List<DifficultyHitObject>();

            for (int i = 1; i < sortedObjects.Length; i++)
            {
                var currentObject = new ManiaDifficultyHitObject(sortedObjects[i], sortedObjects[i - 1], clockRate, objects, perColumnObjects, objects.Count);
                objects.Add(currentObject);
                perColumnObjects[currentObject.Column].Add(currentObject);
            }

            return objects;
        }

        // Sorting is done in CreateDifficultyHitObjects, since the full list of hitobjects is required.
        protected override IEnumerable<DifficultyHitObject> SortObjects(IEnumerable<DifficultyHitObject> input) => input;

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate)
        {
            return new Skill[]
            {
                new ChordJack(mods),
                new ChordStream(mods),
                new SpeedJack(mods),
                new SpeedStream(mods)
            };
        }

        protected override Mod[] DifficultyAdjustmentMods
        {
            get
            {
                var mods = new Mod[]
                {
                    new ManiaModDoubleTime(),
                    new ManiaModHalfTime(),
                    new ManiaModEasy(),
                    new ManiaModHardRock(),
                };

                if (isForCurrentRuleset)
                    return mods;

                // if we are a convert, we can be played in any key mod.
                return mods.Concat(new Mod[]
                {
                    new ManiaModKey1(),
                    new ManiaModKey2(),
                    new ManiaModKey3(),
                    new ManiaModKey4(),
                    new ManiaModKey5(),
                    new MultiMod(new ManiaModKey5(), new ManiaModDualStages()),
                    new ManiaModKey6(),
                    new MultiMod(new ManiaModKey6(), new ManiaModDualStages()),
                    new ManiaModKey7(),
                    new MultiMod(new ManiaModKey7(), new ManiaModDualStages()),
                    new ManiaModKey8(),
                    new MultiMod(new ManiaModKey8(), new ManiaModDualStages()),
                    new ManiaModKey9(),
                    new MultiMod(new ManiaModKey9(), new ManiaModDualStages()),
                }).ToArray();
            }
        }

        /// <summary>
        /// Returns the combined star rating of the beatmap, calculated using peak strains from all sections of the map.
        /// </summary>
        /// <remarks>
        /// For each section, the peak strains of all separate skills are combined into a single peak strain for the section.
        /// The resulting partial rating of the beatmap is a weighted sum of the combined peaks (higher peaks are weighted more).
        /// </remarks>
        private double combinedDifficultyValue(ChordJack chordJack, ChordStream chordStream, SpeedJack speedJack, SpeedStream speedStream)
        {
            List<double> peaks = combinePeaks(
                chordJack.GetCurrentStrainPeaks().ToList(),
                chordStream.GetCurrentStrainPeaks().ToList(),
                speedJack.GetCurrentStrainPeaks().ToList(),
                speedStream.GetCurrentStrainPeaks().ToList()
            );

            if (peaks.Count == 0)
                return 0;

            double difficulty = 0;
            double weight = 1;

            foreach (double strain in peaks.OrderDescending())
            {
                difficulty += strain * weight;
                weight *= 0.9;
            }

            return difficulty;
        }

        /// <summary>
        /// Combines lists of peak strains from multiple skills into a list of single peak strains for each section.
        /// </summary>
        private List<double> combinePeaks(List<double> chordJackPeaks, List<double> chordStreamPeaks, List<double> speedJackPeaks, List<double> speedStreamPeaks)
        {
            var combinedPeaks = new List<double>();

            for (int i = 0; i < chordJackPeaks.Count; i++)
            {
                double chordJackPeak = chordJackPeaks[i] * chordjack_multiplier;
                double chordStreamPeak = chordStreamPeaks[i] * chordstream_multiplier;
                double speedJackPeak = speedJackPeaks[i] * speedjack_multiplier;
                double speedStreamPeak = speedStreamPeaks[i] * speedstream_multiplier;

                double peak = DifficultyCalculationUtils.Norm(peak_norm, chordJackPeak, chordStreamPeak, speedJackPeak, speedStreamPeak);

                // Sections with 0 strain are excluded to avoid worst-case time complexity of the following sort (e.g. /b/2351871).
                // These sections will not contribute to the difficulty.
                if (peak > 0)
                    combinedPeaks.Add(peak);
            }

            return combinedPeaks;
        }
    }
}
