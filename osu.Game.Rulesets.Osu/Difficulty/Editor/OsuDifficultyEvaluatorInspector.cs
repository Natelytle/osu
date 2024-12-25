// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Editor;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;

namespace osu.Game.Rulesets.Osu.Difficulty.Editor
{
    internal partial class OsuDifficultyEvaluatorInspector : DifficultyEvaluatorInspector
    {
        protected override Evaluator[] Evaluators => [
            new("Snap Aim", obj => SnapAimEvaluator.EvaluateDifficultyOf(obj)),
            new("Distance (Snap)", SnapAimEvaluator.EvaluateDistanceBonus),
            new("Agility (Snap)", SnapAimEvaluator.EvaluateAgilityBonus),
            new("Angle (Snap)", SnapAimEvaluator.EvaluateAngleBonus),
            new("Vel Change (Snap)", SnapAimEvaluator.EvaluateVelocityChangeBonus),
            new("Flow Aim", obj => FlowAimEvaluator.EvaluateDifficultyOf(obj)),
            new("Speed", SpeedEvaluator.EvaluateDifficultyOf),
            new("Rhythm", RhythmEvaluator.EvaluateDifficultyOf),
            new("Flashlight (hidden = false)", obj => FlashlightEvaluator.EvaluateDifficultyOf(obj, false)),
            new("Flashlight (hidden = true)", obj => FlashlightEvaluator.EvaluateDifficultyOf(obj, true)),
        ];
    }
}
