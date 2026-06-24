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
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
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
        private const double combine_lambda = 2.0;

        private const double speed_weight = 1.02237;
        private const double jack_weight = 1.42793;
        private const double coordination_weight = 2.49980;
        private const double technical_weight = 2.49916;
        private const double release_weight = 2.83449;

        private readonly double[] highPercentiles = { 0.945, 0.935, 0.925, 0.915 };
        private readonly double[] midPercentiles = { 0.845, 0.835, 0.825, 0.815 };

        private const double high_percentile_weight = 0.25;
        private const double high_percentile_scale = 0.88;

        private const double mid_percentile_weight = 0.20;
        private const double mid_percentile_scale = 0.94;

        private const double power_mean_weight = 0.55;
        private const double power_mean_exponent = 5.0;

        private const double note_count_offset = 34.64147;
        private const double final_scaling = 0.90741;

        private const double overall_multiplier = 0.360643;
        private const double power_exponent = 0.52899;

        /// SR *= (1 - full_ln_damper * lnRatio^2).
        private const double full_ln_damper = 0.06263;

        private const double ln_hybrid_damper = 0.028;
        private const double ln_hybrid_ramp_lo = 0.15;
        private const double ln_hybrid_ramp_hi = 0.35;
        private const double ln_hybrid_fade_lo = 0.50;
        private const double ln_hybrid_fade_hi = 0.75;

        // Short, dense, high-LN maps dominated by Coordination (staggered "light-LN" release
        // maps, e.g. pupa / Take a Hint) are over-rated. Dampen the star rating, gated on all
        // three traits at once so genuine LN maps (long, or release/jack-dominant, or low
        // coordination like Circulation) are left untouched.
        private const double short_ln_coord_nerf = 0.17;
        private const double slc_ln_lo = 0.45;
        private const double slc_ln_hi = 0.65;
        private const double slc_weight_lo = 750.0;
        private const double slc_weight_hi = 2200.0;
        private const double slc_coord_dom_lo = 0.230;
        private const double slc_coord_dom_hi = 0.247;

        private const double od_weight = 0.188;
        private static readonly double leniency_at_od8 = hitLeniency(8.0);

        private const double long_note_weight_per_200_ms = 0.6;
        private const double max_long_note_weight_duration_ms = 1000.0;

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

            var speed = (Speed)skills[0];
            var technical = (Technical)skills[1];
            var jack = (Jack)skills[2];
            var coordination = (Coordination)skills[3];
            var release = (Release)skills[4];

            double noteWeight = totalNoteWeight(beatmap);
            double odMult = odMultiplier(Beatmap.BeatmapInfo.Difficulty.OverallDifficulty);
            double lnRatio = computeLnRatio(beatmap);
            double hybridLn = DifficultyCalculationUtils.Smoothstep(lnRatio, ln_hybrid_ramp_lo, ln_hybrid_ramp_hi) * (1.0 - DifficultyCalculationUtils.Smoothstep(lnRatio, ln_hybrid_fade_lo, ln_hybrid_fade_hi));
            double lnDamper = (1.0 - full_ln_damper * lnRatio * lnRatio) * (1.0 - ln_hybrid_damper * hybridLn);

            double speedDifficulty = skillStarRating(speed, noteWeight) * odMult;
            double technicalDifficulty = skillStarRating(technical, noteWeight) * odMult;
            double jackDifficulty = skillStarRating(jack, noteWeight) * odMult;
            double coordinationDifficulty = skillStarRating(coordination, noteWeight) * odMult;
            double releaseDifficulty = skillStarRating(release, noteWeight) * odMult;

            double shortLnCoordMult = shortLnCoordNerf(speedDifficulty, technicalDifficulty, jackDifficulty, coordinationDifficulty, releaseDifficulty, lnRatio, noteWeight);

            double starRating = scaleToStarRating(aggregateDifficulty(combineObjectStrains(speed, technical, jack, coordination, release), noteWeight)) * odMult * lnDamper * shortLnCoordMult;

            Console.WriteLine($"Variety: {participationRatio(speedDifficulty, technicalDifficulty, jackDifficulty, coordinationDifficulty, releaseDifficulty)}");

            return new ManiaDifficultyAttributes
            {
                StarRating = starRating,
                Mods = mods,
                MaxCombo = beatmap.HitObjects.Sum(maxComboForObject),
                SpeedDifficulty = speedDifficulty,
                TechnicalDifficulty = technicalDifficulty,
                JackDifficulty = jackDifficulty,
                CoordinationDifficulty = coordinationDifficulty,
                ReleaseDifficulty = releaseDifficulty,
                Variety = participationRatio(speedDifficulty, technicalDifficulty, jackDifficulty, coordinationDifficulty, releaseDifficulty),
            };
        }

        private static double participationRatio(params double[] difficulties)
        {
            double sum = 0;
            double sumSquares = 0;

            foreach (double d in difficulties)
            {
                sum += d;
                sumSquares += d * d;
            }

            return sumSquares > 0 ? sum * sum / sumSquares : 1.0;
        }

        private IEnumerable<double> combineObjectStrains(Speed speed, Technical technical, Jack jack, Coordination coordination, Release release)
        {
            var speedStrains = speed.GetObjectDifficulties();
            var technicalStrains = technical.GetObjectDifficulties();
            var jackStrains = jack.GetObjectDifficulties();
            var coordinationStrains = coordination.GetObjectDifficulties();
            var releaseStrains = release.GetObjectDifficulties();

            for (int i = 0; i < speedStrains.Count; i++)
            {
                double powerSum = speed_weight * Math.Pow(speedStrains[i], combine_lambda)
                                  + jack_weight * Math.Pow(jackStrains[i], combine_lambda)
                                  + coordination_weight * Math.Pow(coordinationStrains[i], combine_lambda)
                                  + technical_weight * Math.Pow(technicalStrains[i], combine_lambda);

                double tapDifficulty = powerSum > 0 ? Math.Pow(powerSum, 1.0 / combine_lambda) : 0.0;
                yield return tapDifficulty + release_weight * releaseStrains[i];
            }
        }

        private double aggregateDifficulty(IEnumerable<double> strains, double noteWeight)
        {
            double[] sortedStrains = strains.Where(strain => strain > 0).OrderBy(strain => strain).ToArray();

            if (sortedStrains.Length == 0)
                return 0.0;

            double highMean = calculatePercentileMean(sortedStrains, highPercentiles);
            double midMean = calculatePercentileMean(sortedStrains, midPercentiles);
            double powerMean = calculatePowerMean(sortedStrains, power_mean_exponent);

            double rawDifficulty = high_percentile_weight * (high_percentile_scale * highMean)
                                   + mid_percentile_weight * (mid_percentile_scale * midMean)
                                   + power_mean_weight * powerMean;

            return rawDifficulty * (noteWeight / (noteWeight + note_count_offset)) * final_scaling;
        }

        private static double shortLnCoordNerf(double speed, double technical, double jack, double coordination, double release, double lnRatio, double noteWeight)
        {
            double total = speed + technical + jack + coordination + release;

            if (total <= 0.0)
                return 1.0;

            double coordDom = coordination / total;
            double lnGate = DifficultyCalculationUtils.Smoothstep(lnRatio, slc_ln_lo, slc_ln_hi);
            double shortGate = DifficultyCalculationUtils.Smoothstep(slc_weight_hi - noteWeight, 0.0, slc_weight_hi - slc_weight_lo);
            double coordGate = DifficultyCalculationUtils.Smoothstep(coordDom, slc_coord_dom_lo, slc_coord_dom_hi);

            return 1.0 - short_ln_coord_nerf * lnGate * shortGate * coordGate;
        }

        private static double scaleToStarRating(double aggregatedDifficulty)
        {
            if (aggregatedDifficulty <= 0)
                return 0.0;

            return overall_multiplier * Math.Pow(aggregatedDifficulty, power_exponent);
        }

        private double skillStarRating(StrainSkill skill, double noteWeight)
            => scaleToStarRating(aggregateDifficulty(skill.GetObjectDifficulties(), noteWeight));

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

        /// <summary>
        /// Calculates the mean of specific percentile values from a sorted array.
        /// </summary>
        /// <param name="sortedValues">Array of difficulty values, sorted ascending.</param>
        /// <param name="percentiles">Array of percentile positions (0.0 to 1.0).</param>
        private double calculatePercentileMean(double[] sortedValues, double[] percentiles)
        {
            int maxIndex = sortedValues.Length - 1;
            double sum = 0.0;

            foreach (double percentile in percentiles)
            {
                int index = Math.Clamp((int)Math.Round(maxIndex * percentile), 0, maxIndex);
                sum += sortedValues[index];
            }

            return sum / percentiles.Length;
        }

        private double calculatePowerMean(double[] values, double exponent)
        {
            double sum = values.Sum(value => Math.Pow(value, exponent));
            return Math.Pow(sum / values.Length, 1.0 / exponent);
        }

        private static double computeLnRatio(IBeatmap beatmap)
        {
            int total = 0, ln = 0;

            foreach (var hitObject in beatmap.HitObjects)
            {
                total++;
                if (hitObject is HoldNote)
                    ln++;
            }

            return total > 0 ? (double)ln / total : 0.0;
        }

        private double totalNoteWeight(IBeatmap beatmap)
        {
            double weight = 0.0;

            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject is HoldNote holdNote)
                {
                    double duration = Math.Min(holdNote.EndTime - holdNote.StartTime, max_long_note_weight_duration_ms);
                    weight += 1.0 + long_note_weight_per_200_ms * duration / 200.0;
                }
                else
                    weight += 1.0;
            }

            return weight;
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

            ManipulationEvaluator.Evaluate(objects.Cast<ManiaDifficultyHitObject>().ToList());

            return objects;
        }

        protected override IEnumerable<DifficultyHitObject> SortObjects(IEnumerable<DifficultyHitObject> input) => input;

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        {
            return new Skill[]
            {
                new Speed(mods),
                new Technical(mods),
                new Jack(mods),
                new Coordination(mods),
                new Release(mods),
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
