// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaPerformanceCalculator : PerformanceCalculator
    {
        private const double ur_sr_lo = 6.0;
        private const double ur_sr_hi = 11.0;
        private const double ur_shift_easy = 155.0;
        private const double ur_shift_hard = 155.0;
        private const double ur_exp_easy = 2.8;
        private const double ur_exp_hard = 1.15;
        private const double ur_acc_min = 0.55;
        private const double ur_acc_max = 1.06;
        private const double ur_acc_max_hard = 1.20;
        private const double ur_ceiling_sr_lo = 10.0;
        private const double ur_ceiling_sr_hi = 11.5;

        private const double variety_floor = 0.88;
        private const double variety_cap = 1.10;
        private const double variety_midpoint = 3.7;
        private const double variety_steepness = 2.0;

        private const double dense_buff = 0.18;
        private const double dense_coact_lo = 3.0;
        private const double dense_coact_hi = 5.0;
        private const double dense_release_lo = 1.5;
        private const double dense_release_hi = 3.0;
        private const double dense_sr_taper_lo = 9.5;
        private const double dense_sr_taper_hi = 13.0;

        private int countPerfect;
        private int countGreat;
        private int countGood;
        private int countOk;
        private int countMeh;
        private int countMiss;

        public ManiaPerformanceCalculator()
            : base(new ManiaRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var maniaAttributes = (ManiaDifficultyAttributes)attributes;

            countPerfect = score.Statistics.GetValueOrDefault(HitResult.Perfect);
            countGreat = score.Statistics.GetValueOrDefault(HitResult.Great);
            countGood = score.Statistics.GetValueOrDefault(HitResult.Good);
            countOk = score.Statistics.GetValueOrDefault(HitResult.Ok);
            countMeh = score.Statistics.GetValueOrDefault(HitResult.Meh);
            countMiss = score.Statistics.GetValueOrDefault(HitResult.Miss);

            double multiplier = 1.0;

            if (score.Mods.Any(m => m is ModNoFail))
                multiplier *= 0.75;
            if (score.Mods.Any(m => m is ModEasy))
                multiplier *= 0.5;

            double difficultyValue = computeDifficultyValue(maniaAttributes);
            double varietyMultiplier = this.varietyMultiplier(maniaAttributes.Variety);
            double lengthMultiplier = this.lengthMultiplier(totalHits, maniaAttributes.StarRating);
            double totalValue = difficultyValue * varietyMultiplier * lengthMultiplier * multiplier;

            return new ManiaPerformanceAttributes
            {
                Difficulty = difficultyValue,
                Total = totalValue
            };
        }

        private double varietyMultiplier(double variety)
        {
            const double range = variety_cap - variety_floor;
            return variety_floor + DiffUtils.Logistic(variety, variety_midpoint, variety_steepness, range);
        }

        private double lengthMultiplier(double totalNotes, double starRating)
        {
            if (totalNotes <= 0)
                return 1.0;

            return 1.1 / (1.0 + Math.Sqrt(starRating / (2.0 * totalNotes)));
        }

        private double computeDifficultyValue(ManiaDifficultyAttributes attributes)
        {
            // Star rating to pp curve, plus the dense-fast bonus, forms the mechanical strain base.
            double strainBase = 7.5 * DiffUtils.Pow(Math.Max(attributes.StarRating - 0.15, 0.05), 2.2) * denseFastMultiplier(attributes);

            return strainBase * accuracyMultiplier(attributes);
        }

        /// <summary>
        /// Scales the strain by how accurately the map was played, driven by the estimated unstable rate.
        /// </summary>
        private double accuracyMultiplier(ManiaDifficultyAttributes attributes)
        {
            double[] windows = attributes.HitWindows;

            if (windows == null || windows.Length < 5 || windows[4] <= 0)
                return 1.0;

            return accuracyScaling(estimateUnstableRate(windows), attributes.StarRating);
        }

        private static double accuracyScaling(double unstableRate, double starRating)
        {
            double hardness = DiffUtils.Smoothstep(starRating, ur_sr_lo, ur_sr_hi);
            double shift = ur_shift_easy + (ur_shift_hard - ur_shift_easy) * hardness;
            double exponent = ur_exp_easy + (ur_exp_hard - ur_exp_easy) * hardness;

            double precision = DiffUtils.Pow(DiffUtils.Erf(shift / (DiffUtils.SQRT2 * Math.Max(unstableRate, 1e-6))), exponent);

            double ceiling = ur_acc_max + (ur_acc_max_hard - ur_acc_max) * DiffUtils.Smoothstep(starRating, ur_ceiling_sr_lo, ur_ceiling_sr_hi);

            return ur_acc_min + (ceiling - ur_acc_min) * precision;
        }

        private double estimateUnstableRate(double[] windows)
        {
            double targetAccuracy = calculateCustomAccuracy();

            double low = 0.5, high = 400.0;

            for (int i = 0; i < 80; i++)
            {
                double mid = 0.5 * (low + high);

                if (expectedCustomAccuracy(mid, windows) > targetAccuracy)
                    low = mid;
                else
                    high = mid;
            }

            return 10.0 * 0.5 * (low + high);
        }

        /// <summary>
        /// The custom accuracy a player with the given timing deviation (ms) is expected to score, from the
        /// probability of each judgement under a centred normal error model and the map's hit windows.
        /// </summary>
        private static double expectedCustomAccuracy(double deviation, double[] windows)
        {
            double within(double window) => DiffUtils.Erf(window / (deviation * DiffUtils.SQRT2));

            double belowPerfect = within(windows[0]);
            double belowGreat = within(windows[1]);
            double belowGood = within(windows[2]);
            double belowOk = within(windows[3]);
            double belowMeh = within(windows[4]);

            double pPerfect = belowPerfect;
            double pGreat = belowGreat - belowPerfect;
            double pGood = belowGood - belowGreat;
            double pOk = belowOk - belowGood;
            double pMeh = belowMeh - belowOk;

            return (320 * pPerfect + 300 * pGreat + 200 * pGood + 100 * pOk + 50 * pMeh) / 320.0;
        }

        private static double denseFastMultiplier(ManiaDifficultyAttributes attributes)
        {
            double coActivation = Math.Min(attributes.SpeedDifficulty, attributes.JackDifficulty);
            double coGate = DiffUtils.Smoothstep(coActivation, dense_coact_lo, dense_coact_hi);
            double releaseGate = 1.0 - DiffUtils.Smoothstep(attributes.ReleaseDifficulty, dense_release_lo, dense_release_hi);
            double srTaper = 1.0 - DiffUtils.Smoothstep(attributes.StarRating, dense_sr_taper_lo, dense_sr_taper_hi);

            return 1.0 + dense_buff * coGate * releaseGate * srTaper;
        }

        private double totalHits => countPerfect + countOk + countGreat + countGood + countMeh + countMiss;

        /// <summary>
        /// Accuracy used to weight judgements independently from the score's actual accuracy.
        /// </summary>
        private double calculateCustomAccuracy()
        {
            if (totalHits == 0)
                return 0;

            return (countPerfect * 320 + countGreat * 300 + countGood * 200 + countOk * 100 + countMeh * 50) / (totalHits * 320);
        }
    }
}
