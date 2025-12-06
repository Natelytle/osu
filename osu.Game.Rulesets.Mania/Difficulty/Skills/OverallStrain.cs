// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class OverallStrain : ManiaSkill
    {
        private double difficultyMultiplier => 0.27;

        public OverallStrain(Mod[] mods)
            : base(mods)
        {
        }

        private double strain;

        protected override double DifficultyOnPress(DifficultyHitObject current)
        {
            return IndividualStrainEvaluator.EvaluateDifficultyOf(current) * difficultyMultiplier;
        }

        protected override double DifficultyOnRelease(DifficultyHitObject current)
        {
            return IndividualStrainEvaluator.EvaluateTailDifficultyOf(current) * difficultyMultiplier;
        }

        public virtual List<NestedObjectDifficultyInfo> GetStrainValues()
        {
            ProcessedDifficultyInfo.Sort((s1, s2) => s1.Time.CompareTo(s2.Time));

            List<NestedObjectDifficultyInfo> strainValues = new List<NestedObjectDifficultyInfo>();

            List<NestedObjectDifficultyInfo> chord = new List<NestedObjectDifficultyInfo>();
            double chordDifficulty = 0;

            for (int i = 0; i < ProcessedDifficultyInfo.Count; i++)
            {
                NestedObjectDifficultyInfo cur = ProcessedDifficultyInfo[i];
                NestedObjectDifficultyInfo? prev = i > 0 ? ProcessedDifficultyInfo[i - 1] : null;

                double delta = prev == null ? double.PositiveInfinity : cur.Time - prev.Value.Time;

                if (prev == null)
                {
                    chord.Add(cur);
                    chordDifficulty += cur.Difficulty;
                    continue;
                }

                if (delta == 0)
                {
                    chord.Add(cur);
                    chordDifficulty += cur.Difficulty;
                    continue;
                }

                strain = applyDecay(strain, delta, 0.125);
                strain += chordDifficulty;

                clearChord(strain);

                chord.Add(cur);
                chordDifficulty = cur.Difficulty;
            }

            if (chord.Count > 0)
            {
                strain += chordDifficulty;
                clearChord(strain);
            }

            return strainValues;

            void clearChord(double finalStrain)
            {
                foreach (var obj in chord)
                    strainValues.Add(new NestedObjectDifficultyInfo(finalStrain, obj.Note, obj.IsTail));

                chord.Clear();
                chordDifficulty = 0;
            }
        }

        private double applyDecay(double value, double deltaTime, double decayBase)
            => value * Math.Pow(decayBase, deltaTime / 1000);
    }
}
