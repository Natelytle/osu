// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Osu.Difficulty.Utils
{
    public readonly record struct DifficultyPoint
    {
        /// <summary>
        /// The difficulty value of this difficulty point.
        /// </summary>
        public double Difficulty { get; init; }

        /// <summary>
        /// The absolute time value of this difficulty point.
        /// </summary>
        public double Time { get; init; }

        /// <summary>
        /// How far this difficulty point is from the previous.
        /// </summary>
        public double DeltaTime { get; init; }
    }
}
