// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Difficulty.Utils
{
    /// <summary>
    /// A probability with two outcomes, one that is the case if the last note was a success, and one that is the case if the last note was a fail.
    /// </summary>
    public record struct ConditionalProbability
    {
        public double PGivenSuccess;
        public double PGivenFail;
    }
}
