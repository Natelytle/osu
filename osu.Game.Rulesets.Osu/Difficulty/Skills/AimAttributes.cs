// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class AimAttributes
    {
        public double FcProbabilityThroughput;
        public double HiddenFactor;
        public double[] ComboThroughputs = Array.Empty<double>();
        public double[] MissThroughputs = Array.Empty<double>();
        public double[] MissCounts = Array.Empty<double>();
        public double CheeseNoteCount;
        public double[] CheeseLevels = Array.Empty<double>();
        public double[] CheeseFactors = Array.Empty<double>();
    }
}
