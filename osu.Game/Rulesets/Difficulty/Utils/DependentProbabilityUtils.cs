// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Rulesets.Difficulty.Utils
{
    public static class DependentProbabilityUtils
    {
        /// <summary>
        /// The total probability of each dependent probability occuring, given prior probabilities.
        /// </summary>
        /// <param name="dependentProbabilities">The conditional probabilities, which use the probability that the previous note was a success to determine their total probability of being a success.</param>
        /// <returns>A list of what the total probabilities of the dependent probabilities are.</returns>
        public static List<double> TotalProbabilitiesOf(List<ConditionalProbability> dependentProbabilities)
        {
            List<double> totalProbabilities = new List<double>();
            double accruedProbability = 1;

            for (int i = 0; i < dependentProbabilities.Count; i++)
            {
                // Law of total probability
                // The probability of B is equal to P(B | A) * P(A) + P(B | A') * P(A')
                accruedProbability = accruedProbability * dependentProbabilities[i].PGivenSuccess
                                     + (1 - accruedProbability) * dependentProbabilities[i].PGivenFail;

                totalProbabilities.Add(accruedProbability);
            }

            return totalProbabilities;
        }
    }
}
