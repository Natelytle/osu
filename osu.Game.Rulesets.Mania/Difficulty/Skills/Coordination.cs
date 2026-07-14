// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Coordination : ManiaSkill
    {
        private readonly DifficultyProcessor coordinationProcessor;

        public Coordination(Mod[] mods, DifficultyProcessor coordinationProcessor)
            : base(mods)
        {
            this.coordinationProcessor = coordinationProcessor;
        }

        protected override double DifficultyAt(DifficultyHitObject current)
        {
            coordinationProcessor.ProcessRowStrainFor(current);

            return coordinationProcessor.CurrentStrain;
        }
    }
}
