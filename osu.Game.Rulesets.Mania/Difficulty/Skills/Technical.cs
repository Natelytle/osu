// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Technical : ManiaSkill
    {
        private const double strain_decay_base = 0.06696;

        private const double speed_factor_offset = 0.050;

        // A reversal at a boundary scores a base amount plus a multiple of how hard that boundary is to cross.
        private const double reversal_base_complexity = 0.6;
        private const double reversal_coefficient_multiplier = 2.0;

        private const double pattern_buff = 0.69740;
        private const double technical_scale = 1.49964;

        private double previousDeltaTime = -1.0;

        public Technical(Mod[] mods, int totalColumns)
            : base(mods, totalColumns)
        {
        }

        protected override double StrainDecayBase => strain_decay_base;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            var hitObject = (ManiaDifficultyHitObject)current;

            if (hitObject.DeltaTime < CHORD_TOLERANCE)
            {
                UpdateColumnState(hitObject);
                return 0.0;
            }

            double rhythmIrregularity = 0.0;

            if (previousDeltaTime > CHORD_TOLERANCE)
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
                    double coefficient = CrossColumnCoefficientSum(previous.Column, hitObject.Column);
                    columnComplexity += reversal_base_complexity + reversal_coefficient_multiplier * coefficient;
                }

                if (Math.Abs(currentDirection) >= 2)
                    columnComplexity += CrossColumnCoefficientSum(previous.Column, hitObject.Column); // wide jump, weighted by path
            }

            double speedFactor = 1.0 / (hitObject.DeltaTime / 1000.0 + speed_factor_offset);

            previousDeltaTime = hitObject.DeltaTime;
            UpdateColumnState(hitObject);

            return pattern_buff * (rhythmIrregularity + columnComplexity) * speedFactor * technical_scale;
        }
    }
}
