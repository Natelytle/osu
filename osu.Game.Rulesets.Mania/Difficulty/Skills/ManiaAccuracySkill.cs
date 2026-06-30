// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Utils.AccuracySimulation;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public abstract class ManiaAccuracySkill : ManiaSkill
    {
        protected ManiaAccuracySkill(Mod[] mods)
            : base(mods)
        {
        }

        public override double DifficultyValue()
        {
            AccuracySimulator accuracySimulator = new AccuracySimulator(Mods.ToArray(), 8, ObjectDifficulties, TailDifficulties);

            double skillLevel = accuracySimulator.SkillLevelAtAccuracy(1);

            return skillLevel * 0.95;
        }
    }
}
