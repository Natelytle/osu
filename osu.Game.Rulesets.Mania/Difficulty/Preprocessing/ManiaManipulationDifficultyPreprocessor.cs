// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing
{
    public static class ManiaManipulationDifficultyPreprocessor
    {
        private const double high_speed_nerf = 0.72;
        private const double period_ramp = 26.0;
        private const int run_cap = 90;
        private const int max_period = 4;
        private const int max_shift = 1;
        private const double speed_hi_ms = 82.0;
        private const double speed_lo_ms = 30.0;

        private const double movement_fast_ms = 60.0;
        private const int movement_cap = 400;
        private const double movement_taper_lo = 70.0;
        private const double movement_taper_hi = 160.0;
        private const double movement_stamina_relief = 0.9;

        private const double movement_dir_lo = 0.42;
        private const double movement_dir_hi = 0.50;

        private const int movement_chord_window = 14;
        private const double movement_chord_lo = 0.14;
        private const double movement_chord_hi = 0.34;

        private const double mash_nerf = 1.0;
        private const double mash_speed_hi_ms = 50.0;
        private const double mash_speed_lo_ms = 33.0;
        private const double mash_ramp_lo = 3.0;
        private const double mash_ramp_hi = 9.0;
        private const int mash_run_cap = 64;
        private const double mash_chord_lo = 0.06;
        private const double mash_chord_hi = 0.18;
        private const double mash_cross_lo = 0.10;
        private const double mash_cross_hi = 0.22;

        private const double jumptrill_nerf = 0.88;
        private const double jumptrill_ramp = 2.5;
        private const double jumptrill_speed_hi_ms = 140.0;
        private const double jumptrill_speed_lo_ms = 38.0;

        private const double stamina_buff = 0.52;
        private const double stamina_speed_hi_ms = 85.0;
        private const double stamina_speed_lo_ms = 62.0;
        private const double stamina_speed_vfast_hi_ms = 62.0;
        private const double stamina_speed_vfast_lo_ms = 48.0;
        private const double stamina_vfast_taper = 0.72;
        private const double stamina_run_lo = 6.0;
        private const double stamina_run_hi = 30.0;
        private const int stamina_run_cap = 256;

        /// <summary>
        /// Groups objects into rows, assigning a manipulation factor to the notes in each row based on how much manipulation affects the difficulty of the note.
        /// </summary>
        /// <param name="mapData">The structured data of the map used to calculate manipulation attributes for the notes.</param>
        /// <param name="totalColumns">The number of columns of the beatmap, used to split rows into left/right hands.</param>
        public static void ProcessAndAssign(ManiaMapData mapData, int totalColumns)
        {
            if (mapData.Rows.Count == 0)
                return;

            var rows = mapData.Rows;

            bool[] handLocal = new bool[rows.Count];
            for (int i = 0; i < rows.Count; i++)
                handLocal[i] = isHandLocal(rows[i].Columns, totalColumns);

            for (int i = 0; i < rows.Count; i++)
            {
                double timeSincePreviousRow = i > 0 ? rows[i].StartTime - rows[i - 1].StartTime : double.PositiveInfinity;

                double manipulationFactor = Math.Min(
                    Math.Min(
                        rollAndPatternFactor(rows, i, timeSincePreviousRow),
                        jumptrillFactor(rows, i, timeSincePreviousRow)),
                    mashFactor(rows, handLocal, i, timeSincePreviousRow));

                double staminaFactor = staminaFactorFor(rows, i, timeSincePreviousRow);

                foreach (var member in rows[i].Objects)
                {
                    if (manipulationFactor < 1.0)
                        member.ManipulationFactor = manipulationFactor;

                    if (staminaFactor > 1.0)
                        member.StaminaFactor = staminaFactor;
                }
            }
        }

        /// <summary>
        /// Counts how many consecutive rows immediately before <paramref name="row"/> satisfy <paramref name="condition"/>,
        /// stopping early at <paramref name="cap"/>. <paramref name="condition"/> receives the index of the earlier of the
        /// two rows being compared on each step (i.e. it is called once per adjacent row pair, walking backward).
        /// </summary>
        private static int countRunBackward(int row, int cap, Func<int, bool> condition)
        {
            int run = 0;

            while (run < cap && row - 1 >= 0 && condition(row - 1))
            {
                run++;
                row--;
            }

            return run;
        }

        private static double rollAndPatternFactor(IReadOnlyList<ManiaRow> rows, int row, double timeSincePreviousRow)
        {
            double speedScale = DiffUtils.Smoothstep(speed_hi_ms - timeSincePreviousRow, 0.0, speed_hi_ms - speed_lo_ms);

            if (speedScale <= 0.0)
                return 1.0;

            int patternRun = longestRollOrPeriodicRun(rows, row);
            double runWeight = DiffUtils.ReverseLerp(patternRun, 0.0, period_ramp);

            int moveRun = movementRun(rows, row, out double directionConsistency);

            if (moveRun >= 2)
            {
                double staminaRelief = movement_stamina_relief * DiffUtils.Smoothstep(moveRun, movement_taper_lo, movement_taper_hi);
                double rollGate = DiffUtils.Smoothstep(directionConsistency, movement_dir_lo, movement_dir_hi);
                double chordGate = 1.0 - DiffUtils.Smoothstep(localChordDensity(rows, row), movement_chord_lo, movement_chord_hi);

                double moveWeight = DiffUtils.ReverseLerp(moveRun, 0.0, period_ramp) * (1.0 - staminaRelief) * rollGate * chordGate;
                runWeight = Math.Max(runWeight, moveWeight);
            }

            if (runWeight <= 0.0)
                return 1.0;

            return 1.0 - high_speed_nerf * runWeight * speedScale;
        }

        private static int longestRollOrPeriodicRun(IReadOnlyList<ManiaRow> rows, int row)
        {
            // "Roll": each row shifted by the same constant column offset from the previous row (e.g. 1,2,3,4 repeating with a +1 shift).
            int run = countRunBackward(row, run_cap, earlier => columnShift(rows[earlier].Columns, rows[earlier + 1].Columns) != 0);

            // Periodic jacks/patterns: row N repeats row N-period, for small periods.
            for (int period = 2; period <= max_period; period++)
                run = Math.Max(run, periodRunLength(rows, row, period));

            return run;
        }

        private static int periodRunLength(IReadOnlyList<ManiaRow> rows, int row, int period)
        {
            int run = 0;

            while (run < run_cap && row - period >= 0 && sameColumns(rows[row].Columns, rows[row - period].Columns))
            {
                run++;
                row -= period;
            }

            return run;
        }

        private static double jumptrillFactor(IReadOnlyList<ManiaRow> rows, int row, double timeSincePreviousRow)
        {
            if (!rows[row].IsJump)
                return 1.0;

            double speedScale = DiffUtils.Smoothstep(jumptrill_speed_hi_ms - timeSincePreviousRow, 0.0, jumptrill_speed_hi_ms - jumptrill_speed_lo_ms);

            if (speedScale <= 0.0)
                return 1.0;

            int run = 0;

            for (int k = row;
                 k - 2 >= 0
                 && rows[k].IsJump
                 && sameColumns(rows[k].Columns, rows[k - 2].Columns)
                 && !sameColumns(rows[k].Columns, rows[k - 1].Columns);
                 k--)
            {
                run++;
            }

            if (run == 0)
                return 1.0;

            double runWeight = DiffUtils.ReverseLerp(run, 0.0, jumptrill_ramp);
            return 1.0 - jumptrill_nerf * runWeight * speedScale;
        }

        private static double mashFactor(IReadOnlyList<ManiaRow> rows, bool[] handLocal, int row, double timeSincePreviousRow)
        {
            if (!handLocal[row])
                return 1.0;

            double speedScale = DiffUtils.Smoothstep(mash_speed_hi_ms - timeSincePreviousRow, 0.0, mash_speed_hi_ms - mash_speed_lo_ms);

            if (speedScale <= 0.0)
                return 1.0;

            int run = 0;

            for (int k = row; run < mash_run_cap && k - 1 >= 0 && handLocal[k - 1] && rows[k].StartTime - rows[k - 1].StartTime <= mash_speed_hi_ms; k--)
                run++;

            double runWeight = DiffUtils.Smoothstep(run, mash_ramp_lo, mash_ramp_hi);

            if (runWeight <= 0.0)
                return 1.0;

            double chordGate = DiffUtils.Smoothstep(localChordDensity(rows, row), mash_chord_lo, mash_chord_hi);
            double crossGate = 1.0 - DiffUtils.Smoothstep(localCrossHandDensity(handLocal, row), mash_cross_lo, mash_cross_hi);

            return 1.0 - mash_nerf * runWeight * speedScale * chordGate * crossGate;
        }

        private static bool isHandLocal(int[] columns, int totalColumns)
        {
            bool hasLeft = false;
            bool hasRight = false;

            foreach (int c in columns)
            {
                if (c < totalColumns / 2)
                    hasLeft = true;
                if (c >= (totalColumns + 1) / 2)
                    hasRight = true;
            }

            return !(hasLeft && hasRight);
        }

        private static double localCrossHandDensity(bool[] handLocal, int row)
        {
            int lo = Math.Max(0, row - movement_chord_window);
            int hi = Math.Min(handLocal.Length - 1, row + movement_chord_window);

            int crossCount = 0;

            for (int r = lo; r <= hi; r++)
            {
                if (!handLocal[r])
                    crossCount++;
            }

            return (double)crossCount / (hi - lo + 1);
        }

        private static int movementRun(IReadOnlyList<ManiaRow> rows, int row, out double directionConsistency)
        {
            directionConsistency = 0.0;

            if (!rows[row].IsSingleNote)
                return 0;

            int runStart = row;
            while (row - runStart < movement_cap && isFastLateralMove(rows, runStart))
                runStart--;

            int runEnd = row;
            while (runEnd - row < movement_cap && isFastLateralMove(rows, runEnd + 1))
                runEnd++;

            int dirPairs = 0;
            int sameDirCount = 0;
            int previousDirection = 0;

            for (int k = runStart + 1; k <= runEnd; k++)
            {
                int direction = Math.Sign(rows[k].Columns[0] - rows[k - 1].Columns[0]);

                if (previousDirection != 0)
                {
                    dirPairs++;
                    if (direction == previousDirection)
                        sameDirCount++;
                }

                previousDirection = direction;
            }

            if (dirPairs > 0)
                directionConsistency = (double)sameDirCount / dirPairs;

            return runEnd - runStart + 1;
        }

        private static bool isFastLateralMove(IReadOnlyList<ManiaRow> rows, int k)
        {
            return k - 1 >= 0 && k < rows.Count
                              && rows[k].IsSingleNote && rows[k - 1].IsSingleNote
                              && rows[k].Columns[0] != rows[k - 1].Columns[0]
                              && rows[k].StartTime - rows[k - 1].StartTime < movement_fast_ms;
        }

        private static double localChordDensity(IReadOnlyList<ManiaRow> rows, int row)
        {
            int lo = Math.Max(0, row - movement_chord_window);
            int hi = Math.Min(rows.Count - 1, row + movement_chord_window);

            int chordCount = 0;

            for (int r = lo; r <= hi; r++)
            {
                if (rows[r].IsChord)
                    chordCount++;
            }

            return (double)chordCount / (hi - lo + 1);
        }

        private static double staminaFactorFor(IReadOnlyList<ManiaRow> rows, int row, double timeSincePreviousRow)
        {
            if (!rows[row].IsJump)
                return 1.0;

            double speedScale = DiffUtils.Smoothstep(stamina_speed_hi_ms - timeSincePreviousRow, 0.0, stamina_speed_hi_ms - stamina_speed_lo_ms)
                                * (1.0 - stamina_vfast_taper * DiffUtils.Smoothstep(stamina_speed_vfast_hi_ms - timeSincePreviousRow, 0.0, stamina_speed_vfast_hi_ms - stamina_speed_vfast_lo_ms));

            if (speedScale <= 0.0)
                return 1.0;

            int run = 1 + countRunBackward(row, stamina_run_cap - 1, earlier =>
                rows[earlier].IsJump && rows[earlier + 1].StartTime - rows[earlier].StartTime <= stamina_speed_hi_ms);

            double runWeight = DiffUtils.Smoothstep(run, stamina_run_lo, stamina_run_hi);
            return 1.0 + stamina_buff * speedScale * runWeight;
        }

        private static bool sameColumns(int[] a, int[] b)
        {
            if (a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// If <paramref name="b"/> is <paramref name="a"/> with every column shifted by the same constant
        /// k (with 0 &lt; |k| &lt;= <see cref="max_shift"/>), returns k; otherwise returns 0.
        /// </summary>
        private static int columnShift(int[] a, int[] b)
        {
            if (a.Length != b.Length || a.Length == 0)
                return 0;

            int k = b[0] - a[0];

            if (k == 0 || Math.Abs(k) > max_shift)
                return 0;

            for (int i = 1; i < a.Length; i++)
            {
                if (b[i] - a[i] != k)
                    return 0;
            }

            return k;
        }
    }
}
