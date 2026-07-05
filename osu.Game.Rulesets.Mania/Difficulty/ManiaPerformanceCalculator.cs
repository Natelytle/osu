// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaPerformanceCalculator : PerformanceCalculator
    {
        private const double ur_sr_lo = 6.0;
        private const double ur_sr_hi = 11.0;
        private const double ur_shift_easy = 150.0;
        private const double ur_shift_hard = 150.0;
        private const double ur_exp_easy = 3.4;
        private const double ur_exp_hard = 1.15;
        private const double ur_acc_min = 0.55;
        private const double ur_acc_max = 0.95;
        private const double ur_acc_max_hard = 0.95;
        private const double ur_ceiling_sr_lo = 10.0;
        private const double ur_ceiling_sr_hi = 11.5;

        private const double low_acc_fade_lo = 0.80;
        private const double low_acc_fade_hi = 0.90;

        // Long-note charts drag tap accuracy down through tail-release timing, which is not the tap "accuracy
        // skill" the UR scaling rewards. The estimate is discounted by how release-heavy the map is
        // (ReleaseDifficulty), so genuine LN charts keep their reward while rice / short-LN charts are untouched.
        private const double release_discount_strength = 1.1;
        private const double release_discount_lo = 1.5;
        private const double release_discount_hi = 2.5;

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
        private bool isLegacyScore;

        private double? estimatedUnstableRate;

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
            isLegacyScore = score.Mods.Any(m => m is ManiaModClassic) && (totalHits + 0.1) > maniaAttributes.NoteCount + maniaAttributes.HoldNoteCount;

            double[] hitWindows = isLegacyScore
                ? getLegacyHitWindows(score.Mods, false, maniaAttributes.OverallDifficulty)
                : getLazerHitWindows(score.Mods, maniaAttributes.OverallDifficulty);

            estimatedUnstableRate = computeEstimatedUnstableRate(hitWindows, maniaAttributes.NoteCount, maniaAttributes.HoldNoteCount);

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
                EstimatedUnstableRate = estimatedUnstableRate,
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
            if (estimatedUnstableRate == null)
                return ur_acc_min;

            double lowAccFade = DiffUtils.Smoothstep(calculateCustomAccuracy(), low_acc_fade_lo, low_acc_fade_hi);

            return lowAccFade * accuracyScaling(noteUnstableRate(estimatedUnstableRate.Value, attributes.ReleaseDifficulty), attributes.StarRating);
        }

        /// <summary>
        /// Discounts the estimated unstable rate on long-note charts, whose MAX proportion is dragged down by tail
        /// releases rather than by tap inaccuracy. The discount is driven by the map's release difficulty, so heavy
        /// LN charts keep their accuracy reward while rice and short/easy-LN charts (low release) are left untouched.
        /// </summary>
        private static double noteUnstableRate(double unstableRate, double releaseDifficulty)
        {
            double discount = 1.0 + release_discount_strength * DiffUtils.Smoothstep(releaseDifficulty, release_discount_lo, release_discount_hi);

            return unstableRate / discount;
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

        #region Unstable Rate Estimation

        private const double tail_multiplier = 1.5; // Lazer LN tails have 1.5x the hit window of a Note or an LN head.
        private const double tail_deviation_multiplier = 1.8; // Empirical testing shows that players get ~1.8x the deviation on tails.

        // Multipliers for legacy LN hit windows. These are made slightly more lenient for some reason.
        private const double legacy_max_multiplier = 1.2;
        private const double legacy_300_multiplier = 1.1;

        /// <summary>
        /// Returns the estimated unstable rate of the score, assuming the average hit location is in the center of the
        /// hit window. Returns null if the score is a miss-only score.
        /// </summary>
        private double? computeEstimatedUnstableRate(double[] hitWindows, int noteCount, int holdNoteCount)
        {
            if (totalSuccessfulHits == 0 || noteCount + holdNoteCount == 0)
                return null;

            double noteHeadPortion = (double)(noteCount + holdNoteCount) / (noteCount + holdNoteCount * 2);
            double tailPortion = (double)holdNoteCount / (noteCount + holdNoteCount * 2);

            double likelihoodGradient(double d)
            {
                if (d <= 0)
                    return 0;

                // Since tails have a higher deviation, find the deviation values for notes/heads and tails that average out to the final deviation value.
                double dNote = d / Math.Sqrt(noteHeadPortion + tailPortion * Math.Pow(tail_deviation_multiplier, 2));
                double dTail = dNote * tail_deviation_multiplier;

                JudgementProbs pNotes = judgementProbs(hitWindows, dNote);
                // Since lazer tails have the same hit behaviour as Notes, return pNote instead of pHold for them.
                JudgementProbs pHolds = isLegacyScore ? judgementProbsLegacyHold(hitWindows, dNote, dTail) : judgementProbs(hitWindows, dTail, tail_multiplier);

                return -calculateLikelihoodOfDeviation(pNotes, pHolds, noteCount, holdNoteCount);
            }

            // Finding the minimum of the function returns the most likely deviation for the hit results. UR is deviation * 10.
            double deviation = RootFinding.FindMinimumExpand(likelihoodGradient, 0, 30);

            return deviation * 10;
        }

        private static double[] getLegacyHitWindows(Mod[] mods, bool isConvert, double overallDifficulty)
        {
            double[] legacyHitWindows = new double[5];

            double greatWindowLeniency = 0;
            double goodWindowLeniency = 0;

            // When converting beatmaps to osu!mania in stable, the resulting hit window sizes are dependent on whether the beatmap's OD is above or below 4.
            if (isConvert)
            {
                overallDifficulty = 10;

                if (overallDifficulty <= 4)
                {
                    greatWindowLeniency = 13;
                    goodWindowLeniency = 10;
                }
            }

            double windowMultiplier = 1;

            if (mods.Any(m => m is ModHardRock))
                windowMultiplier *= 1 / 1.4;
            else if (mods.Any(m => m is ModEasy))
                windowMultiplier *= 1.4;

            legacyHitWindows[0] = Math.Floor(16 * windowMultiplier);
            legacyHitWindows[1] = Math.Floor((64 - 3 * overallDifficulty + greatWindowLeniency) * windowMultiplier);
            legacyHitWindows[2] = Math.Floor((97 - 3 * overallDifficulty + goodWindowLeniency) * windowMultiplier);
            legacyHitWindows[3] = Math.Floor((127 - 3 * overallDifficulty) * windowMultiplier);
            legacyHitWindows[4] = Math.Floor((151 - 3 * overallDifficulty) * windowMultiplier);

            return legacyHitWindows;
        }

        private static double[] getLazerHitWindows(Mod[] mods, double overallDifficulty)
        {
            double[] lazerHitWindows = new double[5];

            double windowMultiplier = 1;

            if (mods.Any(m => m is ModHardRock))
                windowMultiplier *= 1 / 1.4;
            else if (mods.Any(m => m is ModEasy))
                windowMultiplier *= 1.4;

            if (overallDifficulty < 5)
                lazerHitWindows[0] = (22.4 - 0.6 * overallDifficulty) * windowMultiplier;
            else
                lazerHitWindows[0] = (24.9 - 1.1 * overallDifficulty) * windowMultiplier;
            lazerHitWindows[1] = (64 - 3 * overallDifficulty) * windowMultiplier;
            lazerHitWindows[2] = (97 - 3 * overallDifficulty) * windowMultiplier;
            lazerHitWindows[3] = (127 - 3 * overallDifficulty) * windowMultiplier;
            lazerHitWindows[4] = (151 - 3 * overallDifficulty) * windowMultiplier;

            return lazerHitWindows;
        }

        private struct JudgementProbs
        {
            public LogProbability PMax;
            public LogProbability P300;
            public LogProbability P200;
            public LogProbability P100;
            public LogProbability P50;
            public LogProbability P0;
        }

        // The probabilities of getting each judgement given a deviation.
        // The multiplier is for lazer LN tails, which are 1.5x as lenient.
        private JudgementProbs judgementProbs(double[] hitWindows, double d, double multiplier = 1)
        {
            JudgementProbs probabilities = new JudgementProbs
            {
                PMax = new LogProbability(1) - compProbHitWindow(hitWindows[0] * multiplier, d),
                P300 = compProbHitWindow(hitWindows[0] * multiplier, d) - compProbHitWindow(hitWindows[1] * multiplier, d),
                P200 = compProbHitWindow(hitWindows[1] * multiplier, d) - compProbHitWindow(hitWindows[2] * multiplier, d),
                P100 = compProbHitWindow(hitWindows[2] * multiplier, d) - compProbHitWindow(hitWindows[3] * multiplier, d),
                P50 = compProbHitWindow(hitWindows[3] * multiplier, d) - compProbHitWindow(hitWindows[4] * multiplier, d),
                P0 = compProbHitWindow(hitWindows[4] * multiplier, d)
            };

            return probabilities;
        }

        // The probabilities of getting each judgement given a deviation.
        // This is only used for Legacy Holds, which has a different hit behaviour from Notes and lazer LNs.
        private JudgementProbs judgementProbsLegacyHold(double[] hitWindows, double dHead, double dTail)
        {
            JudgementProbs probabilities = new JudgementProbs
            {
                PMax = new LogProbability(1) - compProbHitLegacyHold(hitWindows[0] * legacy_max_multiplier, dHead, dTail),
                P300 = compProbHitLegacyHold(hitWindows[0] * legacy_max_multiplier, dHead, dTail) - compProbHitLegacyHold(hitWindows[1] * legacy_300_multiplier, dHead, dTail),
                P200 = compProbHitLegacyHold(hitWindows[1] * legacy_300_multiplier, dHead, dTail) - compProbHitLegacyHold(hitWindows[2], dHead, dTail),
                P100 = compProbHitLegacyHold(hitWindows[2], dHead, dTail) - compProbHitLegacyHold(hitWindows[3], dHead, dTail),
                P50 = compProbHitLegacyHold(hitWindows[3], dHead, dTail) - compProbHitLegacyHold(hitWindows[4], dHead, dTail),
                P0 = compProbHitLegacyHold(hitWindows[4], dHead, dTail)
            };

            return probabilities;
        }

        /// <summary>
        /// Combines the probability of getting each judgement on both note types into a single probability value for each judgement,
        /// and compares them to the judgements of the play using a binomial likelihood formula.
        /// </summary>
        private double calculateLikelihoodOfDeviation(JudgementProbs noteProbabilities, JudgementProbs lnProbabilities, double noteCount, double lnCount)
        {
            // Lazer mechanics treat the heads of LNs like notes.
            double noteProbCount = isLegacyScore ? noteCount : noteCount + lnCount;

            LogProbability pMax = LogProbability.Combine(noteProbabilities.PMax, lnProbabilities.PMax, noteProbCount, lnCount);
            LogProbability p300 = LogProbability.Combine(noteProbabilities.P300, lnProbabilities.P300, noteProbCount, lnCount);
            LogProbability p200 = LogProbability.Combine(noteProbabilities.P200, lnProbabilities.P200, noteProbCount, lnCount);
            LogProbability p100 = LogProbability.Combine(noteProbabilities.P100, lnProbabilities.P100, noteProbCount, lnCount);
            LogProbability p50 = LogProbability.Combine(noteProbabilities.P50, lnProbabilities.P50, noteProbCount, lnCount);
            LogProbability p0 = LogProbability.Combine(noteProbabilities.P0, lnProbabilities.P0, noteProbCount, lnCount);

            // Multinomial likelihood formula. 0.5 is added to countGreat since the most likely deviation for an SS would otherwise be 0.
            LogProbability totalProb = LogProbability.Pow(pMax, countPerfect / totalHits)
                                       * LogProbability.Pow(p300, (countGreat + 0.5) / totalHits)
                                       * LogProbability.Pow(p200, countGood / totalHits)
                                       * LogProbability.Pow(p100, countOk / totalHits)
                                       * LogProbability.Pow(p50, countMeh / totalHits)
                                       * LogProbability.Pow(p0, countMiss / totalHits);

            return totalProb.Probability;
        }

        /// <returns>
        /// The complementary (inverse) probability of landing within a hit window.
        /// </returns>
        private LogProbability compProbHitWindow(double window, double deviation) => erfc(window / (deviation * Math.Sqrt(2)));

        /// <returns>
        /// The complementary (inverse) probability of landing within both hit windows of classic LNs.
        /// </returns>
        private LogProbability compProbHitLegacyHold(double window, double headDeviation, double tailDeviation)
        {
            double root2 = Math.Sqrt(2);

            LogProbability logPcHead = erfc(window / (headDeviation * root2));

            // Calculate the expected value of the distance from 0 of the head hit, given it lands within the current window.
            // We'll subtract this from the tail window to approximate the difficulty of landing both hits within 2x the current window.
            double beta = window / headDeviation;
            double z = NormalCdf(0, 1, beta) - 0.5;
            double expectedValue = headDeviation * (NormalPdf(0, 1, 0) - NormalPdf(0, 1, beta)) / z;

            LogProbability logPcTail = erfc((2 * window - expectedValue) / (tailDeviation * root2));

            return logPcHead + logPcTail - logPcHead * logPcTail;
        }

        /// <summary>
        /// Probability density of the normal distribution with the given mean and standard deviation at <paramref name="x"/>.
        /// </summary>
        public static double NormalPdf(double mean, double standardDeviation, double x)
        {
            double z = (x - mean) / standardDeviation;
            return Math.Exp(-0.5 * z * z) / (standardDeviation * Math.Sqrt(2 * Math.PI));
        }

        /// <summary>
        /// Cumulative distribution of the normal distribution with the given mean and standard deviation at <paramref name="x"/>.
        /// </summary>
        public static double NormalCdf(double mean, double standardDeviation, double x)
            => 0.5 * DiffUtils.Erfc((mean - x) / (standardDeviation * DiffUtils.SQRT2));

        private LogProbability erfc(double x) => x <= 5
            ? new LogProbability(DiffUtils.Erfc(x))
            : -Math.Pow(x, 2) - Math.Log(x * Math.Sqrt(Math.PI)); // This is an approximation, https://www.desmos.com/calculator/kdbxwxgf01

        #endregion

        private static double denseFastMultiplier(ManiaDifficultyAttributes attributes)
        {
            double coActivation = Math.Min(attributes.SpeedDifficulty, attributes.JackDifficulty);
            double coGate = DiffUtils.Smoothstep(coActivation, dense_coact_lo, dense_coact_hi);
            double releaseGate = 1.0 - DiffUtils.Smoothstep(attributes.ReleaseDifficulty, dense_release_lo, dense_release_hi);
            double srTaper = 1.0 - DiffUtils.Smoothstep(attributes.StarRating, dense_sr_taper_lo, dense_sr_taper_hi);

            return 1.0 + dense_buff * coGate * releaseGate * srTaper;
        }

        private double totalHits => countPerfect + countOk + countGreat + countGood + countMeh + countMiss;
        private double totalSuccessfulHits => totalHits - countMiss;

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
