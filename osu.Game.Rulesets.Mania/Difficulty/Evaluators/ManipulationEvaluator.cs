// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class ManipulationEvaluator
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

        public static void Evaluate(IReadOnlyList<ManiaDifficultyHitObject> objects)
        {
            if (objects.Count == 0)
                return;

            // Group consecutive objects that start within the chord tolerance into rows.
            var rowColumns = new List<int[]>();
            var rowTimes = new List<double>();
            var rowMembers = new List<List<ManiaDifficultyHitObject>>();

            for (int i = 0; i < objects.Count;)
            {
                double rowStart = objects[i].StartTime;
                var columns = new List<int>();
                var members = new List<ManiaDifficultyHitObject>();

                int j = i;

                while (j < objects.Count && Math.Abs(objects[j].StartTime - rowStart) <= ChordEvaluator.CHORD_TOLERANCE_MS)
                {
                    columns.Add(objects[j].Column);
                    members.Add(objects[j]);
                    j++;
                }

                columns.Sort();
                rowColumns.Add(columns.ToArray());
                rowTimes.Add(rowStart);
                rowMembers.Add(members);
                i = j;
            }

            for (int row = 0; row < rowColumns.Count; row++)
            {
                double rowDelta = row > 0 ? rowTimes[row] - rowTimes[row - 1] : double.PositiveInfinity;

                double factor = Math.Min(
                    manipulationFactor(rowColumns, rowTimes, row, rowDelta),
                    jumptrillFactor(rowColumns, row, rowDelta));

                if (factor >= 1.0)
                    continue;

                foreach (var member in rowMembers[row])
                    member.ManipulationFactor = factor;
            }
        }

        private static double manipulationFactor(List<int[]> rowColumns, List<double> rowTimes, int row, double rowDelta)
        {
            double speedScale = speedScaleFor(rowDelta);

            if (speedScale <= 0.0)
                return 1.0;

            int run = rollRun(rowColumns, row);

            for (int period = 2; period <= max_period; period++)
                run = Math.Max(run, periodRun(rowColumns, row, period));

            double runWeight = DifficultyCalculationUtils.ReverseLerp(run, 0.0, period_ramp);
            int moveRun = movementRun(rowColumns, rowTimes, row, out double directionConsistency);

            if (moveRun >= 2)
            {
                double staminaRelief = movement_stamina_relief * DifficultyCalculationUtils.Smoothstep(moveRun, movement_taper_lo, movement_taper_hi);
                double rollGate = DifficultyCalculationUtils.Smoothstep(directionConsistency, movement_dir_lo, movement_dir_hi);
                double chordGate = 1.0 - DifficultyCalculationUtils.Smoothstep(localChordDensity(rowColumns, row), movement_chord_lo, movement_chord_hi);
                double moveWeight = DifficultyCalculationUtils.ReverseLerp(moveRun, 0.0, period_ramp) * (1.0 - staminaRelief) * rollGate * chordGate;
                runWeight = Math.Max(runWeight, moveWeight);
            }

            if (runWeight <= 0.0)
                return 1.0;

            return 1.0 - high_speed_nerf * runWeight * speedScale;
        }

        private static double jumptrillFactor(List<int[]> rowColumns, int row, double rowDelta)
        {
            if (rowColumns[row].Length != 2)
                return 1.0;

            double speedScale = DifficultyCalculationUtils.Smoothstep(jumptrill_speed_hi_ms - rowDelta, 0.0, jumptrill_speed_hi_ms - jumptrill_speed_lo_ms);

            if (speedScale <= 0.0)
                return 1.0;

            int run = 0;

            for (int k = row;
                 k - 2 >= 0
                 && rowColumns[k].Length == 2
                 && sameColumns(rowColumns[k], rowColumns[k - 2])
                 && !sameColumns(rowColumns[k], rowColumns[k - 1]);
                 k--)
            {
                run++;
            }

            if (run == 0)
                return 1.0;

            double runWeight = DifficultyCalculationUtils.ReverseLerp(run, 0.0, jumptrill_ramp);
            return 1.0 - jumptrill_nerf * runWeight * speedScale;
        }

        private static double speedScaleFor(double rowDelta)
        {
            return DifficultyCalculationUtils.Smoothstep(speed_hi_ms - rowDelta, 0.0, speed_hi_ms - speed_lo_ms);
        }

        private static int movementRun(List<int[]> rows, List<double> rowTimes, int row, out double directionConsistency)
        {
            directionConsistency = 0.0;

            if (rows[row].Length != 1)
                return 0;

            int lo = row;
            while (row - lo < movement_cap && isFastMove(rows, rowTimes, lo))
                lo--;

            int hi = row;
            while (hi - row < movement_cap && isFastMove(rows, rowTimes, hi + 1))
                hi++;

            // Fraction of consecutive moves that keep the same spatial direction (a roll sweep) rather than
            // reversing/jumping (reading).
            int dirPairs = 0;
            int sameDir = 0;
            int prevDir = 0;

            for (int k = lo + 1; k <= hi; k++)
            {
                int dir = Math.Sign(rows[k][0] - rows[k - 1][0]);

                if (prevDir != 0)
                {
                    dirPairs++;
                    if (dir == prevDir)
                        sameDir++;
                }

                prevDir = dir;
            }

            if (dirPairs > 0)
                directionConsistency = (double)sameDir / dirPairs;

            return hi - lo + 1;
        }

        private static double localChordDensity(List<int[]> rows, int row)
        {
            int lo = Math.Max(0, row - movement_chord_window);
            int hi = Math.Min(rows.Count - 1, row + movement_chord_window);
            int chords = 0;

            for (int r = lo; r <= hi; r++)
            {
                if (rows[r].Length > 1)
                    chords++;
            }

            return (double)chords / (hi - lo + 1);
        }

        private static bool isFastMove(List<int[]> rows, List<double> rowTimes, int k)
        {
            return k - 1 >= 0 && k < rows.Count && rows[k].Length == 1 && rows[k - 1].Length == 1
                   && rows[k][0] != rows[k - 1][0]
                   && rowTimes[k] - rowTimes[k - 1] < movement_fast_ms;
        }

        private static int periodRun(List<int[]> rows, int row, int period)
        {
            int run = 0;

            while (run < run_cap && row - period >= 0 && sameColumns(rows[row], rows[row - period]))
            {
                run++;
                row--;
            }

            return run;
        }

        private static int rollRun(List<int[]> rows, int row)
        {
            int run = 0;

            while (run < run_cap && row - 1 >= 0 && shiftOf(rows[row - 1], rows[row]) != 0)
            {
                run++;
                row--;
            }

            return run;
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
        private static int shiftOf(int[] a, int[] b)
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
