// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class CrossColumnEvaluatorTest
    {
        public static double EvaluateSpeedDifficultyOf(ManiaDifficultyHitObject current)
        {
            ManiaDifficultyHitObject? prev = current.PrevHeadInColumn(0);

            // Evaluated as null if column is out of bounds
            ManiaDifficultyHitObject? leftPrev = current.PrevHeadInColumn(0, current.Column - 1);
            ManiaDifficultyHitObject? rightPrev = current.PrevHeadInColumn(0, current.Column + 1);

            double? leftDelta = current.StartTime - maxOrNull(leftPrev?.StartTime, prev?.StartTime);
            double? rightDelta = current.StartTime - maxOrNull(rightPrev?.StartTime, prev?.StartTime);

            double speedDifficultyLeft = leftDelta is not null ? evaluateSingleColumnSpeed(current, leftDelta.Value, current.Column - 1) : 0.0;
            double speedDifficultyRight = rightDelta is not null ? evaluateSingleColumnSpeed(current, rightDelta.Value, current.Column + 1) : 0.0;

            return Math.Sqrt(speedDifficultyLeft * speedDifficultyRight);
        }

        private static double evaluateSingleColumnSpeed(ManiaDifficultyHitObject current, double delta, int otherColumn)
        {
            double adjustedDelta = Math.Max(delta, 60);
            adjustedDelta = Math.Max(adjustedDelta, 0.75 * ManiaDifficultyUtils.CalculateHitLeniency(current.GreatHitWindow));

            double difficulty = Math.Max(0, 0.4 * Math.Pow(1000.0 / adjustedDelta, 2) - 80.0);

            int columnCount = current.PreviousHeadObjects.Length;
            int boundaryColumnIndex = Math.Min(current.Column, otherColumn) + 1;

            double crossHandCoefficient = getCrossHandCoefficient(columnCount, boundaryColumnIndex);

            return difficulty * crossHandCoefficient;
        }

        public static (double left, double right) EvaluateCrossDifficultiesOf(ManiaDifficultyHitObject current)
        {
            ManiaDifficultyHitObject? prev = current.PrevHeadInColumn(0);

            // Evaluated as null if column is out of bounds
            ManiaDifficultyHitObject? leftPrev = current.PrevHeadInColumn(0, current.Column - 1);
            ManiaDifficultyHitObject? rightPrev = current.PrevHeadInColumn(0, current.Column + 1);

            double? leftDelta = current.StartTime - maxOrNull(leftPrev?.StartTime, prev?.StartTime);
            double? rightDelta = current.StartTime - maxOrNull(rightPrev?.StartTime, prev?.StartTime);

            double crossDifficultyLeft = leftDelta is not null ? evaluateSingleColumnCross(current, leftDelta.Value, current.Column - 1) : 0.0;
            double crossDifficultyRight = rightDelta is not null ? evaluateSingleColumnCross(current, rightDelta.Value, current.Column + 1) : 0.0;

            return (crossDifficultyLeft, crossDifficultyRight);
        }

        private static double evaluateSingleColumnCross(ManiaDifficultyHitObject current, double delta, int otherColumn)
        {
            double adjustedDelta = Math.Max(delta, ManiaDifficultyUtils.CalculateHitLeniency(current.GreatHitWindow));

            double difficulty = 0.16 * Math.Pow(1000.0 / adjustedDelta, 2);

            double usage = getKeyUsageFor(current, delta, otherColumn);

            int columnCount = current.PreviousHeadObjects.Length;
            int boundaryColumnIndex = Math.Min(current.Column, otherColumn) + 1;

            double crossHandCoefficient = getCrossHandCoefficient(columnCount, boundaryColumnIndex);

            // Nerf for low usage of the other column
            difficulty -= difficulty * crossHandCoefficient * (1.0 - usage);

            difficulty *= crossHandCoefficient;

            return difficulty;
        }

        // Get key usage in the previous or next 150ms as a value between zero and one.
        private static double getKeyUsageFor(ManiaDifficultyHitObject current, double delta, int column)
        {
            double usage = 0;

            var next = current.NextHeadInColumn(0, column, true);
            usage = next is not null ? Math.Max(usage, DifficultyCalculationUtils.Smoothstep(next.StartTime - current.StartTime, 175, 125)) : usage;

            var prev = current.PrevHeadInColumn(0, column);
            usage = prev is not null ? Math.Max(usage, DifficultyCalculationUtils.Smoothstep(current.StartTime - (prev.StartTime + delta), 175, 125)) : usage;

            return usage;
        }

        /// <summary>
        /// Gets the cross-hand coefficients that determine how much cross-column difficulty
        /// each column boundary contributes based on the total key count.
        /// These values are tuned based on typical finger layouts and hand coordination.
        /// </summary>
        private static double getCrossHandCoefficient(int keyCount, int columnBoundaryIndex)
        {
            // Pre-calculated coefficient matrices for different key counts (1K to 10K)
            double[][] crossMatrix =
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

            if (keyCount >= 1 && keyCount <= 10)
            {
                return crossMatrix[keyCount - 1][columnBoundaryIndex];
            }

            // Fallback for unsupported key counts - they're pretty hard, so we use 0.4 for every potential binding
            return 0.4;
        }

        private static double? maxOrNull(double? first, double? second) => first > second ? first : second ?? first;
    }
}
