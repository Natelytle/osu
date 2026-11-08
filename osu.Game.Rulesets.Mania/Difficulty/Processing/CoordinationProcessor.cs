// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
namespace osu.Game.Rulesets.Mania.Difficulty.Processing
{
    public class CoordinationProcessor : IDifficultyProcessor
    {
        private const double strain_decay_base = 0.53;

        public double CurrentStrain { get; private set; }

        public void ProcessStrainFor(DifficultyHitObject current, ManiaDifficultyConstants constants)
        {
            CurrentStrain *= Math.Pow(constants.CoordinationStrainDecayBase, current.DeltaTime / 1000);

            CurrentStrain += CoordinationEvaluator.EvaluateDifficultyOf((ManiaDifficultyHitObject)current, constants);
        }
    }
}
