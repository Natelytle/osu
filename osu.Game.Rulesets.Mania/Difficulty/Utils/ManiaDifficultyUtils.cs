// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public static class ManiaDifficultyUtils
    {
        /// <summary>
        /// Calculates hit window leniency based on the great hit window.
        /// </summary>
        /// <returns>Hit leniency value in milliseconds (clamped to reasonable bounds)</returns>
        public static double CalculateHitLeniency(double hitWindow)
        {
            double rawLeniency = 0.3 * Math.Sqrt(hitWindow / 500.0);

            rawLeniency = Math.Min(rawLeniency, 0.6 * (rawLeniency - 0.09) + 0.09);

            return Math.Max(1e-9, rawLeniency);
        }
    }
}
