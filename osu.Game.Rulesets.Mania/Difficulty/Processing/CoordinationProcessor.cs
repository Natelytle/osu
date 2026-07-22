// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Processing
{
    public class CoordinationProcessor : IDifficultyProcessor
    {
        private const double strain_decay_base = 0.52909;

        public double CurrentStrain { get; private set; }

        public void ProcessStrainFor(DifficultyHitObject current)
        {
            CurrentStrain *= Math.Pow(strain_decay_base, current.DeltaTime / 1000);

            CurrentStrain += CoordinationEvaluator.EvaluateDifficultyOf((ManiaDifficultyHitObject)current);
        }

        public AccuracyDifficulties TransformStrainToAccuracyDifficulties(double strain)
        {
            AccuracyValueMultipliers multipliers = new AccuracyValueMultipliers
            {
                MultiplierAtSS = 1.22,
                MultiplierAt99_5 = 1.15,
                MultiplierAt99 = 1.1,
                MultiplierAt98 = 1.00,
                MultiplierAt95 = 0.94,
                MultiplierAt90 = 0.83,
                MultiplierAt85 = 0.72,
                MultiplierAt80 = 0.32
            };

            return new AccuracyDifficulties(strain, multipliers);
        }
    }
}
