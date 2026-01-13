// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;

namespace osu.Game.Rulesets.Mania.Difficulty.Editor
{
    internal static class ManiaEvaluatorDebugger
    {
        public record Evaluator(string Name, Type Type);

        public static Evaluator[] Evaluators { get; } = [
            new Evaluator("Jack", typeof(JackEvaluator)),
            // new Evaluator("Stream", typeof(StreamEvaluator))
        ];

        public static void DebugObject(Evaluator evaluator, DifficultyHitObject obj)
        {
            if (!Debugger.IsAttached)
                throw new InvalidOperationException("Please run osu!lazer with a debugger attached.");

            if (evaluator.Type == typeof(JackEvaluator))
            {
                Debugger.Break();
                JackEvaluator.EvaluateDifficultyOf(obj);
            }
            // else if (evaluator.Type == typeof(StreamEvaluator))
            // {
            //     Debugger.Break();
            //     StreamEvaluator.EvaluateDifficultyOf(obj);
            // }
        }
    }
}
