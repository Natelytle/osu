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
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.MathUtils;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaDifficultyCalculator : DifficultyCalculator
    {
        private const double tap_combine_exponent = 2.77504;

        private const double speed_skill_weight = 1.02237;
        private const double jack_skill_weight = 1.42793;
        private const double coordination_skill_weight = 2.49980;
        private const double technical_skill_weight = 2.49916;
        private const double release_skill_weight = 2.83449;

        private const double long_note_ratio_damping = 0.06263;

        private const double od_weight = 0.188;

        private static readonly double baselineHitLeniency = calculateHitLeniency(8.0);

        private readonly bool isForCurrentRuleset;

        private IReadOnlyList<DifficultyHitObject> difficultyHitObjects = null!;

        private DifficultyAggregator combinedAggregator = null!;
        private DifficultyAggregator speedAggregator = null!;
        private DifficultyAggregator technicalAggregator = null!;
        private DifficultyAggregator jackAggregator = null!;
        private DifficultyAggregator coordinationAggregator = null!;
        private DifficultyAggregator releaseAggregator = null!;

        private int processedStrainCount;

        public override int Version => 20241007;

        public ManiaDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
            isForCurrentRuleset = beatmap.BeatmapInfo.Ruleset.MatchesOnlineID(ruleset);
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        {
            combinedAggregator = new DifficultyAggregator();
            speedAggregator = new DifficultyAggregator();
            technicalAggregator = new DifficultyAggregator();
            jackAggregator = new DifficultyAggregator();
            coordinationAggregator = new DifficultyAggregator();
            releaseAggregator = new DifficultyAggregator();

            processedStrainCount = 0;

            return new Skill[]
            {
                new Speed(mods),
                new Technical(mods),
                new Jack(mods),
                new Coordination(mods),
                new Release(mods),
            };
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            if (beatmap.HitObjects.Count == 0)
                return new ManiaDifficultyAttributes { Mods = mods };

            var speed = skills.OfType<Speed>().Single();
            var technical = skills.OfType<Technical>().Single();
            var jack = skills.OfType<Jack>().Single();
            var coordination = skills.OfType<Coordination>().Single();
            var release = skills.OfType<Release>().Single();

            double odMultiplier = calculateOdMultiplier(Beatmap.BeatmapInfo.Difficulty.OverallDifficulty);

            var speedStrains = speed.GetObjectDifficulties();
            var technicalStrains = technical.GetObjectDifficulties();
            var jackStrains = jack.GetObjectDifficulties();
            var coordinationStrains = coordination.GetObjectDifficulties();
            var releaseStrains = release.GetObjectDifficulties();

            for (int i = processedStrainCount; i < speedStrains.Count; i++)
            {
                var hitObject = (ManiaDifficultyHitObject)difficultyHitObjects[i];

                double tapStrain = combineTapStrains(speedStrains[i], jackStrains[i], coordinationStrains[i], technicalStrains[i]);
                double combinedStrain = tapStrain + release_skill_weight * releaseStrains[i];

                double longNoteDamper = calculateLongNoteDamper(hitObject.LongNoteRatio);

                combinedAggregator.Add(combinedStrain, hitObject.CumulativeNoteWeight, odMultiplier, longNoteDamper);
                speedAggregator.Add(speedStrains[i], hitObject.CumulativeNoteWeight, odMultiplier, 1.0);
                technicalAggregator.Add(technicalStrains[i], hitObject.CumulativeNoteWeight, odMultiplier, 1.0);
                jackAggregator.Add(jackStrains[i], hitObject.CumulativeNoteWeight, odMultiplier, 1.0);
                coordinationAggregator.Add(coordinationStrains[i], hitObject.CumulativeNoteWeight, odMultiplier, 1.0);
                releaseAggregator.Add(releaseStrains[i], hitObject.CumulativeNoteWeight, odMultiplier, 1.0);
            }

            processedStrainCount = speedStrains.Count;

            return new ManiaDifficultyAttributes
            {
                StarRating = combinedAggregator.CurrentRating,
                Mods = mods,
                MaxCombo = beatmap.HitObjects.Sum(maxComboFor),
                SpeedDifficulty = speedAggregator.CurrentRating,
                TechnicalDifficulty = technicalAggregator.CurrentRating,
                JackDifficulty = jackAggregator.CurrentRating,
                CoordinationDifficulty = coordinationAggregator.CurrentRating,
                ReleaseDifficulty = releaseAggregator.CurrentRating,
            };
        }

        private static double combineTapStrains(double speedStrain, double jackStrain, double coordinationStrain, double technicalStrain)
        {
            double powerSum = speed_skill_weight * Math.Pow(speedStrain, tap_combine_exponent) + jack_skill_weight * Math.Pow(jackStrain, tap_combine_exponent)
                                                                                               + coordination_skill_weight * Math.Pow(coordinationStrain, tap_combine_exponent)
                                                                                               + technical_skill_weight * Math.Pow(technicalStrain, tap_combine_exponent);

            return powerSum > 0 ? Math.Pow(powerSum, 1.0 / tap_combine_exponent) : 0.0;
        }

        private static double calculateLongNoteDamper(double longNoteRatio)
            => 1.0 - long_note_ratio_damping * longNoteRatio * longNoteRatio;

        private static double calculateHitLeniency(double overallDifficulty)
        {
            double hitWindow300Ms = 34.0 + 3.0 * Math.Min(10.0, Math.Max(0.0, 10.0 - overallDifficulty));
            double q = hitWindow300Ms / 1000.0;
            double baseValue = 0.3 * Math.Sqrt(q);
            double alt = 0.6 * (baseValue - 0.09) + 0.09;
            return Math.Max(1e-9, Math.Min(baseValue, alt));
        }

        private static double calculateOdMultiplier(double overallDifficulty)
        {
            double raw = baselineHitLeniency / calculateHitLeniency(overallDifficulty);
            return 1.0 + od_weight * (raw - 1.0);
        }

        private static int maxComboFor(HitObject hitObject)
        {
            if (hitObject is HoldNote hold)
                return 1 + (int)((hold.EndTime - hold.StartTime) / 100);

            return 1;
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        {
            var sortedObjects = beatmap.HitObjects.ToArray();
            int totalColumns = ((ManiaBeatmap)beatmap).TotalColumns;

            double clockRate = ModUtils.CalculateRateWithMods(mods);

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

            difficultyHitObjects = objects;
            return objects;
        }

        protected override IEnumerable<DifficultyHitObject> SortObjects(IEnumerable<DifficultyHitObject> input) => input;

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
    }
}
