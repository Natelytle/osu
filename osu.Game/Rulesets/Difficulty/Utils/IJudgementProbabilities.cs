// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Difficulty.Utils
{
    public interface IJudgementProbabilities
    {
        /// <summary>
        /// The average score of these <see cref="IJudgementProbabilities"/>
        /// </summary>
        double Score { get; }

        /// <summary>
        /// The variance of the score of these <see cref="IJudgementProbabilities"/>
        /// </summary>
        double Variance { get; }

        /// <summary>
        /// The third central moment of the distribution of the score of these <see cref="IJudgementProbabilities"/>
        /// </summary>
        double Gamma { get; }

        IJudgementProbabilities Inverse();
    }
}
