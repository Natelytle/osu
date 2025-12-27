// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public record ManiaDifficultyConstants
    {
        public static ManiaDifficultyConstants Default { get; } = new ManiaDifficultyConstants();

        public double IndividualStrainScale = 1.0;

        public double OverallStrainScale = 1.0;

        public double IndividualHoldFactorMultiplier = 1.25;
        public double OverallHoldFactorMultiplier = 1.25;
    }
}
