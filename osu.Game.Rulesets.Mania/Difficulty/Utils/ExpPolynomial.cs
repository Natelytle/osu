// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public struct ExpPolynomial
    {
        private double[]? coefficients;

        // The product of this matrix with 21 computed points at X values [0.0, 0.05, ..., 0.95, 1.0] returns the least squares fit polynomial coefficients.
        private static readonly double[][] matrix =
        {
            new[] { 0.0, -6.99428, -9.87548, -9.76922, -7.66867, -4.43461, -0.795376, 2.65313, 5.4474, 7.2564, 7.88146, 7.2564, 5.4474, 2.65313, -0.795376, -4.43461, -7.66867, -9.76922, -9.87548, -6.99428, 0.0 },
            new[] { 0.0, 13.0907, 18.2388, 17.6639, 13.3211, 6.90022, -0.173479, -6.73969, -11.9029, -15.0326, -15.7629, -13.993, -9.88668, -3.87281, 3.35498, 10.8382, 17.3536, 21.4129, 21.2632, 14.8864, 0.0 },
            new[] { 0.0, -7.21754, -9.85841, -9.24217, -6.5276, -2.71265, 1.36553, 5.03057, 7.76692, 9.21984, 9.19538, 7.66039, 4.74253, 0.730255, -3.92717, -8.61967, -12.5764, -14.8657, -14.395, -9.91114, 0.0 }
        };

        /// <summary>
        /// Computes a quartic or cubic function that starts at 0 and ends at the highest judgement count in the array.
        /// </summary>
        /// <param name="accuracyLosses">A list of how much accuracy was lost at each skill level [0.0, 0.05, ..., 0.95, 1.0].</param>
        public void Fit(double[] accuracyLosses)
        {
            List<double> logAccuracyLosses = accuracyLosses.Select(x => Math.Log(x + 1)).ToList();

            // The polynomial will pass through the point (1, accuracy loss at 0 skill).
            double endPoint = logAccuracyLosses.Max();

            double[] penalties = { 1, 0.95, 0.9, 0.85, 0.8, 0.75, 0.7, 0.65, 0.6, 0.55, 0.5, 0.45, 0.4, 0.35, 0.3, 0.25, 0.2, 0.15, 0.1, 0.05, 0 };

            for (int i = 0; i < logAccuracyLosses.Count; i++)
            {
                logAccuracyLosses[i] -= endPoint * (1 - penalties[i]);
            }

            // The precomputed matrix assumes the accuracy losses go in order of greatest to least.
            logAccuracyLosses.Reverse();

            coefficients = new double[4];

            coefficients[3] = endPoint;

            // Now we dot product the adjusted misscounts with the precomputed matrix.
            for (int row = 0; row < matrix.Length; row++)
            {
                for (int column = 0; column < matrix[row].Length; column++)
                {
                    coefficients[row] += matrix[row][column] * logAccuracyLosses[column];
                }

                coefficients[3] -= coefficients[row];
            }
        }

        /// <summary>
        /// Solve for the miss penalty at a specified miss count.
        /// </summary>
        /// <returns>The penalty value at the specified miss count.</returns>
        public double GetPenaltyAt(double accuracy)
        {
            if (coefficients is null)
                return 1;

            double accuracyLoss = 1 - accuracy;

            List<double> listCoefficients = coefficients.ToList();
            listCoefficients.Add(-Math.Log(accuracyLoss + 1));

            List<double?> xVals = SpecialFunctions.SolvePolynomialRoots(listCoefficients);

            const double max_error = 1e-7;
            double? largestValue = xVals.Where(x => x >= 0 - max_error && x <= 1 + max_error).OrderDescending().FirstOrDefault();

            return largestValue != null ? Math.Clamp(largestValue.Value, 0, 1) : 1;
        }
    }
}
