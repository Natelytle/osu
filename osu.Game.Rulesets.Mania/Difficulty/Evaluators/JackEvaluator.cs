// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class JackEvaluator
    {
        private const double extra_column_strain_multiplier = 0.04;
        private const double gap_multiplier_norm = 2.0;
        private const double grace_tolerance = 50;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject maniaCurr = (ManiaDifficultyHitObject)current;
            double handDelta = meanHandDelta(maniaCurr);

            double strainDifficulty = timeWeightFunc(handDelta);
            double gapMultiplier = 1;

            foreach (List<ManiaDifficultyHitObject> surroundingColumn in maniaCurr.SurroundingNotes)
            {
                gapMultiplier = DifficultyCalculationUtils.Norm(gap_multiplier_norm, gapMultiplier, evaluateColumnGapDifficulty(maniaCurr, surroundingColumn));
            }

            return strainDifficulty * gapMultiplier;
        }

        private static double evaluateColumnGapDifficulty(ManiaDifficultyHitObject maniaCurr, List<ManiaDifficultyHitObject> surroundingColumn)
        {
            double columnGapDifficulty = 0;

            foreach (ManiaDifficultyHitObject maniaSurr in surroundingColumn)
            {
                double offsetMultiplier = chordDistanceAwayValue(maniaCurr, maniaSurr);

                // Get the length of the current gap in terms of how many notes back it stretches.
                double gapNoteLength = maniaSurr.ColumnStrainTime > 0 ? Math.Clamp(maniaCurr.ColumnStrainTime / maniaSurr.ColumnStrainTime, 1, 3) - 1 : 0;

                // Let the value go to zero as the gap length increases further from 2, since we only reward a gap if the gap didn't exist at most 3 chords ago.
                gapNoteLength = Math.Min(gapNoteLength, 2 - (gapNoteLength + 1) / 2.0) * offsetMultiplier;

                columnGapDifficulty = Math.Max(columnGapDifficulty, gapNoteLength);
            }

            return columnGapDifficulty;
        }

        private static double meanHandDelta(ManiaDifficultyHitObject maniaCurr)
        {
            int column = 0;
            double minDeltaTime = double.PositiveInfinity;

            double deltaTimeSum = 0;
            double columnInfluenceSum = 0;

            foreach (List<ManiaDifficultyHitObject> surroundingColumn in maniaCurr.SurroundingNotes)
            {
                Hand columnHandedness = Handedness.GetHandednessOf(column, maniaCurr.SurroundingNotes.Length);
                double columnInfluence = Handedness.GetHandednessFactorOf(maniaCurr.NoteHandedness, columnHandedness);

                if (columnInfluence is not 0)
                {
                    double columnDeltaTime = getColumnJackDeltaTime(maniaCurr, surroundingColumn);

                    if (columnHandedness is not Hand.Ambiguous)
                        minDeltaTime = Math.Min(minDeltaTime, columnDeltaTime);

                    deltaTimeSum += columnDeltaTime;
                    columnInfluenceSum += columnInfluence;
                }

                column++;
            }

            if (columnInfluenceSum == 0)
                return double.PositiveInfinity;

            double mean = deltaTimeSum / columnInfluenceSum;

            double scaled = minDeltaTime * Math.Sqrt(mean / minDeltaTime);

            // We don't want to inflate the amount too much if say, a note were 50000000 years ago, so we clamp it and adjust scaling
            return Math.Max(scaled, minDeltaTime * 1.5);
        }

        private static double getColumnJackDeltaTime(ManiaDifficultyHitObject maniaCurr, List<ManiaDifficultyHitObject> column)
        {
            double columnDeltaTime = 1000;

            foreach (ManiaDifficultyHitObject maniaSurr in column)
            {
                double timeDifference = maniaCurr.StartTime - maniaSurr.StartTime;
                double offsetDivisor = chordDistanceAwayValue(maniaCurr, maniaSurr);

                if (offsetDivisor == 0 || timeDifference < 0)
                    continue;

                var surrNext = maniaSurr.NextInColumn(0);

                if (surrNext is not null)
                {
                    // Since this is jack deltaTime, we need a next note
                    timeDifference = Math.Max(timeDifference, surrNext.ColumnStrainTime);

                    // Divide by our offset to effectively inflate the delta time if you're likely to treat it as part of the current chord.
                    columnDeltaTime = Math.Min(columnDeltaTime, timeDifference / offsetDivisor);
                }
            }

            return columnDeltaTime;
        }

        // Around 1 at 150ms (200bpm 1/2, 100bpm 1/4)
        private static double timeWeightFunc(double columnDeltaTime) => 80 / Math.Pow(columnDeltaTime, 0.88);

        private static double chordDistanceAwayValue(ManiaDifficultyHitObject maniaCurr, ManiaDifficultyHitObject maniaSurr)
        {
            // First, we check to make sure there's no note in this surrounding column between us and the current note in time.
            ManiaDifficultyHitObject? surrNext = maniaSurr.NextInColumn(0);

            if (surrNext is not null && surrNext.StartTime <= maniaCurr.StartTime)
                return 1;

            // If not, we weight it by how close our notes are in time.
            return DifficultyCalculationUtils.SmoothstepBellCurve(maniaCurr.StartTime, maniaSurr.StartTime, grace_tolerance);
        }
    }
}
