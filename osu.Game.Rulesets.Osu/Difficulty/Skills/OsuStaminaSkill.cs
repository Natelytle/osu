// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class OsuStaminaSkill : Skill
    {
        public OsuStaminaSkill(Mod[] mods)
            : base(mods)
        {
        }

        public override void Process(DifficultyHitObject current)
        {
            throw new System.NotImplementedException();
        }

        public override double DifficultyValue()
        {
            throw new System.NotImplementedException();
        }
    }
}
