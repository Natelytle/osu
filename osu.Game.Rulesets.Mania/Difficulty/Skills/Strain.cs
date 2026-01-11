// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Strain : StrainSkill
    {
        public Strain(Mod[] mods, int totalColumns)
            : base(mods)
        {
        }

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            return JackEvaluator.EvaluateDifficultyOf(current) * 29.0;
        }

        protected override double CalculateInitialStrain(double offset, DifficultyHitObject current) => 0;

        private double applyDecay(double value, double deltaTime, double decayBase)
            => value * Math.Pow(decayBase, deltaTime / 1000);
    }
}
