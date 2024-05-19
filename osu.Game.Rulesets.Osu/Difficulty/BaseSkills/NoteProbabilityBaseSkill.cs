// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.BaseSkills
{
    public abstract class NoteProbabilityBaseSkill : Skill
    {
        protected NoteProbabilityBaseSkill(Mod[] mods)
            : base(mods)
        {
        }

        protected readonly List<double> Difficulties = new List<double>();

        public override void Process(DifficultyHitObject current)
        {
            Difficulties.Add(DifficultyValueOf(current));
        }

        /// <summary>
        /// Returns the strain value at <see cref="DifficultyHitObject"/>. This value is calculated with or without respect to previous objects.
        /// </summary>
        protected abstract double DifficultyValueOf(DifficultyHitObject current);

        public abstract List<JudgementProbabilities> JudgementProbabilitiesList(double skill);

        public override double DifficultyValue() => throw new NotImplementedException();
    }
}
