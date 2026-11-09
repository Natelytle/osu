// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    /// <summary>
    /// Comparisons between the column sets of two rows. Every array passed here is sorted ascending.
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
        public static bool SharesColumn(int[] a, int[] b) => SharedColumnCount(a, b) > 0;

        /// <summary>
        /// How many columns the two rows have in common.
        /// </summary>
        public static int SharedColumnCount(int[] a, int[] b)
        {
            int i = 0, j = 0, shared = 0;

            while (i < a.Length && j < b.Length)
            {
                if (a[i] == b[j])
                {
                    shared++;
                    i++;
                    j++;
                }
                else if (a[i] < b[j])
                    i++;
                else
                    j++;
            }

            return shared;
        }
    }
}
