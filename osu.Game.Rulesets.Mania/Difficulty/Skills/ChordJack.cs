// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class ChordJack : StrainSkill
    {
        public ChordJack(Mod[] mods)
            : base(mods)
        {
        }

        private const double convergence_time_seconds = 60.0;
        private static readonly double tau = convergence_time_seconds / Math.Log(100);

        private double stamina;

        private double strainDecay(double ms) => 1.0 - Math.Exp(-ms / (1000 * tau));

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject hitObject = (ManiaDifficultyHitObject)current;

            if (hitObject.CurrentChord.Notes.Count <= 1)
                return stamina;

            double difficulty = ChordjackEvaluator.EvaluateDifficultyOf(current);
            stamina += (difficulty - stamina) * strainDecay(current.DeltaTime);
            stamina = Math.Clamp(stamina, 0, 14);

            return stamina;
        }

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current)
        {
            return stamina - stamina * strainDecay(time - current.Previous(0).StartTime);
        }
    }
}
