// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class CoordinationEvaluator
    {
        private const double cross_column_rate_offset = 0.045;
        private const double coordination_scale = 1.14529;

        private const double chord_load_per_extra_column = 0.9;
        private const double chordjack_nerf = 0.45397;
        private const double chord_speed_threshold_ms = 140.625;

        private const double held_long_note_weight = 0.01003;
        private const double held_speed_factor_offset = 0.08;

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject hitObject)
        {
            double crossColumnStrain = 0.0;

            if (hitObject.Previous(0) is ManiaDifficultyHitObject previous
                && previous.Column != hitObject.Column
                && hitObject.DeltaTime >= ChordEvaluator.CHORD_TOLERANCE_MS)
            {
                double coefficient = CrossColumnEvaluator.CoefficientSum(previous.Column, hitObject.Column, hitObject.PreviousHitObjects.Length);
                crossColumnStrain = coefficient / (hitObject.DeltaTime / 1000.0 + cross_column_rate_offset);
                crossColumnStrain *= TrillEvaluator.TrillFactor(hitObject);
            }

            int chordSize = ChordEvaluator.Size(hitObject);

            double lastStartTime = hitObject.LastStartTimeInColumn(hitObject.Column);
            double columnDelta = double.IsNegativeInfinity(lastStartTime) ? double.PositiveInfinity : hitObject.StartTime - lastStartTime;
            bool isChordjack = chordSize >= 2 && columnDelta <= JackEvaluator.JACK_WINDOW_MS;
            double chordSpeedFactor = !double.IsPositiveInfinity(columnDelta)
                ? Math.Clamp(chord_speed_threshold_ms / columnDelta, 0.1, 2.0)
                : 1.0;
            double chordLoad = chordSize >= 2 ? chord_load_per_extra_column * (chordSize - 1) * (isChordjack ? chordjack_nerf : 1.0) * chordSpeedFactor : 0.0;

            int heldColumns = hitObject.ConcurrentlyHeldColumns(ChordEvaluator.CHORD_TOLERANCE_MS);
            double heldSpeedFactor = hitObject.DeltaTime >= ChordEvaluator.CHORD_TOLERANCE_MS ? 1.0 / (hitObject.DeltaTime / 1000.0 + held_speed_factor_offset) : 1.0;
            double heldNoteLoad = held_long_note_weight * Math.Sqrt(heldColumns) * heldSpeedFactor;

            return crossColumnStrain * coordination_scale + chordLoad + heldNoteLoad;
        }
    }
}
