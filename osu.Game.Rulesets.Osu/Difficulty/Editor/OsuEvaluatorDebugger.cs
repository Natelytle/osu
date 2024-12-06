// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;

namespace osu.Game.Rulesets.Osu.Difficulty.Editor
{
    internal static class OsuEvaluatorDebugger
    {
        public record Evaluator(string Name, Type Type);

        public static Evaluator[] Evaluators { get; } = [
            new Evaluator("Snap Aim", typeof(SnapAimEvaluator)),
            new Evaluator("Flow Aim", typeof(FlowAimEvaluator)),
            new Evaluator("Speed", typeof(SpeedEvaluator)),
            new Evaluator("Rhythm", typeof(RhythmEvaluator)),
            new Evaluator("Flashlight", typeof(FlashlightEvaluator))
        ];

        public static void DebugObject(Evaluator evaluator, DifficultyHitObject obj)
        {
            if (!Debugger.IsAttached)
                throw new InvalidOperationException("Please run osu!lazer with a debugger attached.");

            if (evaluator.Type == typeof(SnapAimEvaluator))
            {
                Debugger.Break();
                SnapAimEvaluator.EvaluateDifficultyOf(obj);
            }
            else if (evaluator.Type == typeof(FlowAimEvaluator))
            {
                Debugger.Break();
                FlowAimEvaluator.EvaluateDifficultyOf(obj);
            }
            else if (evaluator.Type == typeof(SpeedEvaluator))
            {
                Debugger.Break();
                SpeedEvaluator.EvaluateDifficultyOf(obj);
            }
            else if (evaluator.Type == typeof(RhythmEvaluator))
            {
                Debugger.Break();
                RhythmEvaluator.EvaluateDifficultyOf(obj);
            }
            else if (evaluator.Type == typeof(FlashlightEvaluator))
            {
                Debugger.Break();
                bool hidden = false;
                FlashlightEvaluator.EvaluateDifficultyOf(obj, hidden);
            }
        }
    }
}
