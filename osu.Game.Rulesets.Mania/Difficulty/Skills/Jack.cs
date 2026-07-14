// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Jack : ManiaSkill
    {
        private readonly DifficultyProcessor jackProcessor;

        public Jack(Mod[] mods, DifficultyProcessor jackProcessor)
            : base(mods)
        {
            this.jackProcessor = jackProcessor;
        }

        protected override double DifficultyAt(DifficultyHitObject current)
        {
            jackProcessor.ProcessRowStrainFor(current);

            return jackProcessor.CurrentStrain;
        }
    }
}
