// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;

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

        private const double movement_dir_lo = 0.30;
        private const double movement_dir_hi = 0.46;

        private const int movement_chord_window = 14;
        private const double movement_chord_lo = 0.14;
        private const double movement_chord_hi = 0.34;

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
        /// A "row" is a set of hit objects whose start times fall within chord tolerance of each other -
        /// i.e. notes that are effectively hit at the same time.
        /// </summary>
        private readonly struct Row
        {
            /// <summary>Sorted column indices of every note in this row.</summary>
            public readonly int[] Columns;

            public readonly double StartTime;

            public readonly List<ManiaDifficultyHitObject> ChordMembers;

            public Row(int[] columns, double startTime, List<ManiaDifficultyHitObject> chordMembers)
            {
                Columns = columns;
                StartTime = startTime;
                ChordMembers = chordMembers;
            }

            public bool IsChord => Columns.Length > 1;

            public bool IsSingleNote => Columns.Length == 1;

            public bool IsJump => Columns.Length == 2;
        }

        /// <summary>
        /// Groups objects into rows, assigning a manipulation factor to the notes in each row based on how much manipulation affects the difficulty of the note.
        /// </summary>
        /// <param name="objects">The objects to calculate the manipulation factor for.</param>
        public static void ProcessAndAssign(IReadOnlyList<ManiaDifficultyHitObject> objects)
        {
            if (objects.Count == 0)
                return;

            var rows = groupIntoRows(objects);

            for (int i = 0; i < rows.Count; i++)
            {
                double timeSincePreviousRow = i > 0 ? rows[i].StartTime - rows[i - 1].StartTime : double.PositiveInfinity;

                double manipulationFactor = Math.Min(
                    rollAndPatternFactor(rows, i, timeSincePreviousRow),
                    jumptrillFactor(rows, i, timeSincePreviousRow));

                double staminaFactor = staminaFactorFor(rows, i, timeSincePreviousRow);

                foreach (var member in rows[i].ChordMembers)
                {
                    if (manipulationFactor < 1.0)
                        member.ManipulationFactor = manipulationFactor;

                    if (staminaFactor > 1.0)
                        member.StaminaFactor = staminaFactor;
                }
            }
        }

        /// <summary>
        /// Groups consecutive hit objects that start within chord tolerance of each other into <see cref="Row"/>s.
        /// </summary>
        private static List<Row> groupIntoRows(IReadOnlyList<ManiaDifficultyHitObject> objects)
        {
            var rows = new List<Row>();

            int i = 0;

            while (i < objects.Count)
            {
                double rowStart = objects[i].StartTime;
                var members = new List<ManiaDifficultyHitObject>();

                while (i < objects.Count && Math.Abs(objects[i].StartTime - rowStart) <= ChordEvaluator.CHORD_TOLERANCE_MS)
                {
                    members.Add(objects[i]);
                    i++;
                }

                int[] columns = members.Select(m => m.Column).OrderBy(c => c).ToArray();
                rows.Add(new Row(columns, rowStart, members));
            }

            return rows;
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

        /// <summary>
        /// Detects "manipulation" patterns: fast rolls or jacks (rows repeating with a fixed period, or shifting
        /// by a constant column offset), and fast lateral movement runs. Returns a multiplier &lt;= 1.0 that nerfs
        /// difficulty for these easily-abusable patterns; 1.0 means no nerf applies.
        /// </summary>
        private static double rollAndPatternFactor(List<Row> rows, int row, double timeSincePreviousRow)
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

        /// <summary>
        /// Longest of: a "roll" (each row shifted by a constant column offset from the previous one), or a
        /// periodic jack/pattern repeating with period 2..<see cref="max_period"/>.
        /// </summary>
        private static int longestRollOrPeriodicRun(List<Row> rows, int row)
        {
            // "Roll": each row shifted by the same constant column offset from the previous row (e.g. 1,2,3,4 repeating with a +1 shift).
            int run = countRunBackward(row, run_cap, earlier => columnShift(rows[earlier].Columns, rows[earlier + 1].Columns) != 0);

            // Periodic jacks/patterns: row N repeats row N-period, for small periods.
            for (int period = 2; period <= max_period; period++)
                run = Math.Max(run, periodRunLength(rows, row, period));

            return run;
        }

        /// <summary>How many rows back from <paramref name="row"/> repeat with the given <paramref name="period"/>.</summary>
        private static int periodRunLength(List<Row> rows, int row, int period)
        {
            int run = 0;

            while (run < run_cap && row - period >= 0 && sameColumns(rows[row].Columns, rows[row - period].Columns))
            {
                run++;
                row -= period;
            }

            return run;
        }

        /// <summary>
        /// Detects a "jumptrill": an alternating pattern of two different jumps (e.g. AB AB AB), as opposed to a
        /// jump simply repeating in place. Returns a multiplier &lt;= 1.0 that nerfs difficulty.
        /// </summary>
        private static double jumptrillFactor(List<Row> rows, int row, double timeSincePreviousRow)
        {
            if (!rows[row].IsJump)
                return 1.0;

            double speedScale = DiffUtils.Smoothstep(jumptrill_speed_hi_ms - timeSincePreviousRow, 0.0, jumptrill_speed_hi_ms - jumptrill_speed_lo_ms);

            if (speedScale <= 0.0)
                return 1.0;

            // Count back through alternating jump pairs: row k must match row k-2 (same jump recurring)
            // but differ from row k-1 (the in-between jump is different), i.e. a genuine A-B-A-B alternation.
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

        /// <summary>
        /// Extends outward from <paramref name="row"/> in both directions while consecutive rows are fast,
        /// single-note, column-changing hits ("movement"). Also reports how consistently the movement
        /// goes in the same direction each step (1.0 = always the same direction, i.e. a pure roll).
        /// </summary>
        private static int movementRun(List<Row> rows, int row, out double directionConsistency)
        {
            directionConsistency = 0.0;

            if (!rows[row].IsSingleNote)
                return 0;

            int lo = row;
            while (row - lo < movement_cap && isFastLateralMove(rows, lo))
                lo--;

            int hi = row;
            while (hi - row < movement_cap && isFastLateralMove(rows, hi + 1))
                hi++;

            int dirPairs = 0;
            int sameDirCount = 0;
            int previousDirection = 0;

            for (int k = lo + 1; k <= hi; k++)
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

            return hi - lo + 1;
        }

        /// <summary>True if row <paramref name="k"/> is a fast single-note hit on a different column from row k-1.</summary>
        private static bool isFastLateralMove(List<Row> rows, int k)
        {
            return k - 1 >= 0 && k < rows.Count
                              && rows[k].IsSingleNote && rows[k - 1].IsSingleNote
                              && rows[k].Columns[0] != rows[k - 1].Columns[0]
                              && rows[k].StartTime - rows[k - 1].StartTime < movement_fast_ms;
        }

        /// <summary>Fraction of rows within a window around <paramref name="row"/> that are chords (2+ notes).</summary>
        private static double localChordDensity(List<Row> rows, int row)
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

        /// <summary>
        /// Detects sustained runs of fast jumps (not necessarily alternating - just back-to-back 2-note rows
        /// hit quickly). Returns a multiplier &gt;= 1.0 that buffs difficulty for stamina heavy patterns.
        /// </summary>
        private static double staminaFactorFor(List<Row> rows, int row, double timeSincePreviousRow)
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
