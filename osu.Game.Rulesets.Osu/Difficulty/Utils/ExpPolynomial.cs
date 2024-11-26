// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Utils
{
    /// <summary>
    /// Represents an Exponential Polynomial to compress the skill to miss count curve into a few coefficient values.
    /// </summary>
    /// <remarks>
    /// Miss counts in relation to skill are exponential in nature, so we take the natural log of the miss counts which returns a mostly linear set of points.
    /// With this, we can also fit the data with a low degree polynomial with very good accuracy, even if the highest and lowest miss counts are orders of magnitude apart.
    /// </remarks>
    public struct ExpPolynomial
    {
        private double[] coefficients;
        private readonly int degree;

        // The dot product of this matrix with 7 computed points at X values [0.0, 0.30, 0.60, 0.80, 0.90, 0.95, 1.0] results in the least squares fit coefficients.
        private static readonly double[][] quartic_matrix =
        {
            new[] { 0.0, -25.8899, -32.6909, -11.9147, 48.8588, -26.8943, 0.0 },
            new[] { 0.0, 51.7787, 66.595, 28.8517, -90.3185, 40.9864, 0.0 },
            new[] { 0.0, -31.5028, -41.7398, -22.7118, 46.438, -15.5156, 0.0 }
        };

        private static readonly double[][] cubic_matrix =
        {
            new[] { 0.0, 3.14395, 5.18439, 6.46975, 1.4638, -9.53526, 0.0 },
            new[] { 0.0, -4.85829, -8.09612, -10.4498, -3.84479, 12.1626, 0.0 }
        };

        /// <inheritdoc cref="ExpPolynomial"/>
        /// <param name="degree">The degree of the polynomial to fit, either 3 or 4.</param>
        public ExpPolynomial(int degree)
        {
            if (degree != 3 && degree != 4)
                throw new ArgumentOutOfRangeException(nameof(degree));

            this.degree = degree;
            coefficients = new double[degree];
        }

        /// <summary>
        /// Computes a quartic or cubic function that starts at 0 and ends at the highest judgement count in the array.
        /// </summary>
        /// <param name="judgementCounts">A list of judgements, with X values [0.0, 0.05, ..., 0.95, 1.0].</param>
        public void Fit(double[] judgementCounts)
        {
            List<double> logMissCounts = judgementCounts.Select(x => Math.Log(x + 1)).ToList();

            // The polynomial will pass through the point (1, endPoint).
            double endPoint = logMissCounts.Max();

            double[] penalties = { 1, 0.95, 0.9, 0.8, 0.6, 0.3, 0 };

            for (int i = 0; i < logMissCounts.Count; i++)
            {
                logMissCounts[i] -= endPoint * (1 - penalties[i]);
            }

            // The precomputed matrix assumes the misscounts go in order of greatest to least.
            logMissCounts.Reverse();

            double[][] matrix = degree == 4 ? quartic_matrix : cubic_matrix;
            coefficients = new double[degree];

            coefficients[degree - 1] = endPoint;

            // Now we dot product the adjusted misscounts with the precomputed matrix.
            for (int row = 0; row < matrix.Length; row++)
            {
                for (int column = 0; column < matrix[row].Length; column++)
                {
                    coefficients[row] += matrix[row][column] * logMissCounts[column];
                }

                coefficients[degree - 1] -= coefficients[row];
            }
        }

        /// <summary>
        /// Solve for the largest x value of a polynomial within x = 0 and x = 1 at the specified judgementCount value.
        /// </summary>
        /// <param name="judgementCount">The number of judgements to get the multiplier at.</param>
        /// <returns>The x value at the specified judgementCount value, and null if no value exists.</returns>
        public double? GetSkillMultiplier(double judgementCount)
        {
            if (coefficients is null)
                return null;

            List<double> listCoefficients = coefficients.ToList();
            listCoefficients.Add(-Math.Log(judgementCount + 1));

            List<double?> xVals = SpecialFunctions.SolvePolynomialRoots(listCoefficients);

            const double max_error = 1e-7;
            double? largestValue = xVals.Where(x => x >= 0 - max_error && x <= 1 + max_error).OrderDescending().FirstOrDefault();

            return largestValue != null ? Math.Clamp(largestValue.Value, 0, 1) : null;
        }
    }
}
