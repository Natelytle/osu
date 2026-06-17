// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    /// <summary>
    /// Evaluates the cross-hand coefficients that determine how much cross-column difficulty
    /// each column boundary contributes based on the total key count.
    /// These values are tuned based on typical finger layouts and hand coordination.
    /// </summary>
    public static class CrossColumnEvaluator
    {
        // Pre-calculated coefficient matrices for different key counts (1K to 10K).
        private static readonly double[][] cross_matrix =
        {
            new[] { 0.075, 0.075 }, // 1K
            new[] { 0.125, 0.05, 0.125 }, // 2K
            new[] { 0.125, 0.125, 0.125, 0.125 }, // 3K
            new[] { 0.175, 0.25, 0.05, 0.25, 0.175 }, // 4K
            new[] { 0.175, 0.25, 0.175, 0.175, 0.25, 0.175 }, // 5K
            new[] { 0.225, 0.35, 0.25, 0.05, 0.25, 0.35, 0.225 }, // 6K
            new[] { 0.225, 0.35, 0.25, 0.225, 0.225, 0.25, 0.35, 0.225 }, // 7K
            new[] { 0.275, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.275 }, // 8K
            new[] { 0.275, 0.45, 0.35, 0.25, 0.275, 0.275, 0.25, 0.35, 0.45, 0.275 }, // 9K
            new[] { 0.325, 0.55, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.55, 0.325 } // 10K
        };

        private static readonly double[][] coefficientsByKeyCount = buildCoefficientsByKeyCount();

        public static double CoefficientSum(int columnA, int columnB, int totalColumns)
        {
            double[] coefficients = totalColumns >= 1 && totalColumns <= cross_matrix.Length
                ? coefficientsByKeyCount[totalColumns - 1]
                : buildFallback(totalColumns);

            int lowColumn = Math.Min(columnA, columnB);
            int highColumn = Math.Max(columnA, columnB);
            double sum = 0.0;

            for (int boundary = lowColumn + 1; boundary <= highColumn && boundary < coefficients.Length; boundary++)
                sum += coefficients[boundary];

            return sum;
        }

        private static double[][] buildCoefficientsByKeyCount()
        {
            var result = new double[cross_matrix.Length][];

            for (int keyCount = 1; keyCount <= cross_matrix.Length; keyCount++)
            {
                double[] sourceRow = cross_matrix[keyCount - 1];
                double[] padded = new double[keyCount + 1];
                Array.Copy(sourceRow, padded, Math.Min(sourceRow.Length, padded.Length));
                result[keyCount - 1] = padded;
            }

            return result;
        }

        private static double[] buildFallback(int keyCount)
        {
            double[] fallback = new double[keyCount + 1];

            for (int i = 0; i < fallback.Length; i++)
                fallback[i] = 1.0 / fallback.Length;

            return fallback;
        }
    }
}
