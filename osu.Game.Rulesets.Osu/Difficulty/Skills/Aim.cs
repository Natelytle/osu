// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuProbSkill
    {
        public Aim(Mod[] mods)
            : base(mods)
        {
        }

        private readonly List<double> previousStrains = new List<double>();

        private double flowMultiplier => 69;
        private double snapMultiplier => 33;
        private double strainDecayBase => 0.15;

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            double snapDifficulty = AimEvaluator.EvaluateSnapDifficultyOf(current) * snapMultiplier;
            double flowDifficulty = AimEvaluator.EvaluateFlowDifficultyOf(current) * flowMultiplier;

            double currentDifficulty = Math.Min(snapDifficulty, flowDifficulty);
            double priorDifficulty = highestPreviousStrain(previousStrains, current, current.DeltaTime);

            double currentStrain = getStrainWithPrior(currentDifficulty, priorDifficulty, 3, 3);
            previousStrains.Add(currentStrain);

            return currentStrain;
        }

        private double getStrainWithPrior(double currentDifficulty, double priorDifficulty, double diffIncreaseDampFactor, double diffDecreaseDampFactor)
            => currentDifficulty > priorDifficulty
                ? (priorDifficulty * diffIncreaseDampFactor + currentDifficulty) / (diffIncreaseDampFactor + 1)
                : (priorDifficulty * diffDecreaseDampFactor + currentDifficulty) / (diffDecreaseDampFactor + 1);

        private double highestPreviousStrain(List<double> previousStrains, DifficultyHitObject current, double time)
        {
            double hardestPreviousDifficulty = 0;
            double cumulativeDeltatime = time;

            double timeDecay(double ms) => Math.Pow(strainDecayBase, Math.Pow(ms / 900, 7));

            for (int i = 0; i < previousStrains.Count; i++)
            {
                if (cumulativeDeltatime > 1200)
                {
                    previousStrains.RemoveRange(0, i);
                    break;
                }

                hardestPreviousDifficulty = Math.Max(hardestPreviousDifficulty, previousStrains[^(i + 1)] * timeDecay(cumulativeDeltatime));

                cumulativeDeltatime += current.Previous(i).DeltaTime;
            }

            return hardestPreviousDifficulty;
        }

        /// <summary>
        /// The penalty at each misscount value in the returned list is 5% higher than the last.
        /// For example, the penalty at the 3rd returned misscount would be 15%.
        /// </summary>
        /// <returns>An array of misscounts, separated by a 5% reduction to aim difficulty per index.</returns>
        public double[] GetMissCountsForPenalty()
        {
            const int count = 20;
            const double penalty_per_misscount = 1.0 / count;

            double fcSkill = GetFcSkill();

            double[] misscounts = new double[count];

            for (int i = 0; i < count; i++)
            {
                double penalizedSkill = fcSkill - fcSkill * penalty_per_misscount * (i + 1);

                misscounts[i] = GetMissCountAtSkill(penalizedSkill);
            }

            return misscounts;
        }
    }
}
