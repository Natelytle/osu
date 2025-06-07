// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class SameColumnPressureEvaluator
    {
        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            double difficulty = (1000 / current.ColumnDeltaTime) * (1000 / (current.ColumnDeltaTime + 60)) * jackNerfer(current.ColumnDeltaTime);

            return difficulty;
        }

        private static double jackNerfer(double delta) => 1 - 7e-5 * (1 / Math.Pow(0.15 + Math.Abs(delta - 80) / 1000, 4));
    }
}
