// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics;
using MathNet.Numerics.Distributions;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using Precision = osu.Framework.Utils.Precision;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaPerformanceCalculator : PerformanceCalculator
    {
        private const double tail_multiplier = 1.5; // Lazer LN tails have 1.5x the hit window of a Note or an LN head.
        private const double tail_deviation_multiplier = 1.8; // Empirical testing shows that players get ~1.8x the deviation on tails.

        // Multipliers for legacy LN hit windows. These are made slightly more lenient for some reason.
        private const double legacy_max_multiplier = 1.2;
        private const double legacy_300_multiplier = 1.1;

        private int countPerfect;
        private int countGreat;
        private int countGood;
        private int countOk;
        private int countMeh;
        private int countMiss;
        private double? estimatedUr;
        private bool isLegacyScore;
        private double[] hitWindows = null!;
        private bool isConvert;

        public ManiaPerformanceCalculator()
            : base(new ManiaRuleset())
        {
        }

        public new ManiaPerformanceAttributes Calculate(ScoreInfo score, DifficultyAttributes attributes)
            => (ManiaPerformanceAttributes)CreatePerformanceAttributes(score, attributes);

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var maniaAttributes = (ManiaDifficultyAttributes)attributes;

            isConvert = score.BeatmapInfo!.Ruleset.OnlineID != 3;

            countPerfect = score.Statistics.GetValueOrDefault(HitResult.Perfect);
            countGreat = score.Statistics.GetValueOrDefault(HitResult.Great);
            countGood = score.Statistics.GetValueOrDefault(HitResult.Good);
            countOk = score.Statistics.GetValueOrDefault(HitResult.Ok);
            countMeh = score.Statistics.GetValueOrDefault(HitResult.Meh);
            countMiss = score.Statistics.GetValueOrDefault(HitResult.Miss);
            isLegacyScore = score.Mods.Any(m => m is ManiaModClassic) && !Precision.DefinitelyBigger(totalJudgements, maniaAttributes.NoteCount + maniaAttributes.HoldNoteCount);

            hitWindows = isLegacyScore
                ? getLegacyHitWindows(score.Mods, isConvert, maniaAttributes.OverallDifficulty)
                : getLazerHitWindows(score.Mods, maniaAttributes.OverallDifficulty);

            estimatedUr = computeEstimatedUr(maniaAttributes.NoteCount, maniaAttributes.HoldNoteCount);

            // Arbitrary initial value for scaling pp in order to standardize distributions across game modes.
            // The specific number has no intrinsic meaning and can be adjusted as needed.
            double multiplier = 8.0;

            if (score.Mods.Any(m => m is ModNoFail))
                multiplier *= 0.75;
            if (score.Mods.Any(m => m is ModEasy))
                multiplier *= 0.5;

            double difficultyValue = computeDifficultyValue(maniaAttributes);
            double totalValue = difficultyValue * multiplier;

            return new ManiaPerformanceAttributes
            {
                Difficulty = difficultyValue,
                Total = totalValue,
                EstimatedUr = estimatedUr,
                HitWindows = hitWindows
            };
        }

        private double computeDifficultyValue(ManiaDifficultyAttributes attributes)
        {
            double difficultyValue = Math.Pow(Math.Max(attributes.StarRating - 0.15, 0.05), 2.2)
                                     * (1 + 0.1 * Math.Min(1, (attributes.NoteCount + attributes.HoldNoteCount) / 1500.0)); // Star rating to pp curve

            if (estimatedUr == null)
                return 0;

            double noteHeadPortion = (double)(attributes.NoteCount + attributes.HoldNoteCount) / (attributes.NoteCount + attributes.HoldNoteCount * 2);
            double tailPortion = (double)attributes.HoldNoteCount / (attributes.NoteCount + attributes.HoldNoteCount * 2);

            // We increased the deviation of tails for estimation accuracy, but for difficulty scaling we actually
            // only care about the deviation on notes and heads, as that's the "accuracy skill" of the player.
            double noteUnstableRate = estimatedUr.Value / Math.Sqrt(noteHeadPortion + tailPortion * Math.Pow(tail_deviation_multiplier, 2));

            difficultyValue *= Math.Max(1 - Math.Pow(noteUnstableRate / 500, 1.9), 0);

            return difficultyValue;
        }

        private double totalJudgements => countPerfect + countOk + countGreat + countGood + countMeh + countMiss;
        private double totalSuccessfulJudgements => countPerfect + countOk + countGreat + countGood + countMeh;

        /// <summary>
        /// Returns the estimated unstable rate of the score, assuming the average hit location is in the center of the hit window.
        /// <returns>
        /// Returns Estimated UR, or null if the score is a miss-only score.
        /// </returns>
        /// </summary>
        private double? computeEstimatedUr(int noteCount, int holdNoteCount)
        {
            if (totalSuccessfulJudgements == 0 || noteCount + holdNoteCount == 0)
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

                JudgementProbs pNotes = judgementProbs(dNote);
                // Since lazer tails have the same hit behaviour as Notes, return pNote instead of pHold for them.
                JudgementProbs pHolds = isLegacyScore ? judgementProbsLegacyHold(dNote, dTail) : judgementProbs(dTail, tail_multiplier);

                return -calculateLikelihoodOfDeviation(pNotes, pHolds, noteCount, holdNoteCount);
            }

            // Finding the minimum of the function returns the most likely deviation for the hit results. UR is deviation * 10.
            double deviation = FindMinimum.OfScalarFunction(likelihoodGradient, 30);

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
            public LogProb PMax;
            public LogProb P300;
            public LogProb P200;
            public LogProb P100;
            public LogProb P50;
            public LogProb P0;
        }

        // The probabilities of getting each judgement given a deviation.
        // The multiplier is for lazer LN tails, which are 1.5x as lenient.
        private JudgementProbs judgementProbs(double d, double multiplier = 1)
        {
            JudgementProbs probabilities = new JudgementProbs
            {
                PMax = 0 - compProbHitWindow(hitWindows[0] * multiplier, d),
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
        private JudgementProbs judgementProbsLegacyHold(double dHead, double dTail)
        {
            JudgementProbs probabilities = new JudgementProbs
            {
                PMax = 0 - compProbHitLegacyHold(hitWindows[0] * legacy_max_multiplier, dHead, dTail),
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

            LogProb pMax = LogProb.Combine(noteProbabilities.PMax, lnProbabilities.PMax, noteProbCount, lnCount);
            LogProb p300 = LogProb.Combine(noteProbabilities.P300, lnProbabilities.P300, noteProbCount, lnCount);
            LogProb p200 = LogProb.Combine(noteProbabilities.P200, lnProbabilities.P200, noteProbCount, lnCount);
            LogProb p100 = LogProb.Combine(noteProbabilities.P100, lnProbabilities.P100, noteProbCount, lnCount);
            LogProb p50 = LogProb.Combine(noteProbabilities.P50, lnProbabilities.P50, noteProbCount, lnCount);
            LogProb p0 = LogProb.Combine(noteProbabilities.P0, lnProbabilities.P0, noteProbCount, lnCount);

            // Multinomial likelihood formula. 0.5 is added to countGreat since the most likely deviation for an SS would otherwise be 0.
            LogProb totalProb = LogProb.Pow(pMax, countPerfect / totalJudgements)
                                * LogProb.Pow(p300, (countGreat + 0.5) / totalJudgements)
                                * LogProb.Pow(p200, countGood / totalJudgements)
                                * LogProb.Pow(p100, countOk / totalJudgements)
                                * LogProb.Pow(p50, countMeh / totalJudgements)
                                * LogProb.Pow(p0, countMiss / totalJudgements);

            return totalProb.Probability;
        }

        /// <returns>
        /// The complementary (inverse) probability of landing within a hit window.
        /// </returns>
        private LogProb compProbHitWindow(double window, double deviation) => erfc(window / (deviation * Math.Sqrt(2)));

        /// <returns>
        /// The complementary (inverse) probability of landing within both hit windows of classic LNs.
        /// </returns>
        private LogProb compProbHitLegacyHold(double window, double headDeviation, double tailDeviation)
        {
            double root2 = Math.Sqrt(2);

            LogProb logPcHead = erfc(window / (headDeviation * root2));

            // Calculate the expected value of the distance from 0 of the head hit, given it lands within the current window.
            // We'll subtract this from the tail window to approximate the difficulty of landing both hits within 2x the current window.
            double beta = window / headDeviation;
            double z = Normal.CDF(0, 1, beta) - 0.5;
            double expectedValue = headDeviation * (Normal.PDF(0, 1, 0) - Normal.PDF(0, 1, beta)) / z;

            LogProb logPcTail = erfc((2 * window - expectedValue) / (tailDeviation * root2));

            return logPcHead + logPcTail - logPcHead * logPcTail;
        }

        private LogProb erfc(double x) => x <= 5
            ? new LogProb(SpecialFunctions.Erfc(x))
            : -Math.Pow(x, 2) - Math.Log(x * Math.Sqrt(Math.PI)); // This is an approximation, https://www.desmos.com/calculator/kdbxwxgf01
    }
}
