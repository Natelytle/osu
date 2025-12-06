// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class IndividualStrain : ManiaSkill
    {
        private double difficultyMultiplier => 0.25;

        public IndividualStrain(Mod[] mods, int totalColumns)
            : base(mods)
        {
            perColumnStrain = new double[totalColumns];
        }

        private readonly double[] perColumnStrain;

        protected override double DifficultyOnPress(DifficultyHitObject current)
        {
            return IndividualStrainEvaluator.EvaluateDifficultyOf(current) * difficultyMultiplier;
        }

        // No release difficulty for this skill.
        protected override double DifficultyOnRelease(DifficultyHitObject current)
        {
            return IndividualStrainEvaluator.EvaluateTailDifficultyOf(current) * difficultyMultiplier;
        }

        public virtual List<NestedObjectDifficultyInfo> GetStrainValues()
        {
            ProcessedDifficultyInfo.Sort((s1, s2) => s1.Time.CompareTo(s2.Time));

            List<NestedObjectDifficultyInfo> strainValues = new List<NestedObjectDifficultyInfo>();

            foreach (NestedObjectDifficultyInfo difficultyInfo in ProcessedDifficultyInfo)
            {
                ManiaDifficultyHitObject note = difficultyInfo.Note;

                perColumnStrain[note.Column] = difficultyInfo.Difficulty + applyDecay(perColumnStrain[note.Column], difficultyInfo.ColumnStrainTime, 0.125);

                strainValues.Add(new NestedObjectDifficultyInfo(perColumnStrain[note.Column], note, difficultyInfo.IsTail));
            }

            return strainValues;
        }

        private double applyDecay(double value, double deltaTime, double decayBase)
            => value * Math.Pow(decayBase, deltaTime / 1000);
    }
}
