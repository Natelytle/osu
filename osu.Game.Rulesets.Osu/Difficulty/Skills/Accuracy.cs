// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class Accuracy : AccuracyTimeSkill
    {
        private readonly bool sliderAccuracy;

        public Accuracy(Mod[] mods)
            : base(mods)
        {
            sliderAccuracy = !mods.Any(m => m is OsuModClassic classic && classic.NoSliderHeadAccuracy.Value);
        }

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            return 1;
        }

        protected override IJudgementProbabilities JudgementProbabilities(double skill, double difficulty, DifficultyHitObject hitObject)
        {
            double deviation = UnstableRateAtSkill(skill) / 10;

            if (hitObject.BaseObject is HitCircle || (sliderAccuracy && hitObject.BaseObject is Slider))
            {
                double greatHitWindow = hitObject.HitWindow(HitResult.Great);
                double okHitWindow = hitObject.HitWindow(HitResult.Ok);
                double mehHitWindow = hitObject.HitWindow(HitResult.Meh);

                double greatProb = DifficultyCalculationUtils.Erf(greatHitWindow / (Math.Sqrt(2) * deviation));
                double okProb = DifficultyCalculationUtils.Erf(okHitWindow / (Math.Sqrt(2) * deviation)) - DifficultyCalculationUtils.Erf(greatHitWindow / (Math.Sqrt(2) * deviation));
                double mehProb = DifficultyCalculationUtils.Erf(mehHitWindow / (Math.Sqrt(2) * deviation)) - DifficultyCalculationUtils.Erf(okHitWindow / (Math.Sqrt(2) * deviation));

                return new OsuJudgementProbabilities
                (
                    greatProbability: greatProb,
                    okProbability: okProb,
                    mehProbability: mehProb
                );
            }

            return new OsuJudgementProbabilities
            (
                greatProbability: 1,
                okProbability: 0,
                mehProbability: 0
            );
        }

        public double UnstableRateAtSkill(double skill) => Math.Max(1000 - skill, 0);
    }
}
