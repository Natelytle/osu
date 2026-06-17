// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Release : StrainDecaySkill
    {
        private const double strain_decay_base = 0.89647;

        public Release(Mod[] mods)
            : base(mods)
        {
        }

        protected override double SkillMultiplier => 1.0;

        protected override double StrainDecayBase => strain_decay_base;

        protected override double StrainValueOf(DifficultyHitObject current) => ReleaseEvaluator.EvaluateDifficultyOf((ManiaDifficultyHitObject)current);
    }
}
