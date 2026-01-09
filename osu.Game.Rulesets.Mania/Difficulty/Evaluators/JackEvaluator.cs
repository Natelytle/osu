// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

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

            double strainDifficulty = timeWeightFunc(maniaCurr.ColumnStrainTime);
            double gapMultiplier = 1;

            foreach (List<ManiaDifficultyHitObject> surroundingColumn in maniaCurr.SurroundingNotes)
            {
                strainDifficulty += evaluateColumnStamina(maniaCurr, surroundingColumn) * extra_column_strain_multiplier;
                gapMultiplier = DifficultyCalculationUtils.Norm(gap_multiplier_norm, gapMultiplier, evaluateColumnGapDifficulty(maniaCurr, surroundingColumn));
            }

            return strainDifficulty * gapMultiplier;
        }

        private static double evaluateColumnStamina(ManiaDifficultyHitObject maniaCurr, List<ManiaDifficultyHitObject> surroundingColumn)
        {
            double columnStaminaDifficulty = 0;

            foreach (ManiaDifficultyHitObject maniaSurr in surroundingColumn)
            {
                double offsetMultiplier = chordWeight(maniaSurr.StartTime, maniaCurr.StartTime);

                if (offsetMultiplier == 0)
                    continue;

                double surrObjStrainDifficulty = timeWeightFunc(maniaSurr.ColumnStrainTime) * offsetMultiplier;

                columnStaminaDifficulty = Math.Max(columnStaminaDifficulty, surrObjStrainDifficulty);
            }

            return columnStaminaDifficulty;
        }

        private static double evaluateColumnGapDifficulty(ManiaDifficultyHitObject maniaCurr, List<ManiaDifficultyHitObject> surroundingColumn)
        {
            double columnGapDifficulty = 0;

            foreach (ManiaDifficultyHitObject maniaSurr in surroundingColumn)
            {
                double offsetMultiplier = chordWeight(maniaSurr.StartTime, maniaCurr.StartTime);

                // Get the length of the current gap in terms of how many notes back it stretches.
                double gapNoteLength = maniaSurr.ColumnStrainTime > 0 ? Math.Clamp(maniaCurr.ColumnStrainTime / maniaSurr.ColumnStrainTime, 1, 3) - 1 : 0;

                // Let the value go to zero as the gap length increases further from 2, since we only reward a gap if the gap didn't exist at most 3 chords ago.
                gapNoteLength = Math.Min(gapNoteLength, 2 - (gapNoteLength + 1) / 2.0) * offsetMultiplier;

                columnGapDifficulty = Math.Max(columnGapDifficulty, gapNoteLength);
            }

            return columnGapDifficulty;
        }

        private static double columnDeltaTime(ManiaDifficultyHitObject maniaCurr, List<ManiaDifficultyHitObject> surroundingColumn)
        {
            double columnDeltaTime = double.PositiveInfinity;

            foreach (ManiaDifficultyHitObject maniaSurr in surroundingColumn)
            {
                double offsetDivisor = chordWeight(maniaSurr.StartTime, maniaCurr.StartTime);

                if (offsetDivisor == 0)
                    continue;

                // Divide by our offset to effectively inflate the delta time for this column.
                columnDeltaTime = Math.Max(columnDeltaTime, maniaSurr.StartTime / offsetDivisor);
            }

            return columnDeltaTime;
        }

        // Around 1 at 150ms (200bpm 1/2, 100bpm 1/4)
        private static double timeWeightFunc(double columnDeltaTime) => 80 / Math.Pow(columnDeltaTime, 0.88);

        private static double chordWeight(double otherDeltaTime, double currentDeltaTime) => DifficultyCalculationUtils.SmoothstepBellCurve(otherDeltaTime, currentDeltaTime, grace_tolerance);
    }
}
