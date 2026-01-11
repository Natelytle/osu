// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public static class Handedness
    {
        private static readonly Hand[][] handedness = new[]
        {
            new[] { Hand.Ambiguous },
            new[] { Hand.Left, Hand.Right },
            new[] { Hand.Left, Hand.Ambiguous, Hand.Right, },
            new[] { Hand.Left, Hand.Left, Hand.Right, Hand.Right },
            new[] { Hand.Left, Hand.Left, Hand.Ambiguous, Hand.Right, Hand.Right },
            new[] { Hand.Left, Hand.Left, Hand.Left, Hand.Right, Hand.Right, Hand.Right },
            new[] { Hand.Left, Hand.Left, Hand.Left, Hand.Ambiguous, Hand.Right, Hand.Right, Hand.Right },
            new[] { Hand.Left, Hand.Left, Hand.Left, Hand.Left, Hand.Right, Hand.Right, Hand.Right, Hand.Right },
            new[] { Hand.Left, Hand.Left, Hand.Left, Hand.Left, Hand.Ambiguous, Hand.Right, Hand.Right, Hand.Right, Hand.Right },
            new[] { Hand.Left, Hand.Left, Hand.Left, Hand.Left, Hand.Left, Hand.Right, Hand.Right, Hand.Right, Hand.Right, Hand.Right },
        };

        public static Hand GetHandednessOf(int column, int columnCount)
        {
            return columnCount < 11 ? handedness[columnCount - 1][column] : Hand.Ambiguous;
        }

        public static double GetHandednessFactorOf(Hand hand, Hand other)
        {
            if (hand == other)
                return 1;

            if (hand == Hand.Ambiguous || other == Hand.Ambiguous)
                return 0.5;

            return 0;
        }
    }
}
