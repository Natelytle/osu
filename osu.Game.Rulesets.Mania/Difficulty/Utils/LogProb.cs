// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MathNet.Numerics;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class LogProb
    {
        private double value;

        public double Probability => Math.Exp(value);

        public LogProb(double prob)
        {
            if (prob is < 0 or > 1)
                throw new ArgumentOutOfRangeException();

            value = Math.Log(prob);
        }

        public static implicit operator LogProb(double value)
        {
            return new LogProb(0)
            {
                value = value
            };
        }

        public static LogProb Pow(LogProb val1, double exponent) => val1.value * exponent;

        public static LogProb operator *(LogProb val1, LogProb val2) => val1.value + val2.value;

        public static LogProb operator /(LogProb val1, LogProb val2) => val1.value - val2.value;

        public static LogProb operator +(LogProb val1, LogProb val2) => logSum(val1.value, val2.value);

        public static LogProb operator -(LogProb val1, LogProb val2) => logDiff(val1.value, val2.value);

        private static double logSum(double firstLog, double secondLog)
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

        private static double logDiff(double firstLog, double secondLog)
        {
            double maxVal = Math.Max(firstLog, secondLog);

            // Avoid negative infinity - negative infinity (NaN) by checking if the higher value is negative infinity.
            if (double.IsNegativeInfinity(maxVal))
            {
                return maxVal;
            }

            return firstLog + SpecialFunctions.Log1p(-Math.Exp(-(firstLog - secondLog)));
        }
    }
}
