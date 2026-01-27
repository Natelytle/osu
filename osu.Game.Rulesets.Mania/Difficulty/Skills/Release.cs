// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Release : ManiaSkill
    {
        public Release(Mod[] mods)
            : base(mods, LnMode.Tails) { }

        protected override double BaseDifficulty(ManiaDifficultyHitObject current)
        {
            return ReleaseEvaluator.EvaluateDifficultyOf(current);
        }
    }
}
