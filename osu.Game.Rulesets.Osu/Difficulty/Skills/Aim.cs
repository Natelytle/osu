// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuStrainSkill
    {
        public readonly bool IncludeSliders;

        public Aim(Mod[] mods, bool includeSliders)
            : base(mods)
        {
            IncludeSliders = includeSliders;
        }

        private readonly Queue<DifficultyPoint> aimDifficultyPoints = new Queue<DifficultyPoint>();
        private readonly Queue<DifficultyPoint> speedDifficultyPoints = new Queue<DifficultyPoint>();

        private double skillMultiplierAim => 25.0;
        private double skillMultiplierSpeed => 1.4;
        private double skillMultiplierTotal => 1.0;
        private double meanExponent => 1.2;

        private readonly List<double> sliderStrains = new List<double>();

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) =>
            DifficultyCalculationUtils.Norm(meanExponent,
                OsuStrainUtils.GetStrainValueOf(aimDifficultyPoints, time, 0.15),
                OsuStrainUtils.GetStrainValueOf(speedDifficultyPoints, time, 0.3)) * skillMultiplierTotal;

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            double aimDifficulty = AimEvaluator.EvaluateDifficultyOf(current, IncludeSliders);
            double speedDifficulty = SpeedAimEvaluator.EvaluateDifficultyOf(current);

            if (Mods.Any(m => m is OsuModTouchDevice))
            {
                aimDifficulty = Math.Pow(aimDifficulty, 0.8);
                speedDifficulty = Math.Pow(speedDifficulty, 0.95);
            }

            if (Mods.Any(m => m is OsuModRelax))
            {
                speedDifficulty *= 0.0;
            }

            DifficultyPoint aimDifficultyPoint = new DifficultyPoint
            {
                Difficulty = aimDifficulty * skillMultiplierAim,
                Time = ((OsuDifficultyHitObject)current).StartTime,
                DeltaTime = ((OsuDifficultyHitObject)current).StartTime - aimDifficultyPoints.LastOrDefault().Time
            };

            aimDifficultyPoints.Enqueue(aimDifficultyPoint);

            DifficultyPoint speedDifficultyPoint = new DifficultyPoint
            {
                Difficulty = speedDifficulty * skillMultiplierSpeed,
                Time = ((OsuDifficultyHitObject)current).StartTime,
                DeltaTime = ((OsuDifficultyHitObject)current).StartTime - speedDifficultyPoints.LastOrDefault().Time
            };

            speedDifficultyPoints.Enqueue(speedDifficultyPoint);

            double currentAimStrain = OsuStrainUtils.GetStrainValueOf(aimDifficultyPoints, current.StartTime, 0.15);
            double currentSpeedStrain = OsuStrainUtils.GetStrainValueOf(speedDifficultyPoints, current.StartTime, 0.3);

            double totalStrain = DifficultyCalculationUtils.Norm(meanExponent, currentAimStrain, currentSpeedStrain) * skillMultiplierTotal;

            if (current.BaseObject is Slider)
                sliderStrains.Add(totalStrain);

            return totalStrain;
        }

        public double GetDifficultSliders()
        {
            if (sliderStrains.Count == 0)
                return 0;

            double maxSliderStrain = sliderStrains.Max();

            if (maxSliderStrain == 0)
                return 0;

            return sliderStrains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxSliderStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders(double difficultyValue)
            => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, difficultyValue);
    }
}
