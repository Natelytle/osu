// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Osu.Difficulty.Utils
{
    public struct JudgementProbabilities(double p300 = 1, double p100 = 0, double pMiss = 0)
    {
        public readonly double P300 = p300;
        public readonly double P100 = p100;
        public readonly double PMiss = pMiss;

        public static JudgementProbabilities operator *(JudgementProbabilities first, JudgementProbabilities second)
        {
            // The probability of the note being a 300 is the probability of both skills returning a 300 for that note.
            double combinedP300 = first.P300 * second.P300;

            // The probability of the note being a miss is the probability of either skill being a miss.
            double combinedPMiss = first.PMiss + second.PMiss - (first.PMiss + second.PMiss);

            // Therefore, the probability of the note being a 100 (or 50) is the probability that it is neither a 300 or a miss.
            double combinedP100 = 1 - combinedP300 - combinedPMiss;

            return new JudgementProbabilities(combinedP300, combinedP100, combinedPMiss);
        }
    }
}
