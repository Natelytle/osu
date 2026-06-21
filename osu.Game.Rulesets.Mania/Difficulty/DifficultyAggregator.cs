// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class DifficultyAggregator
    {
        private const double high_band_weight = 0.25;
        private const double high_band_scale = 0.88;

        private const double mid_band_weight = 0.20;
        private const double mid_band_scale = 0.94;

        private const double power_mean_weight = 0.55;
        private const double power_mean_exponent = 5.0;

        private const double note_count_offset = 34.64147;
        private const double final_scaling = 0.90741;

        private const double star_rating_multiplier = 0.38374;
        private const double star_rating_exponent = 0.52899;

        private static readonly double[] highBandPercentiles = { 0.945, 0.935, 0.925, 0.915 };
        private static readonly double[] midBandPercentiles = { 0.845, 0.835, 0.825, 0.815 };

        private readonly RunningPercentile[] highBand = highBandPercentiles.Select(p => new RunningPercentile(p)).ToArray();
        private readonly RunningPercentile[] midBand = midBandPercentiles.Select(p => new RunningPercentile(p)).ToArray();

        private double powerSum;
        private int positiveCount;

        public double CurrentRating { get; private set; }

        public void Add(double strain, double noteWeight, double constantMultiplier, double additionalMultiplier)
        {
            if (strain > 0)
            {
                foreach (var tracker in highBand)
                    tracker.Add(strain);

                foreach (var tracker in midBand)
                    tracker.Add(strain);

                powerSum += Math.Pow(strain, power_mean_exponent);
                positiveCount++;
            }

            if (positiveCount == 0)
                return;

            double highMean = highBand.Average(t => t.Value);
            double midMean = midBand.Average(t => t.Value);
            double powerMean = Math.Pow(powerSum / positiveCount, 1.0 / power_mean_exponent);

            double rawDifficulty = high_band_weight * (high_band_scale * highMean) + mid_band_weight * (mid_band_scale * midMean)
                                                                                   + power_mean_weight * powerMean;

            double scaledDifficulty = rawDifficulty * (noteWeight / (noteWeight + note_count_offset)) * final_scaling;

            double rating = calculateStarRating(scaledDifficulty) * constantMultiplier * additionalMultiplier;

            if (rating > CurrentRating)
                CurrentRating = rating;
        }

        private static double calculateStarRating(double scaledDifficulty)
        {
            if (scaledDifficulty <= 0)
                return 0.0;

            return star_rating_multiplier * Math.Pow(scaledDifficulty, star_rating_exponent);
        }
    }
}
