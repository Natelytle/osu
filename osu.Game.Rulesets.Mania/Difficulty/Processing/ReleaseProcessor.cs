// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Processing
{
    public class ReleaseProcessor
    {
        private const double strain_decay_base = 0.89647;

        private double currentStrain;

        public double ProcessStrainFor(DifficultyHitObject current)
        {
            currentStrain *= DiffUtils.Pow(strain_decay_base, current.DeltaTime / 1000);

            currentStrain += ReleaseEvaluator.EvaluateDifficultyOf((ManiaDifficultyHitObject)current);

            return currentStrain;
        }
    }
}
