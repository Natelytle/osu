// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Utils
{
    public class OsuJudgementProbabilities : IJudgementProbabilities
    {
        public double Score { get; init; }
        public double Variance { get; init; }
        public double Gamma { get; init; }

        public OsuJudgementProbabilities() { }

        public OsuJudgementProbabilities(double greatProbability, double okProbability, double mehProbability)
        {
            Score = 300 * greatProbability + 100 * okProbability + 50 * mehProbability;

            Variance = Math.Pow(300 - Score, 2) * greatProbability
                       + Math.Pow(100 - Score, 2) * okProbability
                       + Math.Pow(50 - Score, 2) * mehProbability;

            Gamma = Math.Pow(300 - Score, 3) * greatProbability
                    + Math.Pow(100 - Score, 3) * okProbability
                    + Math.Pow(50 - Score, 3) * mehProbability;
        }

        public IJudgementProbabilities Inverse()
        {
            return new OsuJudgementProbabilities
            {
                Score = 300 - Score,
                Variance = Variance,
                Gamma = -Gamma
            };
        }
    }
}
