// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Strain : Skill
    {
        private double skillMultiplier => 1.0;
        private double strainDecayBase => 0.3;

        private readonly List<double> difficulties = new List<double>();
        private readonly List<double> currentChordDifficulties = new List<double>();

        private double? timeSinceLastChord;

        private double currentStrain;

        public Strain(Mod[] mods)
            : base(mods)
        {
        }

        public override void Process(DifficultyHitObject current)
        {
            getStrainValues(current);
        }

        public override double DifficultyValue()
        {
            return difficulties.Max() * skillMultiplier;
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        private void getStrainValues(DifficultyHitObject current)
        {
            double speedDifficulty = SpeedEvaluator.EvaluateDifficultyOf(current);
            double sameColumnDifficulty = SameColumnEvaluator.EvaluateDifficultyOf(current);
            double crossColumnDifficulty = CrossColumnEvaluator.EvaluateDifficultyOf(current);

            double totalDifficulty = combinedValue(speedDifficulty, sameColumnDifficulty, crossColumnDifficulty);

            currentChordDifficulties.Add(totalDifficulty);

            if (current.StartTime == current.Next(0)?.StartTime)
                return;

            // Once the whole chord is processed, we can add chord difficulties and finally get strain.
            double chordDifficulty = ChordEvaluator.EvaluateDifficultyOf(current);

            currentStrain *= strainDecay(timeSinceLastChord ?? 0);

            timeSinceLastChord = current.Next(0)?.StartTime - current.StartTime;

            for (int i = 0; i < currentChordDifficulties.Count; i++)
            {
                currentChordDifficulties[i] = norm(1.3, currentChordDifficulties[i], chordDifficulty);
                currentChordDifficulties[i] += currentStrain;
            }

            currentStrain = currentChordDifficulties.Max();

            difficulties.AddRange(currentChordDifficulties);

            currentChordDifficulties.Clear();
        }

        private double combinedValue(double speedValue, double sameColumnDifficulty, double crossColumnDifficulty)
        {
            double combinedValue = norm(1.5, sameColumnDifficulty, crossColumnDifficulty);
            combinedValue = norm(1.2, combinedValue, speedValue);

            return combinedValue;
        }

        /// <summary>
        /// Returns the <i>p</i>-norm of an <i>n</i>-dimensional vector.
        /// </summary>
        /// <param name="p">The value of <i>p</i> to calculate the norm for.</param>
        /// <param name="values">The coefficients of the vector.</param>
        private double norm(double p, params double[] values) => Math.Pow(values.Sum(x => Math.Pow(x, p)), 1 / p);
    }
}
