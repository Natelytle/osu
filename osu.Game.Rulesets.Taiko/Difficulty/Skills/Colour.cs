// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Taiko.Difficulty.Evaluators;

namespace osu.Game.Rulesets.Taiko.Difficulty.Skills
{
    /// <summary>
    /// Calculates the colour coefficient of taiko difficulty.
    /// </summary>
    public class Colour : TaikoProbSkill
    {
        protected override double FcProbability => 0.02;
        protected override double SkillMultiplier => 0.7;

        private double currentStrain;

        // This is set to decay slower than other skills, due to the fact that only the first note of each encoding class
        //  having any difficulty values, and we want to allow colour difficulty to be able to build up even on
        // slower maps.
        protected double StrainDecayBase => 0.8;

        public Colour(Mod[] mods)
            : base(mods)
        {
        }

        protected override double HitProbability(double skill, double difficulty)
        {
            if (skill == 0) return 0;
            if (difficulty == 0) return 1;

            // This uses an erf-like curve to drastically increase the probability of hitting notes with a lower difficulty than skill. This keeps length from being inflated.
            return Math.Tanh(skill / difficulty);
        }

        private double strainDecay(double ms) => Math.Pow(StrainDecayBase, ms / 1000);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            currentStrain += ColourEvaluator.EvaluateDifficultyOf(current) * SkillMultiplier;

            return currentStrain;
        }
    }
}
