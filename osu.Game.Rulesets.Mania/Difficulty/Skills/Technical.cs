// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Technical : ManiaSkill
    {
        private readonly DifficultyProcessor technicalProcessor;

        public Technical(Mod[] mods, DifficultyProcessor technicalProcessor)
            : base(mods)
        {
            this.technicalProcessor = technicalProcessor;
        }

        protected override double DifficultyAt(DifficultyHitObject current)
        {
            technicalProcessor.ProcessRowStrainFor(current);

            return technicalProcessor.CurrentStrain;
        }
    }
}
