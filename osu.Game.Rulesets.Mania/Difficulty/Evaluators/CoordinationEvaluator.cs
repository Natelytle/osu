// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class CoordinationEvaluator
    {
        /// <summary>
        /// Evaluates the difficulty of placing the current note relative to everything the other fingers are
        /// doing, based on:
        /// <list type="bullet">
        /// <item><description>how recently each neighbouring column was pressed,</description></item>
        /// <item><description>how wide the chord it sits in is,</description></item>
        /// <item><description>and how many long notes are being held when it is hit.</description></item>
        /// </list>
        /// </summary>

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            // Total combines the tap skills in quadrature, so this evaluator carries the square root of its weight.
            const double total_weight = 1.81659; // sqrt(3.3)

            double coordinationDifficulty = calculateBoundaryPressure(current);

            double columnDelta = current.ColumnDelta;
            int depthInChord = ChordUtils.DepthInChord(current);

            coordinationDifficulty += calculateChordDifficulty(current, depthInChord, columnDelta);
            coordinationDifficulty += calculateShieldDifficulty(current);

            coordinationDifficulty *= current.ManipulationFactor * current.EnduranceFactor;

            return saturate(coordinationDifficulty * total_weight);
        }

        /// <summary>
        /// Two hands only have so many fingers, so past a point more columns being live at once stops adding
        /// difficulty as fast. Bends the top of the range over without ever capping it outright.
        /// </summary>
        private static double saturate(double strain)
        {
            const double threshold = 13.0;
            const double strength = 0.75;
            const double width = 1.5;

            // A softplus of the excess above the threshold.
            // See https://www.desmos.com/calculator/jgnbehwngr
            double z = (strain - threshold) / width;
            double softExcess = width * (Math.Max(z, 0.0) + Math.Log(1.0 + Math.Exp(-Math.Abs(z))));

            return strain - strength * softExcess;
        }

        /// <summary>
        /// Calculates the difficulty of both column boundaries for this column, with a boundary being the hypothetical "middle" of two columns.
        /// </summary>
        private static double calculateBoundaryPressure(ManiaDifficultyHitObject current)
        {
            const double boundary_pressure_weight = 1.07;

            int column = current.Column;
            int totalColumns = current.Row.TotalColumns;
            double total = 0.0;

            if (column > 0)
                total += columnBoundaryPressure(current, column, left: true, totalColumns);

            if (column < totalColumns - 1)
                total += columnBoundaryPressure(current, column, left: false, totalColumns);

            return total * TrillUtils.TrillFactor(current) * boundary_pressure_weight;
        }

        /// <summary>
        /// How much pressure the column on one side of <paramref name="current"/> is putting on the hand, from how
        /// recently it was last pressed.
        /// </summary>
        private static double columnBoundaryPressure(ManiaDifficultyHitObject current, int column, bool left, int totalColumns)
        {
            const double scale_ms = 1300.0;
            const double min_delta_ms = 35.0;

            // Past this the neighbouring column has had time to be forgotten about.
            const double activity_window_ms = 450.0;

            int adjacentColumn = left ? column - 1 : column + 1;
            if (Array.IndexOf(current.Row.Columns, adjacentColumn) >= 0)
                return 0.0;

            double adjacentStartTime = current.LastStartTimeInColumn(adjacentColumn);
            if (double.IsNegativeInfinity(adjacentStartTime))
                return 0.0;

            double adjacentDelta = current.StartTime - adjacentStartTime;
            if (adjacentDelta < ChordUtils.CHORD_TOLERANCE_MS)
                return 0.0;

            // Boundaries sit between columns, so the left side boundary shares this column's index.
            int boundaryIndex = left ? column : column + 1;

            // capping intensity to nerf graces
            double intensity = Math.Min(scale_ms / (adjacentDelta + min_delta_ms), 20.0);
            double coefficient = CrossColumnUtils.ColumnBoundaryMultiplier(boundaryIndex, totalColumns);
            bool otherActive = adjacentDelta <= activity_window_ms;

            return intensity * coefficient * (otherActive ? 1.0 : (1.0 - coefficient)) * previousRowMultiplier(current, adjacentDelta);
        }

        /// <summary>
        /// How far back this column itself was last pressed, compared to how far back the neighbour was.
        /// A neighbour that is fresher than this column means the finger landing here is the one that has
        /// been resting, so the placement costs the hand less.
        /// </summary>
        private static double previousRowMultiplier(ManiaDifficultyHitObject current, double adjacentDelta)
        {
            const double min_share = 0.3;

            // How long this column has been idle, in comparison to the adjacent one.
            double ratio = current.ColumnDelta / adjacentDelta;

            return 1.0 - DiffUtils.Smoothstep(ratio, 2.0, 6.0) * (1 - min_share);
        }

        /// <summary>
        /// What the extra columns of a chord cost to place. Notes past the first come for free with the press
        /// itself, so this only pays for the shape being wider than one finger.
        /// </summary>
        private static double calculateChordDifficulty(ManiaDifficultyHitObject current, int depthInChord, double columnDelta)
        {
            const double load_per_extra_column = 1.5;
            const double shapeBonusWeight = 2.5;

            if (depthInChord < 2)
                return 0.0;

            // Chordjacks are already paid for by Jack, so the dampening here is only for a chord that is
            // actually coming back to itself. A chord played soon after a different one is two separate
            // placements, not one repeated, and keeps the full value.
            bool isChordjack = current.Row.Previous() is { } previousShape
                               && ColumnPatternUtils.SameColumns(previousShape.Columns, current.Row.Columns);

            double difficulty = load_per_extra_column * (depthInChord - 1) * ChordUtils.ChordRepeatNerf(current, columnDelta)
                                * (isChordjack ? ChordUtils.CHORDJACK_NERF : 1.0) * ChordUtils.ChordSpeedFactor(columnDelta)
                                + shapeBonusWeight * calculateShapeBonus(current, columnDelta);

            // The same shape is a movement the hand is already doing rather than new fingers to
            // place, so it of course requires less coordination overall.
            if (isChordjack)
                difficulty *= 0.5;

            return difficulty;
        }

        /// <summary>
        /// How much the hand has to re-place itself because the shape around the repeat changed. A shape that either repeats or shares
        /// no column with the row earns nothing.
        /// </summary>
        private static double calculateShapeBonus(ManiaDifficultyHitObject current, double columnDelta)
        {
            if (current.Row.Previous() is not { } previous
                || previous.Size < 1
                || !ColumnPatternUtils.SharesColumn(previous.Columns, current.Row.Columns))
                return 0.0;

            double shapeBonus = DiffUtils.Smoothstep(ColumnPatternUtils.ChordDifference(previous.Columns, current.Row.Columns), 0.3, 0.75);

            // Each repeated column asks less movement from the fingers, so they get progressively reduced
            int shared = ColumnPatternUtils.SharedColumnCount(previous.Columns, current.Row.Columns);

            if (shared > 1)
            {
                double keymode = Math.Min(current.Row.TotalColumns, 9);
                shapeBonus *= Math.Pow(0.61 + keymode * 0.04, shared);
            }

            // Slow transitions give the hand time to re-place.
            return shapeBonus * (1.0 - DiffUtils.Smoothstep(columnDelta, 100.0, 200.0));
        }

        /// <summary>
        /// A long note with a jack previously is a shield. If the long note needs to be held for an extended period of time,
        /// the hand has to fight against bouncing the finger up from the jack.
        /// </summary>
        private static double calculateShieldDifficulty(ManiaDifficultyHitObject current)
        {
            const double shield_weight = 0.75;

            // The same intensity shape the boundary pressure uses.
            const double scale_ms = 1300.0;
            const double min_delta_ms = 35.0;

            const double min_shield_ms = 250.0;
            const double full_shield_ms = 800.0;

            // Only a note that is actually being held can be a shield.
            double holdLength = current.EndTime - current.StartTime;

            if (holdLength <= 0.0)
                return 0.0;

            if (current.PrevInColumn(0) is null)
                return 0.0;

            // The tighter the jack, the harder it is for the hand to hold current note.
            double intensity = Math.Min(scale_ms / (current.DeltaTime + min_delta_ms), 20.0);

            return shield_weight * intensity * DiffUtils.ReverseLerp(holdLength, min_shield_ms, full_shield_ms);
        }
    }
}
