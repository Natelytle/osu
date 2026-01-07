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
        private static readonly double extra_column_strain_multiplier = 0.04;
        private static readonly double gap_multiplier_norm = 2.0;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject maniaCurr = (ManiaDifficultyHitObject)current;

            double strainDifficulty = timeWeightFunc(maniaCurr.ColumnStrainTime);
            double gapMultiplier = 1;

            foreach (List<ManiaDifficultyHitObject> surroundingColumn in maniaCurr.SurroundingNotes)
            {
                double columnStaminaDifficulty = 0;
                double columnGapDifficulty = 0;

                foreach (ManiaDifficultyHitObject maniaSurr in surroundingColumn)
                {
                    // We adjust for the offset between the surrounding note and the current note.
                    // 0 when the offset is equal to strainTime, so we don't pick up difficulty from the next or prev chord.
                    double offsetMultiplier = DifficultyCalculationUtils.SmoothstepBellCurve(maniaSurr.StartTime, maniaCurr.StartTime, maniaCurr.ColumnStrainTime / 2.0);

                    if (offsetMultiplier == 0)
                        continue;

                    double surrObjStrainDifficulty = timeWeightFunc(maniaSurr.ColumnStrainTime) * offsetMultiplier;

                    columnStaminaDifficulty = Math.Max(columnStaminaDifficulty, surrObjStrainDifficulty);

                    // Account for gaps in chords by checking the ratio of columnStrainTimes.
                    // We only care if the current note has the gap, don't reward for others having gaps.
                    double surrObjUnevenness = maniaSurr.ColumnStrainTime > 0 ? Math.Clamp(maniaCurr.ColumnStrainTime / maniaSurr.ColumnStrainTime, 1, 3) - 1 : 0;

                    // Let the value go to zero as the gap length increases further from 2, since we only reward a gap if the gap didn't exist 2 chords ago.
                    surrObjUnevenness = Math.Min(surrObjUnevenness, 2 - surrObjUnevenness) * offsetMultiplier;

                    columnGapDifficulty = Math.Max(columnGapDifficulty, surrObjUnevenness);
                }

                // Harsh multiplier
                strainDifficulty += columnStaminaDifficulty * extra_column_strain_multiplier;
                gapMultiplier = DifficultyCalculationUtils.Norm(gap_multiplier_norm, gapMultiplier, columnGapDifficulty);
            }

            return strainDifficulty * gapMultiplier;
        }

        // Around 1 at 150ms (200bpm 1/2, 100bpm 1/4)
        private static double timeWeightFunc(double columnDeltaTime) => 82 / Math.Pow(columnDeltaTime, 0.88);
    }
}
