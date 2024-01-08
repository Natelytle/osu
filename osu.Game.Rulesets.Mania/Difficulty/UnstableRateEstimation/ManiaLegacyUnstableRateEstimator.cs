// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using MathNet.Numerics;
using MathNet.Numerics.Distributions;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Difficulty.UnstableRateEstimation
{
    public class ManiaLegacyUnstableRateEstimator : ManiaUnstableRateEstimator
    {
        private static readonly double[] hit_windows = new double[5];

        private static bool isConvert;

        public ManiaLegacyUnstableRateEstimator(IRulesetInfo ruleset, DifficultyAttributes attributes)
            : base(ruleset, attributes)
        {
        }

        /// <inheritdoc />
        /// <exception cref="T:MathNet.Numerics.Optimization.MaximumIterationsException">
        /// Thrown when the optimization algorithm fails to converge.
        /// This should never happen. Even when tested up to 100 Million misses, the algorithm converges with default settings.
        /// </exception>
        public override double? ComputeEstimatedUnstableRate(ScoreInfo score, bool withMisses = true)
        {
            SetStatistics(score);

            ManiaDifficultyAttributes maniaAttributes = (ManiaDifficultyAttributes)Attributes;
            isConvert = score.BeatmapInfo!.Ruleset.OnlineID != 3;
            setHitWindows(maniaAttributes.Mods, maniaAttributes.OverallDifficulty);

            if (TotalSuccessfulHits == 0 || maniaAttributes.NoteCount + maniaAttributes.HoldNoteCount == 0)
                return null;

            double logNoteCount = Math.Log(maniaAttributes.NoteCount);
            double logHoldCount = Math.Log(maniaAttributes.HoldNoteCount);

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

                JudgementProbs pNotes = logPNote(dNote);
                JudgementProbs pHolds = logPHold(dNote, dTail);

                double pMax = LogSum(pNotes.PMax + logNoteCount, pHolds.PMax + logHoldCount) - Math.Log(TotalHits);
                double p300 = LogSum(pNotes.P300 + logNoteCount, pHolds.P300 + logHoldCount) - Math.Log(TotalHits);
                double p200 = LogSum(pNotes.P200 + logNoteCount, pHolds.P200 + logHoldCount) - Math.Log(TotalHits);
                double p100 = LogSum(pNotes.P100 + logNoteCount, pHolds.P100 + logHoldCount) - Math.Log(TotalHits);
                double p50 = LogSum(pNotes.P50 + logNoteCount, pHolds.P50 + logHoldCount) - Math.Log(TotalHits);
                double p0 = 0;

                // In some cases you don't want to include misses in your unstable rate estimation.
                // For example, the results screen unstable rate doesn't change when you miss.
                if (withMisses)
                    p0 = LogSum(pNotes.P0 + logNoteCount, pHolds.P0 + logHoldCount) - Math.Log(TotalHits);

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

        // Log Judgement Probabilities of a Note given a deviation.
        private static JudgementProbs logPNote(double d)
        {
            JudgementProbs probabilities = new JudgementProbs
            {
                PMax = LogDiff(0, logPcNote(hit_windows[0], d)),
                P300 = LogDiff(logPcNote(hit_windows[0], d), logPcNote(hit_windows[1], d)),
                P200 = LogDiff(logPcNote(hit_windows[1], d), logPcNote(hit_windows[2], d)),
                P100 = LogDiff(logPcNote(hit_windows[2], d), logPcNote(hit_windows[3], d)),
                P50 = LogDiff(logPcNote(hit_windows[3], d), logPcNote(hit_windows[4], d)),
                P0 = logPcNote(hit_windows[4], d)
            };

            return probabilities;
        }

        // Log Judgement Probabilities of a Legacy Hold given a deviation.
        // This is only used for Legacy Holds, which has a different hit behaviour from Notes and lazer LNs.
        private static JudgementProbs logPHold(double dHead, double dTail)
        {
            JudgementProbs probabilities = new JudgementProbs
            {
                PMax = LogDiff(0, logPcHold(hit_windows[0] * 1.2, dHead, dTail)),
                P300 = LogDiff(logPcHold(hit_windows[0] * 1.2, dHead, dTail), logPcHold(hit_windows[1] * 1.1, dHead, dTail)),
                P200 = LogDiff(logPcHold(hit_windows[1] * 1.1, dHead, dTail), logPcHold(hit_windows[2], dHead, dTail)),
                P100 = LogDiff(logPcHold(hit_windows[2], dHead, dTail), logPcHold(hit_windows[3], dHead, dTail)),
                P50 = LogDiff(logPcHold(hit_windows[3], dHead, dTail), logPcHold(hit_windows[4], dHead, dTail)),
                P0 = logPcHold(hit_windows[4], dHead, dTail)
            };

            return probabilities;
        }

        /// The log complementary probability of getting a certain judgement with a certain deviation on regular notes.
        private static double logPcNote(double window, double deviation) => LogErfc(window / (deviation * Math.Sqrt(2)));

        /// The log complementary probability of getting a certain judgement with a certain deviation on long notes.
        private static double logPcHold(double window, double headDeviation, double tailDeviation)
        {
            double root2 = Math.Sqrt(2);

            double logPcHead = LogErfc(window / (headDeviation * root2));

            // Calculate the expected value of the distance from 0 of the head hit, given it lands within the current window.
            // We'll subtract this from the tail window to approximate the difficulty of landing both hits within 2x the current window.
            double beta = window / headDeviation;
            double z = Normal.CDF(0, 1, beta) - 0.5;
            double expectedValue = headDeviation * (Normal.PDF(0, 1, 0) - Normal.PDF(0, 1, beta)) / z;

            double logPcTail = LogErfc((2 * window - expectedValue) / (tailDeviation * root2));

            return LogDiff(LogSum(logPcHead, logPcTail), logPcHead + logPcTail);
        }

        private static void setHitWindows(Mod[] mods, double overallDifficulty)
        {
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

            hit_windows[0] = Math.Floor(16 * windowMultiplier);
            hit_windows[1] = Math.Floor((64 - 3 * overallDifficulty + greatWindowLeniency) * windowMultiplier);
            hit_windows[2] = Math.Floor((97 - 3 * overallDifficulty + goodWindowLeniency) * windowMultiplier);
            hit_windows[3] = Math.Floor((127 - 3 * overallDifficulty) * windowMultiplier);
            hit_windows[4] = Math.Floor((151 - 3 * overallDifficulty) * windowMultiplier);
        }
    }
}
