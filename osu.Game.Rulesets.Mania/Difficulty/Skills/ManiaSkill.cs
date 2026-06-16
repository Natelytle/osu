// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public abstract class ManiaSkill : StrainDecaySkill
    {
        protected const double CHORD_TOLERANCE = 8.0;
        protected const double JACK_WINDOW_MS = 350.0;

        private const double trill_nerf = 0.62864;
        private const double trill_run_ramp = 4.99947;

        protected readonly int TotalColumns;

        private readonly double[] crossColumnCoefficients;

        private readonly double[] lastStartTimeInColumn;
        private readonly double[] lastEndTimeInColumn;

        protected ManiaSkill(Mod[] mods, int totalColumns)
            : base(mods)
        {
            TotalColumns = totalColumns;

            lastStartTimeInColumn = new double[totalColumns];
            lastEndTimeInColumn = new double[totalColumns];

            for (int column = 0; column < totalColumns; column++)
            {
                lastStartTimeInColumn[column] = double.NegativeInfinity;
                lastEndTimeInColumn[column] = double.NegativeInfinity;
            }

            crossColumnCoefficients = buildCrossColumnCoefficients(totalColumns);
        }

        protected override double SkillMultiplier => 1.0;

        protected double CrossColumnCoefficientSum(int columnA, int columnB)
        {
            int lowColumn = Math.Min(columnA, columnB);
            int highColumn = Math.Max(columnA, columnB);
            double sum = 0.0;

            for (int boundary = lowColumn + 1; boundary <= highColumn && boundary < crossColumnCoefficients.Length; boundary++)
                sum += crossColumnCoefficients[boundary];

            return sum;
        }

        protected static int ChordSize(DifficultyHitObject current)
        {
            int chordSize = 1;

            for (int i = 0; current.Previous(i) is { } previous && Math.Abs(previous.StartTime - current.StartTime) <= CHORD_TOLERANCE; i++)
                chordSize++;

            return chordSize;
        }

        protected int ConcurrentlyHeldColumns(int column, double time)
        {
            int heldColumns = 0;

            for (int otherColumn = 0; otherColumn < TotalColumns; otherColumn++)
            {
                if (otherColumn == column)
                    continue;

                if (Math.Abs(lastStartTimeInColumn[otherColumn] - time) <= CHORD_TOLERANCE)
                    continue;

                if (lastEndTimeInColumn[otherColumn] > time + CHORD_TOLERANCE)
                    heldColumns++;
            }

            return heldColumns;
        }

        protected double LastStartTimeInColumn(int column) => lastStartTimeInColumn[column];

        protected double LastEndTimeInColumn(int column) => lastEndTimeInColumn[column];

        protected void UpdateColumnState(ManiaDifficultyHitObject hitObject)
        {
            lastStartTimeInColumn[hitObject.Column] = hitObject.StartTime;
            lastEndTimeInColumn[hitObject.Column] = hitObject.EndTime;
        }

        protected bool IsTrillStep(ManiaDifficultyHitObject hitObject)
        {
            if (hitObject.Previous(0) is not ManiaDifficultyHitObject previous ||
                hitObject.Previous(1) is not ManiaDifficultyHitObject previous2)
                return false;

            return previous.Column != hitObject.Column && previous2.Column == hitObject.Column;
        }

        protected double TrillFactor(ManiaDifficultyHitObject hitObject)
        {
            if (!IsTrillStep(hitObject))
                return 1.0;

            double ramp = Math.Max(1.0, trill_run_ramp);
            int cap = (int)Math.Ceiling(ramp) + 1;
            int run = 1;
            var current = hitObject;

            while (run < cap)
            {
                if (current.Previous(0) is not ManiaDifficultyHitObject previousNote || !IsTrillStep(previousNote))
                    break;

                run++;
                current = previousNote;
            }

            double t = Math.Min(1.0, (run - 1) / ramp);
            return 1.0 - (1.0 - trill_nerf) * t;
        }

        /// <summary>
        /// Gets the cross-hand coefficients that determine how much cross-column difficulty
        /// each column boundary contributes based on the total key count.
        /// These values are tuned based on typical finger layouts and hand coordination.
        /// </summary>
        private static double[] buildCrossColumnCoefficients(int keyCount)
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
                double[] sourceRow = crossMatrix[keyCount - 1];
                double[] result = new double[keyCount + 1];
                Array.Clear(result, 0, result.Length);
                Array.Copy(sourceRow, result, Math.Min(sourceRow.Length, result.Length));
                return result;
            }

            double[] fallback = new double[keyCount + 1];
            for (int i = 0; i < fallback.Length; i++)
                fallback[i] = 1.0 / fallback.Length;
            return fallback;
        }
    }
}
