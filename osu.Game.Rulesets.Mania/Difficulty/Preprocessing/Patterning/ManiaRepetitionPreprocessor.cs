// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning
{
    public static class ManiaRepetitionPreprocessor
    {
        private const int max_period = 4;

        private const double turns_lo = 1.5;
        private const double turns_hi = 3.0;

        private const double even_tolerance = 0.18;

        private const double alternation_lo = 0.50;
        private const double alternation_hi = 1.00;

        public static void ProcessAndAssign(IReadOnlyList<ManiaRow> rows)
        {
            int count = rows.Count;
            double[] repetition = new double[count];

            for (int period = 2; period <= max_period; period++)
            {
                int i = period;

                while (i < count)
                {
                    if (rows[i].ColumnMask != rows[i - period].ColumnMask)
                    {
                        i++;
                        continue;
                    }

                    int firstRepeat = i;
                    int first = firstRepeat - period;

                    double total = 0.0;
                    int gaps = 0;

                    for (int row = first + 1; row < firstRepeat; row++)
                    {
                        total += rows[row].StartTime - rows[row - 1].StartTime;
                        gaps++;
                    }

                    while (i < count && rows[i].ColumnMask == rows[i - period].ColumnMask)
                    {
                        double gap = rows[i].StartTime - rows[i - 1].StartTime;
                        double mean = total / gaps;

                        if (Math.Abs(gap - mean) > even_tolerance * mean)
                            break;

                        total += gap;
                        gaps++;
                        i++;
                    }

                    int last = i - 1;
                    double turns = (double)(last - first + 1) / period;

                    if (i == firstRepeat)
                        i++;

                    if (turns < turns_lo)
                        continue;

                    double sequence = DiffUtils.Smoothstep(turns, turns_lo, turns_hi) * alternationOf(rows, last, period);

                    if (sequence <= 0)
                        continue;

                    for (int row = first; row <= last; row++)
                        repetition[row] = Math.Max(repetition[row], sequence);
                }
            }

            for (int i = 0; i < count; i++)
                rows[i].Repetition = repetition[i];
        }

        private static double alternationOf(IReadOnlyList<ManiaRow> rows, int index, int period)
        {
            int clear = 0;

            for (int offset = 0; offset < period; offset++)
            {
                int row = index - offset;

                if ((rows[row].ColumnMask & rows[row - 1].ColumnMask) == 0)
                    clear++;
            }

            return DiffUtils.Smoothstep((double)clear / period, alternation_lo, alternation_hi);
        }
    }
}
