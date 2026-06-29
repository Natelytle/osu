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
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaDifficultyCalculator : DifficultyCalculator
    {
        private const double high_percentile_weight = 0.25;
        private const double high_percentile_scale = 0.88;

        private const double mid_percentile_weight = 0.20;
        private const double mid_percentile_scale = 0.94;

        private const double overall_multiplier = 0.360643;
        private const double power_exponent = 0.52899;

        /// SR *= (1 - full_ln_damper * lnRatio^2).
        private const double full_ln_damper = 0.06263;

        private const double ln_hybrid_damper = 0.028;
        private const double ln_hybrid_ramp_lo = 0.15;
        private const double ln_hybrid_ramp_hi = 0.35;
        private const double ln_hybrid_fade_lo = 0.50;
        private const double ln_hybrid_fade_hi = 0.75;

        private const double short_map_nerf = 0.195;
        private const double short_map_cap_seconds = 66.0;
        private const double short_map_ln_lo = 0.55;
        private const double short_map_ln_hi = 0.72;

        private const double high_end_compression_knee = 11.5;
        private const double high_end_compression_strength = 0.5;
        private const double high_end_coordination_gate_lo = 5.9;
        private const double high_end_coordination_gate_hi = 6.6;

        private const double od_weight = 0.188;
        private static readonly double leniency_at_od8 = hitLeniency(8.0);

        private readonly bool isForCurrentRuleset;

        public override int Version => 20241007;

        public ManiaDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
            isForCurrentRuleset = beatmap.BeatmapInfo.Ruleset.MatchesOnlineID(ruleset);
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            if (beatmap.HitObjects.Count == 0)
                return new ManiaDifficultyAttributes { Mods = mods };

            var totalSkill = (Total)skills[0];
            var coordinationSkill = (Coordination)skills[1];

            double odMult = odMultiplier(Beatmap.BeatmapInfo.Difficulty.OverallDifficulty);

            int totalNotes = beatmap.HitObjects.Count(h => h is ManiaHitObject);
            int longNotes = beatmap.HitObjects.Count(h => h is HoldNote);

            double lnRatio = totalNotes > 0 ? (double)longNotes / totalNotes : 0.0;
            double hybridLn = DifficultyCalculationUtils.Smoothstep(lnRatio, ln_hybrid_ramp_lo, ln_hybrid_ramp_hi) * (1.0 - DifficultyCalculationUtils.Smoothstep(lnRatio, ln_hybrid_fade_lo, ln_hybrid_fade_hi));
            double lnDamper = (1.0 - full_ln_damper * lnRatio * lnRatio) * (1.0 - ln_hybrid_damper * hybridLn);

            double shortMapMult = shortMapNerf(mapLengthSeconds(beatmap.HitObjects, mods), lnRatio);

            double totalDifficulty = totalSkill.DifficultyValue();
            double coordinationDifficulty = scaleToStarRating(coordinationSkill.DifficultyValue()); // TODO: Rebalance this without scaleToStarRating

            double starRating = computeStarRating(totalDifficulty, odMult, lnDamper, shortMapMult, coordinationDifficulty);

            return new ManiaDifficultyAttributes
            {
                StarRating = starRating,
                Mods = mods,
                MaxCombo = beatmap.HitObjects.Sum(maxComboForObject),
                SpeedDifficulty = 0,
                TechnicalDifficulty = 0,
                JackDifficulty = 0,
                CoordinationDifficulty = coordinationDifficulty,
                ReleaseDifficulty = 0,
                Variety = 3.2, // TODO: Make a per-note calculation in Total.cs
                LnRatio = lnRatio
            };
        }

        private static double shortMapNerf(double lengthSeconds, double lnRatio)
        {
            double shortness = 1.0 - Math.Clamp(lengthSeconds / short_map_cap_seconds, 0.0, 1.0);
            double lnGate = DifficultyCalculationUtils.Smoothstep(lnRatio, short_map_ln_lo, short_map_ln_hi);

            return 1.0 - short_map_nerf * shortness * lnGate;
        }

        private static double computeStarRating(double totalDifficulty, double overallDifficultyMultiplier, double longNoteDamper, double shortMapMultiplier, double coordinationDifficulty)
        {
            double starRating = scaleToStarRating(totalDifficulty)
                                * overallDifficultyMultiplier
                                * longNoteDamper
                                * shortMapMultiplier;

            if (starRating > high_end_compression_knee)
            {
                double coordinationGate = DifficultyCalculationUtils.Smoothstep(coordinationDifficulty, high_end_coordination_gate_lo, high_end_coordination_gate_hi);
                double excessAboveKnee = starRating - high_end_compression_knee;
                starRating = high_end_compression_knee + excessAboveKnee * (1.0 - high_end_compression_strength * coordinationGate);
            }

            return starRating;
        }

        private static double scaleToStarRating(double aggregatedDifficulty)
        {
            if (aggregatedDifficulty <= 0)
                return 0.0;

            return overall_multiplier * Math.Pow(aggregatedDifficulty, power_exponent);
        }

        private static double hitLeniency(double overallDifficulty)
        {
            double hitWindow300Ms = 34.0 + 3.0 * Math.Min(10.0, Math.Max(0.0, 10.0 - overallDifficulty));
            double q = hitWindow300Ms / 1000.0;
            double baseValue = 0.3 * Math.Sqrt(q);
            double alt = 0.6 * (baseValue - 0.09) + 0.09;
            return Math.Max(1e-9, Math.Min(baseValue, alt));
        }

        private double odMultiplier(double overallDifficulty)
        {
            double raw = leniency_at_od8 / hitLeniency(overallDifficulty);
            return 1.0 + od_weight * (raw - 1.0);
        }

        private static double mapLengthSeconds(IReadOnlyList<HitObject> hitObjects, Mod[] mods)
        {
            double clockRate = ModUtils.CalculateRateWithMods(mods);

            return ((hitObjects.LastOrDefault()?.GetEndTime() ?? 0) - (hitObjects.FirstOrDefault()?.StartTime ?? 0)) / 1000.0 / clockRate;
        }

        private int maxComboForObject(HitObject hitObject)
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

            List<DifficultyHitObject> objects = new List<DifficultyHitObject>(beatmap.HitObjects.Count);
            List<DifficultyHitObject>[] perColumnObjects = new List<DifficultyHitObject>[totalColumns];

            for (int column = 0; column < totalColumns; column++)
                perColumnObjects[column] = new List<DifficultyHitObject>();

            for (int i = 1; i < sortedObjects.Length; i++)
            {
                var currentObject = new ManiaDifficultyHitObject(sortedObjects[i], sortedObjects[i - 1], clockRate, objects, perColumnObjects, objects.Count);
                objects.Add(currentObject);
                perColumnObjects[currentObject.Column].Add(currentObject);
            }

            ManiaManipulationDifficultyPreprocessor.ProcessAndAssign(objects.Cast<ManiaDifficultyHitObject>().ToList());

            /*meanManipulation = objects.Count > 0
                ? objects.Cast<ManiaDifficultyHitObject>().Average(o => o.ManipulationFactor)
                : 1.0;*/

            return objects;
        }

        //private double meanManipulation = 1.0;

        protected override IEnumerable<DifficultyHitObject> SortObjects(IEnumerable<DifficultyHitObject> input) => input;

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        {
            return new Skill[]
            {
                new Total(mods),
                new Coordination(mods)
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
