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
                    manipulationFactor(rowColumns, row, rowDelta),
                    jumptrillFactor(rowColumns, row, rowDelta));

                if (factor >= 1.0)
                    continue;

                foreach (var member in rowMembers[row])
                    member.ManipulationFactor = factor;
            }
        }

        /// <summary>General roll / stair / split-roll / vibro dampening (sustained, fast).</summary>
        private static double manipulationFactor(List<int[]> rowColumns, int row, double rowDelta)
        {
            double speedScale = speedScaleFor(rowDelta);

            if (speedScale <= 0.0)
                return 1.0;

            int run = rollRun(rowColumns, row);

            for (int period = 2; period <= max_period; period++)
                run = Math.Max(run, periodRun(rowColumns, row, period));

            if (run == 0)
                return 1.0;

            double runWeight = Math.Min(1.0, run / period_ramp);
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

            double runWeight = Math.Min(1.0, run / jumptrill_ramp);
            return 1.0 - jumptrill_nerf * runWeight * speedScale;
        }

        private static double speedScaleFor(double rowDelta)
        {
            return DifficultyCalculationUtils.Smoothstep(speed_hi_ms - rowDelta, 0.0, speed_hi_ms - speed_lo_ms);
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
