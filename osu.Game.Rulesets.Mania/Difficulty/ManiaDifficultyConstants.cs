// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public record ManiaDifficultyConstants
    {
        public static ManiaDifficultyConstants Default { get; } = new ManiaDifficultyConstants();

        // Star rating exponent
        public double StarRatingExponent { get; init; } = 0.52899;
        public double StarRatingMultiplier { get; init; } = 0.37;
        public double PerformanceExponent { get; init; } = 2.470;
        public double PerformanceMultiplier { get; init; } = 3.3944;

        // Skill multipliers
        public double CoordinationMultiplier { get; init; } = 2.22;
        public double JackMultiplier { get; init; } = 208.95;
        public double ReleaseMultiplier { get; init; } = 3.0532;
        public double SpeedMultiplier { get; init; } = 2.4;
        public double TechnicalMultiplier { get; init; } = 4.88;

        // Skill accuracy curves
        public double CoordinationAccuracyScaleFactor { get; init; } = 2.2;
        public double JackAccuracyScaleFactor { get; init; } = 3.3;
        public double ReleaseAccuracyScaleFactor { get; init; } = 1.05;
        public double SpeedAccuracyScaleFactor { get; init; } = 1.45;
        public double TechnicalAccuracyScaleFactor { get; init; } = 1.3;

        // Strain decay parameters
        public double CoordinationStrainDecayBase { get; init; } = 0.52909;
        public double JackStrainDecayBase { get; init; } = 0.50497;
        public double ReleaseStrainDecayBase { get; init; } = 0.89647;
        public double SpeedStrainDecayBase { get; init; } = 0.05007;
        public double TechnicalStrainDecayBase { get; init; } = 0.06696;
    }
}
