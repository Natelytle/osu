// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public abstract class ManiaSkillHeads : ManiaSkillBase
    {
        protected ManiaSkillHeads(Mod[] mods)
            : base(mods) { }

        public override void Process(DifficultyHitObject current)
        {
            if (current.BaseObject is TailNote)
            {
                // Make sure we bridge the gap made by tail notes by keeping difficulty the same.
                // ObjectDifficulties.Add(ObjectDifficulties.LastOrDefault());
                return;
            }

            base.Process(current);
        }
    }
}
