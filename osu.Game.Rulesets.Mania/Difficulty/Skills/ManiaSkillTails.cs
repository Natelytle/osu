// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public abstract class ManiaSkillTails : ManiaSkillBase
    {
        // Used to link the tail difficulties up with the LN that corresponds with them.
        public readonly List<int> HeadIndices = new List<int>();

        protected ManiaSkillTails(Mod[] mods)
            : base(mods) { }

        public override void Process(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject maniaCurrent = (ManiaDifficultyHitObject)current;

            // We have to do a little hacky thing for our first release note, it won't correspond to a head difficulty if there's no previous head.
            if (current.BaseObject is not TailNote || maniaCurrent.HeadIndex == maniaCurrent.TailIndex)
            {
                return;
            }

            base.Process(current);
            HeadIndices.Add(maniaCurrent.HeadIndex);

            // Add the final tail chord difficulties
            if (maniaCurrent.NextTail(0) is null)
            {
                AddChordDifficulties(maniaCurrent.StartTime);
            }
        }
    }
}
