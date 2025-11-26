// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public abstract class ManiaSkill : Skill
    {
        protected ManiaSkill(Mod[] mods)
            : base(mods)
        {
            Difficulties = new List<(double, double, bool)>();
        }

        protected List<(double difficulty, double time, bool isTail)> Difficulties;

        public override void Process(DifficultyHitObject current)
        {
            if (current.BaseObject is Note)
            {
                Difficulties.Add((EvaluatorValueAt(current, false), current.StartTime, false));
            }
            else
            {
                double headDifficulty = EvaluatorValueAt(current, false);
                double tailDifficulty = EvaluatorValueAt(current, true) - headDifficulty;

                Difficulties.Add((headDifficulty, current.StartTime, false));
                Difficulties.Add((tailDifficulty, current.EndTime, true));
            }
        }

        protected abstract double EvaluatorValueAt(DifficultyHitObject current, bool includeTail);

        public abstract List<(double value, bool isTail)> GetStrainValues();
    }
}
