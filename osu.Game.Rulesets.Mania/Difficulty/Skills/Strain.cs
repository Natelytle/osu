// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Aggregation;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Strain : ManiaAccuracySkill
    {
        private const double individual_decay_base = 0.125;
        private const double overall_decay_base = 0.30;

        private const double individual_multiplier = 0.43;
        private const double overall_multiplier = 0.264;

        private readonly double[] individualStrains;
        private double overallStrain;

        public Strain(Mod[] mods, double od, int totalColumns)
            : base(mods, od)
        {
            individualStrains = new double[totalColumns];
            overallStrain = 1;
        }

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            var maniaCurrent = (ManiaDifficultyHitObject)current;

            individualStrains[maniaCurrent.Column] = applyDecay(individualStrains[maniaCurrent.Column], maniaCurrent.ColumnStrainTime, individual_decay_base);
            individualStrains[maniaCurrent.Column] += IndividualStrainEvaluator.EvaluateDifficultyOf(current);

            overallStrain = applyDecay(overallStrain, maniaCurrent.DeltaTime, overall_decay_base);

            if (maniaCurrent.DeltaTime > 0)
            {
                overallStrain += OverallStrainEvaluator.EvaluateDifficultyOf(current);

                var obj = current;

                while ((obj = obj.Next(0)) is not null && obj.DeltaTime == 0)
                {
                    overallStrain += OverallStrainEvaluator.EvaluateDifficultyOf(obj);
                }
            }

            return individualStrains[maniaCurrent.Column] * individual_multiplier + overallStrain * overall_multiplier;
        }

        private double applyDecay(double value, double deltaTime, double decayBase)
            => value * Math.Pow(decayBase, deltaTime / 1000);
    }
}
