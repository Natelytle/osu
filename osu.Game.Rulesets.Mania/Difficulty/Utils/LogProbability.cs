// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class LogProbability
    {
        private double value;

        public double Probability => Math.Exp(value);

        public LogProbability(double prob)
        {
            if (prob is < 0 or > 1)
                throw new ArgumentOutOfRangeException();

            value = Math.Log(prob);
        }

        public static implicit operator LogProbability(double value)
        {
            return new LogProbability(0)
            {
                value = value
            };
        }

        public static LogProbability Pow(LogProbability val1, double exponent) => val1.value * exponent;

        public static LogProbability Combine(LogProbability prob1, LogProbability prob2, double count1, double count2) =>
            logSum(prob1.value + Math.Log(count1), prob2.value + Math.Log(count2)) - Math.Log(count1 + count2);

        public static LogProbability operator *(LogProbability val1, LogProbability val2) => val1.value + val2.value;

        public static LogProbability operator +(LogProbability val1, LogProbability val2) => logSum(val1.value, val2.value);

        public static LogProbability operator -(LogProbability val1, LogProbability val2) => logDiff(val1.value, val2.value);

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

            return firstLog + log1P(-Math.Exp(-(firstLog - secondLog)));
        }

        /// <summary>
        /// Computes ln(1+x) with good relative precision when |x| is small
        /// </summary>
        /// <param name="x">The parameter for which to compute the log1p function. Range: x > 0.</param>
        private static double log1P(double x)
        {
            double y0 = Math.Log(1.0 + x);

            if ((-0.2928 < x) && (x < 0.4142))
            {
                double y = y0;

                if (y == 0.0)
                {
                    y = 1.0;
                }
                else if ((y < -0.69) || (y > 0.4))
                {
                    y = (Math.Exp(y) - 1.0) / y;
                }
                else
                {
                    double t = y / 2.0;
                    y = Math.Exp(t) * Math.Sinh(t) / t;
                }

                double s = y0 * y;
                double r = (s - x) / (s + 1.0);
                y0 = y0 - r * (6 - r) / (6 - 4 * r);
            }

            return y0;
        }
    }
}
