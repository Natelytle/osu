// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.BaseSkills;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class AimNoteProbabilities : NoteProbabilityBaseSkill
    {
        public AimNoteProbabilities(Mod[] mods)
            : base(mods)
        {
        }

        protected override double DifficultyValueOf(DifficultyHitObject current)
        {
            // Should come from aim evaluator
            return 1;
        }

        private JudgementProbabilities judgementProbabilities(double skill, double difficulty)
        {
            double hitProbability = SpecialFunctions.Erf(skill / (Math.Sqrt(2) * difficulty));

            return new JudgementProbabilities(hitProbability, 0, 1 - hitProbability);
        }

        public override List<JudgementProbabilities> JudgementProbabilitiesList(double skill) => Difficulties.Select(difficulty => judgementProbabilities(skill, difficulty)).ToList();
    }
}
