// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class ReleaseEvaluator
    {
        private const double difficulty_multiplier = 1.0;

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            ManiaDifficultyHitObject? nextTail = current.NextTail(0);

            if (nextTail is null || nextTail.EndTime - current.EndTime == 0)
                return 0;

            double currentDifficulty = evaluateIndividualDifficultyOf(current);
            double nextDifficulty = evaluateIndividualDifficultyOf(nextTail);

            double leniency = ManiaDifficultyUtils.CalculateHitLeniency(current.GreatHitWindow);

            // Calculate release difficulty based on timing and individual difficulties
            double releaseTimeDelta = nextTail.EndTime - current.EndTime;
            double releaseDifficulty = 2.5 * (1.0 / Math.Sqrt(releaseTimeDelta)) * (1000.0 / leniency);

            // Multiplier for the individual difficulties of these notes.
            releaseDifficulty *= 1.0 + 0.8 * (currentDifficulty + nextDifficulty);

            // Divide release difficulty by the number of notes within a +-500ms window (probably to nerf high pressing difficulty situations)
            double localNoteCount = getHeadCountWithinTimeWindow(500, current);
            releaseDifficulty *= 35.0 / (localNoteCount + 8.0);

            return releaseDifficulty * difficulty_multiplier;
        }

        private static double evaluateIndividualDifficultyOf(ManiaDifficultyHitObject current)
        {
            var nextHeadInColumn = current.NextHeadInColumn(0);

            double holdDuration = Math.Abs(current.EndTime - current.StartTime - 80.0);

            // Handle case where there's no next note in the column
            double releaseToNextNote;

            if (nextHeadInColumn is not null)
            {
                releaseToNextNote = Math.Abs(nextHeadInColumn.StartTime - current.EndTime - 80.0);
            }
            else
            {
                // If no next note, use a default large value or base it on hold duration
                releaseToNextNote = Math.Max(1000.0, holdDuration * 2.0);
            }

            double leniency = ManiaDifficultyUtils.CalculateHitLeniency(current.GreatHitWindow);

            double holdDifficultyComponent = holdDuration / leniency;
            double timingDifficultyComponent = releaseToNextNote / leniency;

            double lh = DifficultyCalculationUtils.Logistic(holdDifficultyComponent, 0.75, 5.0);
            double lt = DifficultyCalculationUtils.Logistic(timingDifficultyComponent, 0.75, 5.0);

            return 2.0 * (lh * lt) / (lh + lt);
        }

        private static double getHeadCountWithinTimeWindow(double window, ManiaDifficultyHitObject current)
        {
            int count = 1;

            var previous = current.PrevHead(0);

            while (previous is not null && previous.StartTime > current.ActualTime - window)
            {
                count++;

                previous = previous.PrevHead(0);
            }

            var next = current.NextHead(0);

            while (next is not null && next.StartTime < current.ActualTime + window)
            {
                count++;

                next = next.NextHead(0);
            }

            return count;
        }
    }
}
