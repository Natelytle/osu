// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class IndividualStrainEvaluator
    {
        private const double difficulty_multiplier = 0.3;

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            double difficulty = (1000 / current.ColumnDeltaTime) * (1000 / (current.ColumnDeltaTime + 60));

            // Nerf jacks
            difficulty *= 1 - 7e-5 * (1 / Math.Pow((150 + Math.Abs(current.ColumnDeltaTime - 80)) / 1000, 4));

            return difficulty * difficulty_multiplier;
        }
    }
}
