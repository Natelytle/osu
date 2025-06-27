// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class CornerUtils
    {
        // Smooths values within the provided window, either by averaging the values or summing them and multiplying them by scale.
        public static double[] SmoothCornersWithinWindow(double[] cornerTimes, double[] cornerValues, double window, double scale, bool sum = true)
        {
            int n = cornerValues.Length;

            if (n == 0)
            {
                return Array.Empty<double>();
            }

            double[] cumulativeCornerValues = cumulativeSum(cornerTimes, cornerValues);
            double[] averagedValues = new double[n];

            double firstCornerTime = cornerTimes[0];
            double lastCornerTime = cornerTimes[n - 1];
            double lastCumulativeValue = cumulativeCornerValues[n - 1];

            int idxA = 0;
            int idxB = 0;

            for (int i = 0; i < n; i++)
            {
                double t = cornerTimes[i];
                double a = Math.Max(t - window, firstCornerTime);
                double b = Math.Min(t + window, lastCornerTime);

                double valA;

                if (a <= firstCornerTime)
                {
                    valA = 0.0;
                }
                else
                {
                    while (idxA < n - 1 && cornerTimes[idxA + 1] <= a)
                    {
                        idxA++;
                    }

                    valA = cumulativeCornerValues[idxA] + cornerValues[idxA] * (a - cornerTimes[idxA]);
                }

                double valB;

                if (b >= lastCornerTime)
                {
                    valB = lastCumulativeValue;
                }
                else
                {
                    while (idxB < n - 1 && cornerTimes[idxB + 1] <= b)
                    {
                        idxB++;
                    }

                    valB = cumulativeCornerValues[idxB] + cornerValues[idxB] * (b - cornerTimes[idxB]);
                }

                double val = valB - valA;

                if (sum)
                    averagedValues[i] = val * scale;
                else
                    averagedValues[i] = val / (b - a);
            }

            return averagedValues;
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
