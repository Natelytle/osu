// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Utils
{
    /// <summary>
    /// The log of any value x >= 0. Allows incredibly high precision for values close to 0.
    /// </summary>
    public class LogVal
    {
        private double value;

        private bool isNegative;

        public double TrueValue => isNegative ? -Math.Exp(value) : Math.Exp(value);

        public LogVal(double value)
        {
            if (value < 0)
            {
                isNegative = true;
            }

            this.value = Math.Log(Math.Abs(value));
        }

        public static implicit operator LogVal(double value)
        {
            return new LogVal(value);
        }

        public static LogVal Pow(LogVal val, double exponent) => ToLogValNoConversion(val.value * exponent);

        public static LogVal Clamp(LogVal val, LogVal min, LogVal max)
        {
            if ((val - min).isNegative)
                return min;

            if ((max - val).isNegative)
                return max;

            return val;
        }

        public static LogVal operator *(LogVal val1, LogVal val2)
        {
            LogVal product = new LogVal(0);

            if ((val1.isNegative || val2.isNegative) && !(val1.isNegative && val2.isNegative))
                product.isNegative = true;

            product.value = val1.value + val2.value;

            return product;
        }

        public static LogVal operator /(LogVal val1, LogVal val2)
        {
            LogVal quotient = new LogVal(0);

            if ((val1.isNegative || val2.isNegative) && !(val1.isNegative && val2.isNegative))
                quotient.isNegative = true;

            quotient.value = val1.value - val2.value;

            return quotient;
        }

        public static LogVal operator +(LogVal val1, LogVal val2)
        {
            if (val1.isNegative && val2.isNegative)
            {
                LogVal sum = logSum(val1.value, val2.value);

                sum.isNegative = true;

                return sum;
            }

            if (val1.isNegative)
            {
                LogVal sum = logDiff(Math.Max(val1.value, val2.value), Math.Min(val1.value, val2.value));

                if (val2.value < val1.value)
                    sum.isNegative = true;

                return sum;
            }

            if (val2.isNegative)
            {
                LogVal sum = logDiff(Math.Max(val1.value, val2.value), Math.Min(val1.value, val2.value));

                if (val1.value < val2.value)
                    sum.isNegative = true;

                return sum;
            }

            return logSum(val1.value, val2.value);
        }

        public static LogVal operator -(LogVal val1, LogVal val2)
        {
            val2.isNegative = !val2.isNegative;

            return val1 + val2;
        }

        private static LogVal logSum(double firstLog, double secondLog)
        {
            double maxVal = Math.Max(firstLog, secondLog);
            double minVal = Math.Min(firstLog, secondLog);

            // 0 in log form becomes negative infinity, so return negative infinity if both numbers are negative infinity.
            if (double.IsNegativeInfinity(maxVal))
            {
                return ToLogValNoConversion(maxVal);
            }

            return ToLogValNoConversion(maxVal + Math.Log(1 + Math.Exp(minVal - maxVal)));
        }

        private static LogVal logDiff(double firstLog, double secondLog)
        {
            double maxVal = Math.Max(firstLog, secondLog);

            // Avoid negative infinity - negative infinity (NaN) by checking if the higher value is negative infinity.
            if (double.IsNegativeInfinity(maxVal))
            {
                return ToLogValNoConversion(maxVal);
            }

            return ToLogValNoConversion(firstLog + SpecialFunctions.Log1p(-Math.Exp(-(firstLog - secondLog))));
        }

        public static LogVal ToLogValNoConversion(double value)
        {
            LogVal newVal = new LogVal(0)
            {
                value = value
            };

            return newVal;
        }
    }
}
