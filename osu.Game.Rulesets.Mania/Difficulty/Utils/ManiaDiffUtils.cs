// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public static class ManiaDiffUtils
    {
        public static double Softplus(double value, double width)
        {
            double z = value / width;

            return width * (Math.Max(z, 0.0) + Math.Log(1.0 + Math.Exp(-Math.Abs(z))));
        }
    }
}
