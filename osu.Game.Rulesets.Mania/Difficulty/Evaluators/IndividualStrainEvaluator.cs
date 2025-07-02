// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class IndividualStrainEvaluator
    {
        private const double difficulty_multiplier = 0.36;

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            // A window slightly smaller than the average deltaTime of prevPrev to next. Capped to 50ms.
            double averageDeltaTime = Math.Min((current.NextInColumn(0)?.StartTime - current.PrevInColumn(1)?.StartTime) / 4 ?? double.PositiveInfinity, 65);

            // Anti-cheese for notes over 300bpm.
            double cheesedDeltaTime = Math.Max(current.ColumnDeltaTime, averageDeltaTime);

            double difficulty = (1000 / cheesedDeltaTime) * (1000 / (cheesedDeltaTime + 60));

            // Nerf jacks
            difficulty *= 1 - 7e-5 * (1 / Math.Pow((150 + Math.Abs(current.ColumnDeltaTime - 80)) / 1000, 4));

            return difficulty * difficulty_multiplier;
        }
    }
}
