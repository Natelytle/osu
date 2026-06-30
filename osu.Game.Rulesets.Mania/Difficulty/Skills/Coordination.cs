// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Coordination : ManiaAccuracySkill
    {
        private readonly CoordinationProcessor coordinationProcessor;

        public Coordination(Mod[] mods)
            : base(mods)
        {
            coordinationProcessor = new CoordinationProcessor();
        }

        protected override double DifficultyAt(DifficultyHitObject current)
        {
            return coordinationProcessor.ProcessStrainFor(current);
        }
    }
}
