// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public abstract class ManiaSkillHeads : ManiaSkillBase
    {
        // Used to link the tail difficulties up with the LN that corresponds with them.
        public Dictionary<int, int> HeadIndicesToDifficultyIndices = new Dictionary<int, int>();

        protected ManiaSkillHeads(Mod[] mods)
            : base(mods) { }

        public override void Process(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject maniaCurrent = (ManiaDifficultyHitObject)current;

            if (current.BaseObject is TailNote)
            {
                return;
            }

            if (current.BaseObject is HeadNote)
            {
                HeadIndicesToDifficultyIndices.Add(((ManiaDifficultyHitObject)current).HeadIndex, ProcessedNoteCount);
            }

            base.Process(current);

            // Add the final head chord difficulties
            if (maniaCurrent.NextHead(0) is null)
            {
                // If we have more map we can use to smooth with, try
                AddChordDifficulties(maniaCurrent.StartTime);
            }
        }
    }
}
