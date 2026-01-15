// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class JackEvaluator
    {
        public static double GetDifficultyOf(ManiaDifficultyHitObject current)
        {
            var data = current.DifficultyData;
            double baseDifficulty = data.SampleFeatureAtTime(current.StartTime, data.SameColumnPressure);

            // Adjust the toing
            double adjustedDifficulty = Math.Min(baseDifficulty, 8.0 + 0.85 * baseDifficulty);

            return adjustedDifficulty;
        }
    }
}
