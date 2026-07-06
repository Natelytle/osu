// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Processing
{
    public class TechnicalProcessor : IDifficultyProcessor
    {
        public double CurrentStrain { get; private set; }

        private const double strain_decay_base = 0.06696;

        private const double speed_factor_offset = 0.050;

        private const double reversal_base_complexity = 0.6;
        private const double reversal_coefficient_multiplier = 2.0;

        private const double wide_jump_nerf = 0.60;
        private const double wide_jump_span_lo = 3.0;
        private const double wide_jump_span_hi = 5.5;

        private const double pattern_buff = 0.69740;
        private const double technical_scale = 1.49964;

        private const double rhythm_tech_buff = 0.9;
        private const double rhythm_tech_center = 0.15;
        private const double rhythm_tech_width = 0.085;
        private const int rhythm_window = 10;

        private readonly Queue<double> recentIrregularities = new Queue<double>();
        private double irregularitySum;

        private const int variety_window = 8;
        private const double variety_floor = 1.55;
        private const double variety_lo = 2.5;
        private const double variety_hi = 5.5;
        private const double variety_gap_log_base = 1.18;

        private double previousDeltaTime = -1.0;

        private readonly Queue<(int rhythmClass, int direction)> recentShapes = new Queue<(int, int)>();

        public void ProcessStrainFor(DifficultyHitObject current)
        {
            CurrentStrain *= DiffUtils.Pow(strain_decay_base, current.DeltaTime / 1000);

            var hitObject = (ManiaDifficultyHitObject)current;

            if (hitObject.DeltaTime < ChordUtils.CHORD_TOLERANCE_MS)
                return;

            double rhythmIrregularity = 0.0;

            if (previousDeltaTime > ChordUtils.CHORD_TOLERANCE_MS)
            {
                double ratio = hitObject.DeltaTime / previousDeltaTime;

                if (ratio > 1.0)
                    ratio = 1.0 / ratio;

                rhythmIrregularity = 1.0 - ratio;
            }

            double columnComplexity = 0.0;

            if (hitObject.Previous() is ManiaDifficultyHitObject previous && hitObject.Previous(1) is ManiaDifficultyHitObject previous2)
            {
                int previousDirection = previous.Column - previous2.Column;
                int currentDirection = hitObject.Column - previous.Column;

                if (previousDirection != 0 && currentDirection != 0 && Math.Sign(previousDirection) != Math.Sign(currentDirection))
                {
                    double coefficient = CrossColumnUtils.SumBoundaryMultipliersBetween(previous.Column, hitObject.Column, hitObject.PreviousHitObjects.Length);
                    columnComplexity += reversal_base_complexity + reversal_coefficient_multiplier * coefficient;
                }

                if (Math.Abs(currentDirection) >= 2)
                    columnComplexity += CrossColumnUtils.AverageBoundaryMultipliersBetween(previous.Column, hitObject.Column, hitObject.PreviousHitObjects.Length); // wide jump, averaged path scaled by sqrt(span)

                double spanDamper = 1.0 - wide_jump_nerf * DiffUtils.Smoothstep(Math.Abs(currentDirection), wide_jump_span_lo, wide_jump_span_hi);
                columnComplexity *= spanDamper;
            }

            double speedFactor = 1.0 / (hitObject.DeltaTime / 1000.0 + speed_factor_offset);

            previousDeltaTime = hitObject.DeltaTime;
            double complexity = Math.Max(rhythmIrregularity + columnComplexity, variety_floor * patternVariety(hitObject));
            double rhythmAmp = rhythmAmplifier(windowedIrregularity(rhythmIrregularity));

            CurrentStrain += pattern_buff * complexity * speedFactor * technical_scale * rhythmAmp * hitObject.ManipulationFactor * hitObject.StaminaFactor;
        }

        private double windowedIrregularity(double rhythmIrregularity)
        {
            recentIrregularities.Enqueue(rhythmIrregularity);
            irregularitySum += rhythmIrregularity;

            while (recentIrregularities.Count > rhythm_window)
                irregularitySum -= recentIrregularities.Dequeue();

            return irregularitySum / recentIrregularities.Count;
        }

        private static double rhythmAmplifier(double windowedIrregularity)
            => 1.0 + rhythm_tech_buff * DiffUtils.BellCurve(windowedIrregularity, rhythm_tech_center, rhythm_tech_width);

        private double patternVariety(ManiaDifficultyHitObject hitObject)
        {
            int rhythmClass = (int)Math.Round(Math.Log(hitObject.DeltaTime) / Math.Log(variety_gap_log_base));
            int direction = hitObject.Previous() is ManiaDifficultyHitObject previous ? Math.Sign(hitObject.Column - previous.Column) : 0;

            recentShapes.Enqueue((rhythmClass, direction));

            while (recentShapes.Count > variety_window)
                recentShapes.Dequeue();

            int distinctShapes = recentShapes.Distinct().Count();

            return DiffUtils.Smoothstep(distinctShapes, variety_lo, variety_hi);
        }
    }
}
