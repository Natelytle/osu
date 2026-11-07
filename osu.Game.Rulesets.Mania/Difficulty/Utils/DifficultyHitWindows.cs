// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public readonly struct DifficultyHitWindows
    {
        public DifficultyHitWindows(double overallDifficulty, bool classic)
        {
            double clampedOd = classic ? Math.Clamp(overallDifficulty, 0, 10) : overallDifficulty;

            HMax = Math.Floor(classic ? 16 : 22.4 - 0.6 * overallDifficulty) + 0.5;
            H300 = Math.Floor(64 - 3 * clampedOd) + 0.5;
            H200 = Math.Floor(97 - 3 * overallDifficulty) + 0.5;
            H100 = Math.Floor(127 - 3 * overallDifficulty) + 0.5;
            H50 = Math.Floor(151 - 3 * overallDifficulty) + 0.5;
            HMiss = Math.Floor(188 - 3 * overallDifficulty) + 0.5;
        }

        public readonly double HMax;
        public readonly double H300;
        public readonly double H200;
        public readonly double H100;
        public readonly double H50;
        public readonly double HMiss;
    }
}
