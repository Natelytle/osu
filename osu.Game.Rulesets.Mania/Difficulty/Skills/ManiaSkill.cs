// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public abstract class ManiaSkill : Skill
    {
        private const double star_rating_accuracy = 0.9575;

        private readonly TotalAccuracyCurve accuracyCurve = new TotalAccuracyCurve();
        // private readonly List<AccuracyDifficulties> accuracyDifficulties = new List<AccuracyDifficulties>();

        private double noteCount;
        private double totalNoteWeight;

        protected ManiaSkill(Mod[] mods)
            : base(mods)
        {
        }

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            noteCount++;
            totalNoteWeight += getNoteWeight(current);

            AccuracyDifficulties difficulties = AccuracyDifficultiesAt(current);
            // accuracyDifficulties.Add(difficulties);
            accuracyCurve.AddToCurve(difficulties);

            return difficulties.BaseDifficulty;
        }

        protected abstract AccuracyDifficulties AccuracyDifficultiesAt(DifficultyHitObject current);

        private static double getNoteWeight(DifficultyHitObject current)
        {
            const double max_long_note_weight_duration_ms = 1000.0;
            const double long_note_weight_per_200_ms = 0.6;

            double noteWeight = 1;

            if (current.BaseObject is HoldNote holdNote)
            {
                double duration = Math.Min(holdNote.EndTime - holdNote.StartTime, max_long_note_weight_duration_ms);
                noteWeight += long_note_weight_per_200_ms * duration / 200.0;
            }

            return noteWeight;
        }

        public override double DifficultyValue() => DifficultyValueAtAccuracy(star_rating_accuracy);

        public double DifficultyValueAtAccuracy(double accuracy)
        {
            if (noteCount == 0 || accuracy <= AccuracyValueMultipliers.ACCURACY_VALUES[^1])
                return 0.0;

            double rawDifficulty = RootFinding.FindRootExpand(skill => AccuracyAtSkill(skill) - accuracy, 0, 10);

            const double note_count_offset = 34.64147;

            return rawDifficulty * (totalNoteWeight / (totalNoteWeight + note_count_offset));
        }

        public double AccuracyAtSkill(double skill)
        {
            if (skill == 0)
                return 0.0;

            double accuracySum = accuracyCurve.TotalAccuracyAt(skill);

            // Return the accuracy value, but we subtract 1% of the notes from the divisor so that an SS isn't just the difficulty of the highest note.
            return accuracySum / (noteCount - Math.Min(noteCount * 0.01, 10));
        }

        /// <summary>
        /// The coefficients of a quartic fitted to the miss counts at each skill level.
        /// </summary>
        /// <returns>The coefficients for our penalty polynomial.</returns>
        public PolynomialPenaltyUtils.QuarticCoefficients GetScoreLossCoefficients(double ssSkill)
        {
            Dictionary<double, double> scoreLosses = new Dictionary<double, double>();

            // If there are no notes, we just return a zero-polynomial.
            if (ObjectDifficulties.Count == 0 || ObjectDifficulties.Max() == 0)
                return new PolynomialPenaltyUtils.QuarticCoefficients();

            foreach (double skillProportion in PolynomialPenaltyUtils.SKILL_PROPORTIONS)
            {
                if (skillProportion == 1)
                {
                    scoreLosses[skillProportion] = 0;
                    continue;
                }

                double penalizedSkill = ssSkill * skillProportion;

                // We take the log to squash miss counts, which have large absolute value differences, but low relative differences, into a straighter line for the polynomial.
                scoreLosses[skillProportion] = Math.Log((1.0 - AccuracyAtSkill(penalizedSkill)) + 1);
            }

            return PolynomialPenaltyUtils.GetPenaltyCoefficients(scoreLosses);
        }
    }
}
