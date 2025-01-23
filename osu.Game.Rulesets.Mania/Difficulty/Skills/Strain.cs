// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Aggregation;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Strain : ManiaAccuracySkill
    {
        protected override double DifficultyMultiplier => 1;

        // private double strainDecayBase => 0.15;

        public Strain(Mod[] mods, double od)
            : base(mods, od)
        {
        }

        // private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            double difficulty = TotalEvaluator.EvaluateTotalDifficultyOf(current);

            /*

            currChordStrain = norm(BalancingConstants.STRAIN, currChordStrain, totalDifficulty);

            totalDifficulty = norm(BalancingConstants.STRAIN, prevChordStrain * strainMultiplier, totalDifficulty);

            if (current.StartTime != current.Next(0)?.StartTime)
            {
                prevChordStrain = currChordStrain;
            }

            */

            return difficulty;
        }
    }
}
