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
    public class Aim : OsuStrainSkill
    {
        public Aim(Mod[] mods, bool withSliders)
            : base(mods)
        {
            this.withSliders = withSliders;
        }

        private readonly bool withSliders;

        private readonly List<double> previousStrains = new List<double>();

        private double skillMultiplier => 23.55;
        private double strainDecayBase => 0.15;

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current)
        {
            double priorDifficulty = highestPreviousStrain(previousStrains, current, time - current.Previous(0).StartTime);

            return getStrainWithPrior(0, priorDifficulty, 3);
        }

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            double currentDifficulty = AimEvaluator.EvaluateDifficultyOf(current, withSliders) * skillMultiplier;
            double priorDifficulty = highestPreviousStrain(previousStrains, current, current.DeltaTime);

            double currentStrain = getStrainWithPrior(currentDifficulty, priorDifficulty, 3);
            previousStrains.Add(currentStrain);

            return currentStrain;
        }

        private double getStrainWithPrior(double currentDifficulty, double priorDifficulty, double priorDiffInfluence)
            => (priorDifficulty * priorDiffInfluence + currentDifficulty) / (priorDiffInfluence + 1);

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
    }
}
