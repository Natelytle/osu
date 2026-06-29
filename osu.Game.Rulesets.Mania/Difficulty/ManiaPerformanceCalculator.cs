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
using osu.Game.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaPerformanceCalculator : PerformanceCalculator
    {
        private const double release_acc_weight = 1;
        private const double jack_ease = 3.0;

        private const double acc_floor = 0.80;
        private const double acc_exp_low_sr = 1.53;
        private const double acc_exp_high_sr = 0.50;
        private const double acc_sr_lo = 2.0;
        private const double acc_sr_hi = 10.0;

        private const double acc_jack_boost = 0.25;
        private const double acc_balance_low = 0.27;
        private const double acc_balance_high = 0.50;

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

        private const double rice_compression_base = 0.115;
        private const double rice_compression_rate_extra = 0.0;
        private const double rice_share_lo = 0.05;
        private const double rice_share_hi = 0.108;
        private const double rice_sr_lo = 7.0;
        private const double rice_sr_hi = 9.5;

        private const double rice_ln_lo = 0.25;
        private const double rice_ln_hi = 0.70;

        private const double ln_nerf_strength = 0.40;
        private const double ln_nerf_lo = 0.30;
        private const double ln_nerf_hi = 0.60;
        private const double ln_nerf_sr_lo = 8.5;
        private const double ln_nerf_sr_hi = 10.0;

        private double clockRate = 1.0;

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

            clockRate = ModUtils.CalculateRateWithMods(score.Mods);

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
            return variety_floor + DifficultyCalculationUtils.Logistic(variety, variety_midpoint, variety_steepness, range);
        }

        private double lengthMultiplier(double totalNotes, double starRating)
        {
            if (totalNotes <= 0)
                return 1.0;

            return 1.1 / (1.0 + Math.Sqrt(starRating / (2.0 * totalNotes)));
        }

        private double computeDifficultyValue(ManiaDifficultyAttributes attributes)
        {
            double baseValue = 7.5 * Math.Pow(Math.Max(attributes.StarRating - 0.15, 0.05), 2.2); // Star rating to pp curve
            return baseValue * denseFastMultiplier(attributes) * riceCompressionMultiplier(attributes) * lnNerfMultiplier(attributes) * accuracyFactor(attributes);
        }

        private static double lnNerfMultiplier(ManiaDifficultyAttributes attributes)
        {
            double lnGate = DifficultyCalculationUtils.Smoothstep(attributes.LnRatio, ln_nerf_lo, ln_nerf_hi);
            double srFade = 1.0 - DifficultyCalculationUtils.Smoothstep(attributes.StarRating, ln_nerf_sr_lo, ln_nerf_sr_hi);

            return 1.0 - ln_nerf_strength * lnGate * srFade;
        }

        private double riceCompressionMultiplier(ManiaDifficultyAttributes attributes)
        {
            double skillSum = attributes.SpeedDifficulty + attributes.TechnicalDifficulty + attributes.JackDifficulty
                              + attributes.CoordinationDifficulty + attributes.ReleaseDifficulty;
            double releaseShare = skillSum > 0 ? attributes.ReleaseDifficulty / skillSum : 0.0;

            double riceGate = 1.0 - DifficultyCalculationUtils.Smoothstep(releaseShare, rice_share_lo, rice_share_hi);
            double lnGate = 1.0 - DifficultyCalculationUtils.Smoothstep(attributes.LnRatio, rice_ln_lo, rice_ln_hi);
            double srGate = DifficultyCalculationUtils.Smoothstep(attributes.StarRating, rice_sr_lo, rice_sr_hi);

            double strength = rice_compression_base + rice_compression_rate_extra * Math.Max(0.0, clockRate - 1.0);

            return 1.0 - strength * riceGate * lnGate * srGate;
        }

        private double accuracyFactor(ManiaDifficultyAttributes attributes)
        {
            double srHardness = DifficultyCalculationUtils.Smoothstep(attributes.StarRating, acc_sr_lo, acc_sr_hi);
            double exponent = acc_exp_low_sr + (acc_exp_high_sr - acc_exp_low_sr) * srHardness;

            double balance = accDifficultyBalance(attributes);
            double jackiness = 1.0 - DifficultyCalculationUtils.Smoothstep(balance, acc_balance_low, acc_balance_high);
            exponent *= 1.0 + acc_jack_boost * jackiness;

            return Math.Pow(DifficultyCalculationUtils.ReverseLerp(calculateCustomAccuracy(), acc_floor, 1.0), exponent);
        }

        private static double denseFastMultiplier(ManiaDifficultyAttributes attributes)
        {
            double coActivation = Math.Min(attributes.SpeedDifficulty, attributes.JackDifficulty);
            double coGate = DifficultyCalculationUtils.Smoothstep(coActivation, dense_coact_lo, dense_coact_hi);
            double releaseGate = 1.0 - DifficultyCalculationUtils.Smoothstep(attributes.ReleaseDifficulty, dense_release_lo, dense_release_hi);
            double srTaper = 1.0 - DifficultyCalculationUtils.Smoothstep(attributes.StarRating, dense_sr_taper_lo, dense_sr_taper_hi);

            return 1.0 + dense_buff * coGate * releaseGate * srTaper;
        }

        private static double accDifficultyBalance(ManiaDifficultyAttributes attributes)
        {
            double skillsDifficultySum = attributes.SpeedDifficulty + attributes.TechnicalDifficulty + attributes.CoordinationDifficulty
                                         + release_acc_weight * attributes.ReleaseDifficulty;
            double jackDifficulty = attributes.JackDifficulty;
            return skillsDifficultySum / (skillsDifficultySum + jack_ease * jackDifficulty + 1e-9);
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
