// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Strain : StrainSkill
    {
        public static double JackMultiplier => 17.5;
        public static double StreamMultiplier => 5.5;

        public Strain(Mod[] mods, int totalColumns)
            : base(mods)
        {
        }

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            return DifficultyCalculationUtils.Norm(3, JackEvaluator.EvaluateDifficultyOf(current), StreamEvaluator.EvaluateDifficultyOf(current));
        }

        public override double DifficultyValue()
        {
            return ObjectStrains.Count > 0 ? ObjectStrains.Average() * 20 : 0;
        }

        protected override double CalculateInitialStrain(double offset, DifficultyHitObject current) => 0;

        private double applyDecay(double value, double deltaTime, double decayBase)
            => value * Math.Pow(decayBase, deltaTime / 1000);
    }
}
