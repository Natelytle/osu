// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using MathNet.Numerics;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Difficulty.UnstableRateEstimation
{
    public class ManiaDefaultUnstableRateEstimator : ManiaUnstableRateEstimator
    {
        private const double tail_leniency_multiplier = 1.5;

        private static readonly double[] hit_windows = new double[5];

        public ManiaDefaultUnstableRateEstimator(IRulesetInfo ruleset, DifficultyAttributes attributes)
            : base(ruleset, attributes)
        {
        }

        /// <summary>
        /// Returns the estimated unstable rate of the score, assuming the average hit location is in the center of the hit window.
        /// <exception cref="MathNet.Numerics.Optimization.MaximumIterationsException">
        /// Thrown when the optimization algorithm fails to converge.
        /// This should never happen. Even when tested up to 100 Million misses, the algorithm converges with default settings.
        /// </exception>
        /// <returns>
        /// Returns Estimated UR, or null if the score is a miss-only score.
        /// </returns>
        /// </summary>
        public override double? ComputeEstimatedUnstableRate(ScoreInfo score, bool withMisses = true)
        {
            SetStatistics(score);

            ManiaDifficultyAttributes maniaAttributes = (ManiaDifficultyAttributes)Attributes;
            setHitWindows(maniaAttributes.Mods, maniaAttributes.OverallDifficulty);

            if (TotalSuccessfulHits == 0 || maniaAttributes.NoteCount + maniaAttributes.HoldNoteCount == 0)
                return null;

            double logNoteHeadCount = Math.Log(maniaAttributes.NoteCount + maniaAttributes.HoldNoteCount);
            double logTailCount = Math.Log(maniaAttributes.HoldNoteCount);

            double noteHeadPortion = (double)(maniaAttributes.NoteCount + maniaAttributes.HoldNoteCount) / (maniaAttributes.NoteCount + maniaAttributes.HoldNoteCount * 2);
            double tailPortion = (double)maniaAttributes.HoldNoteCount / (maniaAttributes.NoteCount + maniaAttributes.HoldNoteCount * 2);

            double likelihoodGradient(double d)
            {
                if (d <= 0)
                    return 0;

                // Players release on LN tails with a higher deviation, so we find 2 values for deviation that average to the total unstable rate.
                // To average standard deviations, you must average the variances, so tail deviation multiplier is raised to the power 2 before being averaged.
                double dNote = d / Math.Sqrt(noteHeadPortion + tailPortion * Math.Pow(TAIL_DEVIATION_MULTIPLIER, 2));
                double dTail = dNote * TAIL_DEVIATION_MULTIPLIER;

                JudgementProbs pNotesHolds = logPNote(dNote);
                JudgementProbs pTails = logPNote(dTail, tail_leniency_multiplier);

                double pMax = LogSum(pNotesHolds.PMax + logNoteHeadCount, pTails.PMax + logTailCount) - Math.Log(TotalHits);
                double p300 = LogSum(pNotesHolds.P300 + logNoteHeadCount, pTails.P300 + logTailCount) - Math.Log(TotalHits);
                double p200 = LogSum(pNotesHolds.P200 + logNoteHeadCount, pTails.P200 + logTailCount) - Math.Log(TotalHits);
                double p100 = LogSum(pNotesHolds.P100 + logNoteHeadCount, pTails.P100 + logTailCount) - Math.Log(TotalHits);
                double p50 = LogSum(pNotesHolds.P50 + logNoteHeadCount, pTails.P50 + logTailCount) - Math.Log(TotalHits);
                double p0 = 0;

                // In some cases you don't want to include misses in your unstable rate estimation.
                // For example, the results screen unstable rate doesn't change when you miss.
                if (withMisses)
                    p0 = LogSum(pNotesHolds.P0 + logNoteHeadCount, pTails.P0 + logTailCount) - Math.Log(TotalHits);

                double totalProb = Math.Exp(
                    (CountPerfect * pMax
                     + (CountGreat + 0.5) * p300
                     + CountGood * p200
                     + CountOk * p100
                     + CountMeh * p50
                     + CountMiss * p0) / TotalHits
                );

                return -totalProb;
            }

            // Finding the minimum of the function returns the most likely deviation for the hit results. UR is deviation * 10.
            double deviation = FindMinimum.OfScalarFunction(likelihoodGradient, 30);

            return deviation * 10;
        }

        private struct JudgementProbs
        {
            public double PMax;
            public double P300;
            public double P200;
            public double P100;
            public double P50;
            public double P0;
        }

        // Log Judgement Probabilities of a Note or a Tail given a deviation. The multiplier is for Tails, which are 1.5x as lenient.
        private static JudgementProbs logPNote(double d, double multiplier = 1.0)
        {
            JudgementProbs probabilities = new JudgementProbs
            {
                PMax = LogDiff(0, logPcNote(hit_windows[0] * multiplier, d)),
                P300 = LogDiff(logPcNote(hit_windows[0] * multiplier, d), logPcNote(hit_windows[1] * multiplier, d)),
                P200 = LogDiff(logPcNote(hit_windows[1] * multiplier, d), logPcNote(hit_windows[2] * multiplier, d)),
                P100 = LogDiff(logPcNote(hit_windows[2] * multiplier, d), logPcNote(hit_windows[3] * multiplier, d)),
                P50 = LogDiff(logPcNote(hit_windows[3] * multiplier, d), logPcNote(hit_windows[4] * multiplier, d)),
                P0 = logPcNote(hit_windows[4], d)
            };

            return probabilities;
        }

        /// The log complementary probability of getting a certain judgement with a certain deviation on regular notes.
        private static double logPcNote(double window, double deviation) => LogErfc(window / (deviation * Math.Sqrt(2)));

        private static void setHitWindows(Mod[] mods, double overallDifficulty)
        {
            double windowMultiplier = 1;

            if (mods.Any(m => m is ModHardRock))
                windowMultiplier *= 1 / 1.4;
            else if (mods.Any(m => m is ModEasy))
                windowMultiplier *= 1.4;

            if (overallDifficulty < 5)
                hit_windows[0] = (22.4 - 0.6 * overallDifficulty) * windowMultiplier;
            else
                hit_windows[0] = (24.9 - 1.1 * overallDifficulty) * windowMultiplier;
            hit_windows[1] = (64 - 3 * overallDifficulty) * windowMultiplier;
            hit_windows[2] = (97 - 3 * overallDifficulty) * windowMultiplier;
            hit_windows[3] = (127 - 3 * overallDifficulty) * windowMultiplier;
            hit_windows[4] = (151 - 3 * overallDifficulty) * windowMultiplier;
        }
    }
}
