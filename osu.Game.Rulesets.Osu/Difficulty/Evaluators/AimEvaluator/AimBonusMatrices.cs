// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.AimEvaluator
{
    public class AimBonusMatrices
    {
        // 0 degrees is linear, 180 degrees is back and forth.
        public static readonly double[,] LAST_NOTE_POSITION_BONUS =
        {
            // 180   150   120    90    60    30     0
            { 1.00, 1.00, 1.00, 1.00, 1.00, 1.00, 1.00 }, // <= 0.125x current velocity
            { 1.00, 1.05, 1.15, 1.25, 1.40, 1.50, 1.60 }, // 0.25x current velocity
            { 1.00, 1.10, 1.30, 1.50, 1.80, 2.00, 2.20 }, // 0.5x current velocity
            { 1.00, 1.10, 1.30, 1.50, 1.80, 2.00, 2.20 }, // 1x current velocity
            { 1.00, 1.10, 1.30, 1.50, 1.80, 2.00, 2.20 }, // 2x current velocity
            { 1.00, 1.10, 1.30, 1.50, 1.80, 2.00, 2.20 }, // 4x current velocity
            { 1.00, 1.10, 1.30, 1.50, 1.80, 2.00, 2.20 }, // 8x current velocity
        };
    }
}
