﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using System.Linq;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to press keys with regard to keeping up with the speed at which objects need to be hit.
    /// </summary>
    public class Speed : OsuContinuousSkill
    {
        private double skillMultiplier => 1.40;
        protected override double StrainDecayBase => 0.3;

        private double currentStrain;
        private double currentRhythm;

        private readonly List<double> sliderStrains = new List<double>();

        protected override int ReducedSectionCount => 5;

        public Speed(Mod[] mods)
            : base(mods)
        {
        }

        private double strainDecay(double ms) => Math.Pow(StrainDecayBase, ms / 1000);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(((OsuDifficultyHitObject)current).StrainTime);
            currentStrain += SpeedEvaluator.EvaluateDifficultyOf(current, Mods) * skillMultiplier;

            currentRhythm = RhythmEvaluator.EvaluateDifficultyOf(current);

            double totalStrain = currentStrain * currentRhythm;

            if (current.BaseObject is Slider)
                sliderStrains.Add(totalStrain);

            return totalStrain;
        }

        public double RelevantNoteCount()
        {
            if (Strains.Count == 0)
                return 0;

            double maxStrain = Strains.MaxBy(s => s.Strain).Strain;
            if (maxStrain == 0)
                return 0;

            return Strains.Sum(s => s.StrainCountChange == 1 ? 1.0 / (1.0 + Math.Exp(-(s.Strain / maxStrain * 12.0 - 6.0))) : 0);
        }

        public double CountTopWeightedSliders() => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, DifficultyValue());
    }
}
