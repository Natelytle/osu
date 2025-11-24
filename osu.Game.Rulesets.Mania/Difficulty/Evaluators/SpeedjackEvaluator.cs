// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class SpeedjackEvaluator
    {
        private static double multiplier => 1.0;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.Previous(0) is null)
                return 0;

            ManiaDifficultyHitObject currObj = (ManiaDifficultyHitObject)current;

            var currentChord = currObj.CurrentChord;
            var previousChord = currObj.PreviousChord(0);

            if (previousChord == null)
                return 0;

            int sharedColumns = currentChord.Notes
                                            .Select(n => n.Column)
                                            .Intersect(previousChord.Notes.Select(n => n.Column))
                                            .Count();

            if (sharedColumns == 0)
                return 0;

            double density = Math.Min(1, (double)sharedColumns / currentChord.Notes.Count);
            double baseValue = bpmToRatingCurve(currentChord.QuarterBpm) * Math.Pow(1 - density, 1.3);

            return baseValue * multiplier;
        }

        private static double bpmToRatingCurve(double bpm) => Math.Pow(2.0, bpm / 46.5);
    }
}
