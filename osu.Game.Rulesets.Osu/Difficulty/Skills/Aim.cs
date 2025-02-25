﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Aggregation;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuTimeSkill
    {
        public Aim(Mod[] mods, bool withSliders)
            : base(mods)
        {
        }

        private double currentStrain;
        private double agilityStrain;

        private double strainDecayBase => 0.15;
        private double strainDecayAgiBase => 0.15;

        private double strainInfluence => 1 / 8.0;

        private double agiStrainInfluence => 1 / 1.0;

        protected override double HitProbability(double skill, double difficulty)
        {
            if (difficulty <= 0) return 1;
            if (skill <= 0) return 0;

            return DifficultyCalculationUtils.Erf(skill / (Math.Sqrt(2) * difficulty));
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);
        private double agilityStrainDecay(double ms) => Math.Pow(strainDecayAgiBase, ms / 1000);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            agilityStrain *= agilityStrainDecay(current.DeltaTime);
            currentStrain *= strainDecay(current.DeltaTime);

            double agilityDifficulty = SnapAimEvaluator.EvaluateAgilityBonus(current);
            double snapBaseDifficulty = SnapAimEvaluator.EvaluateDifficultyOf(current);
            double snapDifficulty = snapBaseDifficulty + (agilityDifficulty + agilityStrain * agiStrainInfluence);
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current);

            double currentDifficulty = Math.Min(snapDifficulty, flowDifficulty);

            if (snapDifficulty < flowDifficulty)
            {
                currentStrain += snapBaseDifficulty / 4.0;
                agilityStrain += agilityDifficulty * 0.5;
            }
            else
            {
                currentStrain += currentDifficulty / 4.0;
            }

            // Strain contributes around 1 extra star for consistent 7-star gameplay at 200bpm, and 1.75 extra stars for consistent 7-star gameplay at 300bpm.
            return currentDifficulty + currentStrain * strainInfluence;
        }
    }
}
