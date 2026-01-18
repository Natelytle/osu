// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public static class ManiaDifficultyUtils
    {
        public const double COLUMN_ACTIVITY_WINDOW = 150;

        /// <summary>
        /// Calculates hit window leniency based on the great hit window.
        /// </summary>
        /// <returns>Hit leniency value in milliseconds (clamped to reasonable bounds)</returns>
        public static double CalculateHitLeniency(double hitWindow) => Math.Min(hitWindow + 45, 0.6 * (hitWindow + 105));
    }
}
