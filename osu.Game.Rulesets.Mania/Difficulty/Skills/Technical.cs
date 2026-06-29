// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Technical : StrainDecaySkill
    {
        private const double strain_decay_base = 0.06696;

        private const double speed_factor_offset = 0.050;

        private const double reversal_base_complexity = 0.6;
        private const double reversal_coefficient_multiplier = 2.0;

        private const double pattern_buff = 0.69740;
        private const double technical_scale = 1.49964;

        // Rhythmically irregular passages (genuine "tech") are much harder than the same column
        // pattern played to a steady stream rhythm. Reversal/jump complexity alone cannot tell the
        // two apart (fast streams reverse just as often), so amplify the strain by how irregular the
        // local rhythm is - this lifts the under-rated tech maps without touching steady streams.
        // The irregularity is measured over a window (not per-note) so that a sustained tech passage
        // is rewarded while an isolated rhythm change inside an otherwise steady stream / dan course
        // is not (per-note irregularity is too noisy and would leak into steady maps).
        //
        // The amplifier is a BAND, not a ramp: the under-rated tech maps sit at *moderate* sustained
        // irregularity (~0.11-0.22), whereas steady streams/dan courses sit far below (~0.02-0.08) and
        // already-recognised heavy-tech maps (e.g. Poetic Edda, NEURO-CLOUD-9) sit far above (~0.30+)
        // where the calc already rates them correctly. So the buff peaks in the middle band and fades
        // to nothing at both extremes.
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

        public Technical(Mod[] mods)
            : base(mods)
        {
        }

        protected override double SkillMultiplier => 1.0;

        protected override double StrainDecayBase => strain_decay_base;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            var hitObject = (ManiaDifficultyHitObject)current;

            if (hitObject.DeltaTime < ChordEvaluator.CHORD_TOLERANCE_MS)
                return 0.0;

            double rhythmIrregularity = 0.0;

            if (previousDeltaTime > ChordEvaluator.CHORD_TOLERANCE_MS)
            {
                double ratio = hitObject.DeltaTime / previousDeltaTime;

                if (ratio > 1.0)
                    ratio = 1.0 / ratio;

                rhythmIrregularity = 1.0 - ratio;
            }

            double columnComplexity = 0.0;

            if (hitObject.Previous(0) is ManiaDifficultyHitObject previous && hitObject.Previous(1) is ManiaDifficultyHitObject previous2)
            {
                int previousDirection = previous.Column - previous2.Column;
                int currentDirection = hitObject.Column - previous.Column;

                if (previousDirection != 0 && currentDirection != 0 && Math.Sign(previousDirection) != Math.Sign(currentDirection))
                {
                    double coefficient = CrossColumnEvaluator.CoefficientSum(previous.Column, hitObject.Column, hitObject.PreviousHitObjects.Length);
                    columnComplexity += reversal_base_complexity + reversal_coefficient_multiplier * coefficient;
                }

                if (Math.Abs(currentDirection) >= 2)
                    columnComplexity += CrossColumnEvaluator.CoefficientAverage(previous.Column, hitObject.Column, hitObject.PreviousHitObjects.Length); // wide jump, averaged path scaled by sqrt(span)
            }

            double speedFactor = 1.0 / (hitObject.DeltaTime / 1000.0 + speed_factor_offset);

            previousDeltaTime = hitObject.DeltaTime;
            double complexity = Math.Max(rhythmIrregularity + columnComplexity, variety_floor * patternVariety(hitObject));
            double rhythmAmp = rhythmAmplifier(windowedIrregularity(rhythmIrregularity));

            return pattern_buff * complexity * speedFactor * technical_scale * rhythmAmp * hitObject.ManipulationFactor * hitObject.StaminaFactor;
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
            => 1.0 + rhythm_tech_buff * DifficultyCalculationUtils.BellCurve(windowedIrregularity, rhythm_tech_center, rhythm_tech_width);

        private double patternVariety(ManiaDifficultyHitObject hitObject)
        {
            int rhythmClass = (int)Math.Round(Math.Log(hitObject.DeltaTime) / Math.Log(variety_gap_log_base));
            int direction = hitObject.Previous(0) is ManiaDifficultyHitObject previous ? Math.Sign(hitObject.Column - previous.Column) : 0;

            recentShapes.Enqueue((rhythmClass, direction));

            while (recentShapes.Count > variety_window)
                recentShapes.Dequeue();

            int distinctShapes = recentShapes.Distinct().Count();

            return DifficultyCalculationUtils.Smoothstep(distinctShapes, variety_lo, variety_hi);
        }
    }
}
