﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
// using System.Collections.Generic;
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

        private double currentStrain;

        private double strainDecayBase => 0.15;

        private double strainInfluence => 1 / 4.0;

        protected override double HitProbability(double skill, double difficulty)
        {
            if (difficulty == 0) return 1;
            if (skill == 0) return 0;

            return SpecialFunctions.Erf(skill / (Math.Sqrt(2) * difficulty));
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);

            double snapDifficulty = SnapAimEvaluator.EvaluateDifficultyOf(current);
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current);

            double currentDifficulty = Math.Min(snapDifficulty, flowDifficulty);
            currentStrain += currentDifficulty / 4.0;

            // Strain contributes around 1 extra star for consistent 7-star gameplay at 200bpm, and 1.75 extra stars for consistent 7-star gameplay at 300bpm.
            return currentDifficulty + currentStrain * strainInfluence;
        }
    }
}
