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
    /// <summary>
    /// The evaluator that deals with patterns of notes that require you to press the same column in quick succession.
    /// Includes jack, minijack, and chordjacks.
    /// </summary>
    public class JackEvaluator
    {
        private const double gap_multiplier_norm = 2.0;
        private const double ics_tolerance_start_ms = 80;
        private const double ics_tolerance_end_ms = 280;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject maniaCurr = (ManiaDifficultyHitObject)current;
            double handDelta = HandAdjustedDelta(maniaCurr);

            double strainDifficulty = timeWeightFunc(handDelta);
            double gapMultiplier = GapMultiplier(maniaCurr);

            return strainDifficulty * gapMultiplier;
        }

        public static double GapMultiplier(ManiaDifficultyHitObject maniaCurr)
        {
            double totalGapMultiplier = 1;

            foreach (List<ManiaDifficultyHitObject> surroundingColumn in maniaCurr.SurroundingNotes)
            {
                double columnGapMultiplier = 0;

                foreach (ManiaDifficultyHitObject maniaSurr in surroundingColumn)
                {
                    double offsetMultiplier = ManiaDifficultyUtils.ChordProbability(maniaCurr, maniaSurr);

                    // Get the length of the current gap in terms of how many notes back it stretches.
                    double gapNoteLength = maniaSurr.ColumnStrainTime > 0 ? Math.Clamp(maniaCurr.ColumnStrainTime / maniaSurr.ColumnStrainTime, 1, 3) - 1 : 0;

                    // Let the value go to zero as the gap length increases further from 2, since we only reward a gap if the gap didn't exist at most 3 chords ago.
                    columnGapMultiplier = Math.Min(gapNoteLength, 2 - (gapNoteLength + 1) / 2.0) * offsetMultiplier;

                    // Nerf the multiplier if the hit window is small enough to allow ICS (hitting all columns)
                    columnGapMultiplier *= 1 - DifficultyCalculationUtils.Smoothstep(maniaSurr.ColumnStrainTime, maniaCurr.MissWindow + ics_tolerance_start_ms / 10, maniaCurr.MissWindow + ics_tolerance_end_ms / 10);
                }

                totalGapMultiplier = DifficultyCalculationUtils.Norm(gap_multiplier_norm, totalGapMultiplier, columnGapMultiplier);
            }

            return totalGapMultiplier;
        }

        public static double HandAdjustedDelta(ManiaDifficultyHitObject maniaCurr)
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
                    double columnDeltaTime = column == maniaCurr.Column ? maniaCurr.ColumnStrainTime : getColumnJackDeltaTime(maniaCurr, surroundingColumn);

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
            double scaledMin = Math.Min(minDeltaTime * Math.Sqrt(mean / minDeltaTime), minDeltaTime * 1.5);

            return Math.Min(scaledMin, maniaCurr.ColumnStrainTime);
        }

        private static double getColumnJackDeltaTime(ManiaDifficultyHitObject maniaCurr, List<ManiaDifficultyHitObject> column)
        {
            double columnDeltaTime = 1000;

            foreach (ManiaDifficultyHitObject maniaSurr in column)
            {
                double offsetDivisor = 1;

                ManiaDifficultyHitObject? surrPrev = maniaSurr.PrevInColumn(0);

                if (surrPrev is not null)
                {
                    offsetDivisor = 1 - ManiaDifficultyUtils.ChordProbability(maniaCurr, surrPrev);
                }

                if (offsetDivisor == 0)
                    continue;

                // Divide by our offset to effectively inflate the delta time if you're likely to play the previous column note in the current chord.
                columnDeltaTime = Math.Min(columnDeltaTime, maniaSurr.ColumnStrainTime / offsetDivisor);
            }

            return columnDeltaTime;
        }

        // Around 1 at 150ms (200bpm 1/2, 100bpm 1/4)
        private static double timeWeightFunc(double columnDeltaTime) => 80 / Math.Pow(columnDeltaTime, 0.88);
    }
}
