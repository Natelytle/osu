// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Editor;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Editor
{
    internal partial class ManiaDifficultyEvaluatorInspector : DifficultyEvaluatorInspector
    {
        protected override Evaluator[] Evaluators => [
            new("Jack", obj => JackEvaluator.EvaluateDifficultyOf(obj)),
            new("Hand Adjusted Delta", obj => JackEvaluator.HandAdjustedDelta((ManiaDifficultyHitObject)obj)),
            new("Gap Multiplier", obj => JackEvaluator.GapMultiplier((ManiaDifficultyHitObject)obj)),
        ];
    }
}
