// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    /// <summary>
    /// Helpers that compare the column-sets of two rows, used to recognise repeats, rolls and shared jacks.
    /// </summary>
    public static class ColumnPatternUtils
    {
        /// <summary>
        /// Whether <paramref name="a"/> and <paramref name="b"/> contain exactly the same columns in the same order.
        /// </summary>
        public static bool SameColumns(int[] a, int[] b)
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
        /// Whether <paramref name="a"/> and <paramref name="b"/> have at least one column in common.
        /// </summary>
        public static bool SharesColumn(int[] a, int[] b)
        {
            int i = 0, j = 0;

            while (i < a.Length && j < b.Length)
            {
                if (a[i] == b[j])
                    return true;

                if (a[i] < b[j])
                    i++;
                else
                    j++;
            }

            return false;
        }

        /// <summary>
        /// If <paramref name="b"/> is <paramref name="a"/> with every column shifted by the same constant
        /// k of a single adjacent column (|k| == 1, a roll), returns k; otherwise returns 0.
        /// </summary>
        public static int ColumnShift(int[] a, int[] b)
        {
            if (a.Length != b.Length || a.Length == 0)
                return 0;

            int k = b[0] - a[0];

            if (k == 0 || Math.Abs(k) > 1)
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
