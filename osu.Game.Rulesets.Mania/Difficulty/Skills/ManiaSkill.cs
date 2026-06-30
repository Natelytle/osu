// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public abstract class ManiaSkill : Skill
    {
        private double totalNoteWeight;

        protected ManiaSkill(Mod[] mods)
            : base(mods)
        {
        }

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            const double max_long_note_weight_duration_ms = 1000.0;
            const double long_note_weight_per_200_ms = 0.6;

            totalNoteWeight++;

            // Add additional weight for hold notes, depending on their length.
            if (current.BaseObject is HoldNote holdNote)
            {
                double duration = Math.Min(holdNote.EndTime - holdNote.StartTime, max_long_note_weight_duration_ms);
                totalNoteWeight += long_note_weight_per_200_ms * duration / 200.0;
            }

            return DifficultyAt(current);
        }

        protected abstract double DifficultyAt(DifficultyHitObject current);

        public override double DifficultyValue()
        {
            double[] sortedStrains = ObjectDifficulties.Where(strain => strain > 0).OrderBy(strain => strain).ToArray();

            if (sortedStrains.Length == 0)
                return 0.0;

            const int power_mean_exponent = 5;

            double[] highPercentiles = { 0.945, 0.935, 0.925, 0.915 };
            double[] midPercentiles = { 0.845, 0.835, 0.825, 0.815 };

            double highMean = calculatePercentileMean(sortedStrains, highPercentiles);
            double midMean = calculatePercentileMean(sortedStrains, midPercentiles);
            double powerMean = calculatePowerMean(sortedStrains, power_mean_exponent);

            const double high_percentile_weight = 0.25;
            const double high_percentile_scale = 0.88;

            const double mid_percentile_weight = 0.20;
            const double mid_percentile_scale = 0.94;

            const double power_mean_weight = 0.55;

            double rawDifficulty = high_percentile_weight * (high_percentile_scale * highMean)
                                   + mid_percentile_weight * (mid_percentile_scale * midMean)
                                   + power_mean_weight * powerMean;

            const double note_count_offset = 34.64147;
            const double final_scaling = 0.90741;

            return rawDifficulty * (totalNoteWeight / (totalNoteWeight + note_count_offset)) * final_scaling;
        }

        /// <summary>
        /// Calculates the mean of specific percentile values from a sorted array.
        /// </summary>
        /// <param name="sortedValues">Array of difficulty values, sorted ascending.</param>
        /// <param name="percentiles">Array of percentile positions (0.0 to 1.0).</param>
        private double calculatePercentileMean(double[] sortedValues, double[] percentiles)
        {
            int maxIndex = sortedValues.Length - 1;
            double sum = 0.0;

            foreach (double percentile in percentiles)
            {
                int index = Math.Clamp((int)Math.Round(maxIndex * percentile), 0, maxIndex);
                sum += sortedValues[index];
            }

            return sum / percentiles.Length;
        }

        private double calculatePowerMean(double[] values, int exponent)
        {
            double sum = values.Sum(value => DiffUtils.Pow(value, exponent));
            return DiffUtils.Pow(sum / values.Length, 1.0 / exponent);
        }
    }
}
