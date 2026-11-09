// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning
{
    public static class HandLayout
    {
        public static double ShareOf(int column, int totalColumns, ManiaHand hand)
        {
            double left = leftShareOf(column, totalColumns);

            return hand == ManiaHand.Left ? left : 1.0 - left;
        }

        public static ManiaHand HandOf(int column, int totalColumns) =>
            ShareOf(column, totalColumns, ManiaHand.Left) > 0 ? ManiaHand.Left : ManiaHand.Right;

        public static int FingerCount(int totalColumns) => (totalColumns + 1) / 2;

        private static double leftShareOf(int column, int totalColumns)
        {
            if (totalColumns <= 1)
                return 0.5;

            int half = totalColumns / 2;

            return column < half ? 1.0 : 0.0;
        }
    }
}
