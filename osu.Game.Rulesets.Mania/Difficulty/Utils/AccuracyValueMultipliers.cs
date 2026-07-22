// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public struct AccuracyValueMultipliers
    {
        public static readonly double[] ACCURACY_VALUES = { 1.00, 0.995, 0.99, 0.98, 0.95, 0.90, 0.85, 0.80, 0.75 };

        public readonly double[] AccuracyMultipliers;

        public AccuracyValueMultipliers(
            double multiplierAtSS,
            double multiplierAt995,
            double multiplierAt99,
            double multiplierAt98,
            double multiplierAt95,
            double multiplierAt90,
            double multiplierAt85,
            double multiplierAt80)
        {
            AccuracyMultipliers = new[]
            {
                multiplierAtSS,
                multiplierAt995,
                multiplierAt99,
                multiplierAt98,
                multiplierAt95,
                multiplierAt90,
                multiplierAt85,
                multiplierAt80,
                0.0 // The multiplier at 75% is always 0.
            };
        }
    }
}
