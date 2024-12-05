// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Aggregation;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuProbabilitySkill
    {
        public Aim(Mod[] mods, bool withSliders)
            : base(mods)
        {
        }

        private readonly List<double> previousStrains = new List<double>();

        private double strainDecayBase => 0.15;
        private double strainIncreaseRate => 10;

        protected override double HitProbability(double skill, double difficulty)
        {
            if (difficulty == 0) return 1;
            if (skill == 0) return 0;

            return SpecialFunctions.Erf(skill / (Math.Sqrt(2) * difficulty));
        }

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            double snapDifficulty = SnapAimEvaluator.EvaluateDifficultyOf(current);
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current);

            double currentDifficulty = Math.Min(snapDifficulty, flowDifficulty);
            double priorDifficulty = highestPreviousStrain(current, current.DeltaTime);

            double currentStrain = getStrainValueOf(currentDifficulty, priorDifficulty);
            previousStrains.Add(currentStrain);

            return currentDifficulty + currentStrain;
        }

        private double getStrainValueOf(double currentDifficulty, double priorDifficulty) => (priorDifficulty * strainIncreaseRate + currentDifficulty) / (strainIncreaseRate + 1);

        private double highestPreviousStrain(DifficultyHitObject current, double time)
        {
            double hardestPreviousDifficulty = 0;
            double cumulativeDeltaTime = time;

            double timeDecay(double ms) => Math.Pow(strainDecayBase, Math.Pow(ms / 900, 7));

            for (int i = 0; i < previousStrains.Count; i++)
            {
                if (cumulativeDeltaTime > 1200)
                {
                    previousStrains.RemoveRange(0, i);
                    break;
                }

                hardestPreviousDifficulty = Math.Max(hardestPreviousDifficulty, previousStrains[^(i + 1)] * timeDecay(cumulativeDeltaTime));

                cumulativeDeltaTime += current.Previous(i).DeltaTime;
            }

            return hardestPreviousDifficulty;
        }
    }
}
