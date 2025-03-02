// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class CrossColumnEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            var currNote = (ManiaDifficultyHitObject)current;

            int currColumn = currNote.Column;

            var prevNotes = currNote.PreviousHitObjects;

            double totalDifficulty = 0;

            for (int i = 0; i < prevNotes.Length; i++)
            {
                var prevNote = prevNotes[i];

                if (i == currColumn || prevNote is null) continue;

                double noteStrainTime = Math.Max(currNote.StartTime - prevNote.StartTime, 25);

                double currDifficulty = 450 / Math.Pow(noteStrainTime, 2);

                totalDifficulty += currDifficulty;
            }

            return totalDifficulty;
        }
    }
}
