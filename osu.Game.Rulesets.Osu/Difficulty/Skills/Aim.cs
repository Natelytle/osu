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

        private double currentStrain;

        private double skillMultiplier => 100;
        private double strainDecayBase => 0.10;

        private double timeStrainDecay(double ms) => Math.Pow(strainDecayBase, Math.Pow(ms / 900, 7));

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * timeStrainDecay(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            double priorStrain = highestPreviousStrain(current, previousStrains);

            currentStrain = (priorStrain * 3 + AimEvaluator.EvaluateDifficultyOf(current, withSliders)) / 4;

            previousStrains.Add(currentStrain);

            return currentStrain * skillMultiplier;
        }

        private double highestPreviousStrain(DifficultyHitObject current, List<double> previousDifficulties)
        {
            List<double> reversedDifficulties = new List<double>(previousDifficulties);

            reversedDifficulties.Reverse();

            double hardestPreviousDifficulty = 0;
            double cumulativeDeltatime = current.DeltaTime;

            for (int i = 0; i < previousDifficulties.Count; i++)
            {
                if (cumulativeDeltatime > 1500)
                    break;

                hardestPreviousDifficulty = Math.Max(hardestPreviousDifficulty, reversedDifficulties[i] * timeStrainDecay(cumulativeDeltatime));

                cumulativeDeltatime += current.Previous(i).DeltaTime;
            }

            return hardestPreviousDifficulty;
        }
    }
}
