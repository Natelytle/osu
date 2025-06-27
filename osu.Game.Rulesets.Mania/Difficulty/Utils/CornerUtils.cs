// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class CornerUtils
    {
        public static double[] AverageCornersWithinWindow(double[] cornerTimes, double[] cornerValues, double window)
        {
            int n = cornerValues.Length;
            double[] cumulativeCornerValues = cumulativeSum(cornerTimes, cornerValues);

            double[] averagedValues = new double[n];

            for (int i = 0; i < n; i++)
            {
                double t = cornerTimes[i];
                double a = Math.Max(t - window, cornerTimes[0]);
                double b = Math.Min(t + window, cornerTimes[^1]);

                // Get the sum of the values in the window.
                double val = queryCumulativeSum(b, cornerTimes, cornerValues, cumulativeCornerValues) - queryCumulativeSum(a, cornerTimes, cornerValues, cumulativeCornerValues);

                averagedValues[i] = val / (b - a);
            }

            return averagedValues;
        }

        public static double[] SumCornersWithinWindow(double[] cornerTimes, double[] cornerValues, double window, double scale)
        {
            int n = cornerValues.Length;
            double[] cumulativeCornerValues = cumulativeSum(cornerTimes, cornerValues);

            double[] summedValues = new double[n];

            for (int i = 0; i < n; i++)
            {
                double t = cornerTimes[i];
                double a = Math.Max(t - window, cornerTimes[0]);
                double b = Math.Min(t + window, cornerTimes[^1]);

                // Get the sum of the values in the window.
                double val = queryCumulativeSum(b, cornerTimes, cornerValues, cumulativeCornerValues) - queryCumulativeSum(a, cornerTimes, cornerValues, cumulativeCornerValues);

                summedValues[i] = val * scale;
            }

            return summedValues;
        }

        private static double[] cumulativeSum(double[] cornerTimes, double[] cornerValues)
        {
            int n = cornerTimes.Length;
            double[] cumulativeCornerValues = new double[n];

            cumulativeCornerValues[0] = 0.0;

            for (int i = 1; i < n; i++)
            {
                cumulativeCornerValues[i] = cumulativeCornerValues[i - 1] + cornerValues[i - 1] * (cornerTimes[i] - cornerTimes[i - 1]);
            }

            return cumulativeCornerValues;
        }

        private static double queryCumulativeSum(double queryTime, double[] cornerTimes, double[] cornerValues, double[] cumulativeCornerValues)
        {
            if (queryTime <= cornerTimes[0])
                return 0;
            if (queryTime >= cornerTimes[^1])
                return cumulativeCornerValues[^1];

            int index = Array.BinarySearch(cornerTimes, queryTime);

            if (index < 0)
                index = ~index;
            index -= 1;

            return cumulativeCornerValues[index] + cornerValues[index] * (queryTime - cornerTimes[index]);
        }

        // Linear interpolation from old_x, old_vals to new_x.
        public static double[] InterpolateValues(double[] newX, double[] oldX, double[] oldVals)
        {
            int n = newX.Length;

            double[] newVals = new double[n];

            for (int i = 0; i < n; i++)
            {
                double xVal = newX[i];

                if (xVal <= oldX[0])
                    newVals[i] = oldVals[0];

                else if (xVal >= oldX[^1])
                    newVals[i] = oldVals[^1];

                else
                {
                    int idx = Array.BinarySearch(oldX, xVal);

                    if (idx < 0)
                        idx = ~idx;

                    int j = idx - 1;
                    double t = (xVal - oldX[j]) / (oldX[j + 1] - oldX[j]);
                    newVals[i] = oldVals[j] + t * (oldVals[j + 1] - oldVals[j]);
                }
            }

            return newVals;
        }
    }
}
