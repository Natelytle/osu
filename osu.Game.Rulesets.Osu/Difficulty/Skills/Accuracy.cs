// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class Accuracy : OsuProbabilitySkill
    {
        private readonly bool usingClassicSliderAccuracy;
        private readonly double hitWindow;
        private readonly double hitWindow50;

        private const double rhythm_influence = 1;

        private const double ar_bonus_start = 600;
        private const double ar_bonus_end = 1500;
        private const double ar_influence = 1;

        public Accuracy(Mod[] mods, double hitWindow, double hitWindow50)
            : base(mods)
        {
            usingClassicSliderAccuracy = mods.OfType<OsuModClassic>().Any(m => m.NoSliderHeadAccuracy.Value);
            this.hitWindow = hitWindow;
            this.hitWindow50 = hitWindow50;
        }

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            double rhythm = RhythmEvaluator.EvaluateDifficultyOf(current);

            double rhythmMultiplier = Math.Pow(rhythm, rhythm_influence);

            double arMultiplier = 1 + DifficultyCalculationUtils.Smootherstep(((OsuHitObject)current.BaseObject).TimePreempt, ar_bonus_start, ar_bonus_end) * ar_influence;

            double hitWindowMultiplier = rhythmMultiplier * arMultiplier;

            if (current.BaseObject is Slider && usingClassicSliderAccuracy)
                hitWindowMultiplier *= hitWindow / hitWindow50;

            return hitWindowMultiplier;
        }

        protected override double HitProbability(double skill, double difficulty)
        {
            if (difficulty <= 0) return 1;
            if (skill <= 0) return 0;

            // The inverse of 120 * Math.Pow(7.5 / deviation, 2)
            double baseDeviation = 7.5 / Math.Sqrt(skill / 120);

            return SpecialFunctions.Erf(hitWindow / (Math.Sqrt(2) * difficulty * baseDeviation));
        }
    }
}
