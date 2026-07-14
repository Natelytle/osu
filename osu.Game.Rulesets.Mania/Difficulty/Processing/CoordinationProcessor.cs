// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Processing
{
    public class CoordinationProcessor : DifficultyProcessor
    {
        protected override double ChordStrainDecay => 0.52909;

        protected override double CalculateNoteDifficulty(ManiaDifficultyHitObject current)
        {
            return CoordinationEvaluator.EvaluateDifficultyOf(current);
        }
    }
}
