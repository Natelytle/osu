// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using MathNet.Numerics;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Difficulty.UnstableRateEstimator
{
    public abstract class UnstableRateEstimator
    {
        protected readonly IRulesetInfo Ruleset;
        protected readonly DifficultyAttributes Attributes;

        protected double CountPerfect;
        protected double CountGreat;
        protected double CountGood;
        protected double CountOk;
        protected double CountMeh;
        protected double CountMiss;

        protected double TotalHits => CountPerfect + CountGreat + CountGood + CountOk + CountMeh + CountMiss;
        protected double TotalSuccessfulHits => CountPerfect + CountGreat + CountGood + CountOk + CountMeh;

        protected UnstableRateEstimator(IRulesetInfo ruleset, DifficultyAttributes attributes)
        {
            Ruleset = ruleset;
            Attributes = attributes;
        }

        /// <summary>
        /// Estimates the unstable rate of the score, assuming the average hit location is in the center of the hit window.
        /// </summary>
        /// <param name="score">The <see cref="ScoreInfo"/> of the score in which UR is estimated from.</param>
        /// <param name="withMisses">Whether the estimation should include misses. Only applies to Mania and Taiko.</param>
        /// <returns>Estimated UR, or null if the score is a miss-only score.</returns>
        public abstract double? ComputeEstimatedUnstableRate(ScoreInfo score, bool withMisses = true);

        protected void SetStatistics(ScoreInfo score)
        {
            CountPerfect = score.Statistics.GetValueOrDefault(HitResult.Perfect);
            CountGreat = score.Statistics.GetValueOrDefault(HitResult.Great);
            CountGood = score.Statistics.GetValueOrDefault(HitResult.Good);
            CountOk = score.Statistics.GetValueOrDefault(HitResult.Ok);
            CountMeh = score.Statistics.GetValueOrDefault(HitResult.Meh);
            CountMiss = score.Statistics.GetValueOrDefault(HitResult.Miss);
        }

        protected double LogComplementProbOfHit(double x, double deviation) => LogErfc(x / (deviation * Math.Sqrt(2)));

        protected static double LogErfc(double x) => x <= 5
            ? Math.Log(SpecialFunctions.Erfc(x))
            : -Math.Pow(x, 2) - Math.Log(x * Math.Sqrt(Math.PI));

        protected static double LogSum(double firstLog, double secondLog)
        {
            double maxVal = Math.Max(firstLog, secondLog);
            double minVal = Math.Min(firstLog, secondLog);

            // 0 in log form becomes negative infinity, so return negative infinity if both numbers are negative infinity.
            if (double.IsNegativeInfinity(maxVal))
            {
                return maxVal;
            }

            return maxVal + Math.Log(1 + Math.Exp(minVal - maxVal));
        }

        protected static double LogDiff(double firstLog, double secondLog)
        {
            double maxVal = Math.Max(firstLog, secondLog);

            // To avoid a NaN result, a check is performed to prevent subtraction of two negative infinity values.
            if (double.IsNegativeInfinity(maxVal))
            {
                return maxVal;
            }

            return firstLog + SpecialFunctions.Log1p(-Math.Exp(-(firstLog - secondLog)));
        }
    }
}
