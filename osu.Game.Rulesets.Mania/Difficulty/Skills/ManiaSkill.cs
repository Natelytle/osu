// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public abstract class ManiaSkill : Skill
    {
        protected ManiaSkill(Mod[] mods)
            : base(mods)
        {
            ProcessedDifficultyInfo = new List<NestedObjectDifficultyInfo>();
        }

        protected List<NestedObjectDifficultyInfo> ProcessedDifficultyInfo;

        public override void Process(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject maniaCurrent = (ManiaDifficultyHitObject)current;

            if (current.BaseObject is Note)
            {
                ProcessedDifficultyInfo.Add(new NestedObjectDifficultyInfo(DifficultyOnPress(current), maniaCurrent));
            }
            else
            {
                ProcessedDifficultyInfo.Add(new NestedObjectDifficultyInfo(DifficultyOnPress(current), maniaCurrent));
                ProcessedDifficultyInfo.Add(new NestedObjectDifficultyInfo(DifficultyOnRelease(current), maniaCurrent, true));
            }
        }

        protected abstract double DifficultyOnPress(DifficultyHitObject current);
        protected abstract double DifficultyOnRelease(DifficultyHitObject current);

        // Unused
        public override double DifficultyValue()
        {
            throw new System.NotImplementedException();
        }
    }
}
