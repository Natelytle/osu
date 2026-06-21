// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class CoordinationEvaluator
    {
        private const double coordination_scale = 1.14529;

        private const double chord_load_per_extra_column = 0.9;
        private const double chordjack_nerf = 0.45397;
        private const double chord_speed_threshold_ms = 140.625;

        private const double held_long_note_weight = 0.01003;
        private const double held_speed_factor_offset = 0.08;

        private const double boundary_scale = 1.30;
        private const double boundary_min_delta_ms = 35.0;
        private const double boundary_activity_window_ms = 450.0;

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject hitObject)
        {
            double crossColumnStrain = boundaryPressure(hitObject);

            int chordSize = ChordEvaluator.Size(hitObject);

            double lastStartTime = hitObject.LastStartTimeInColumn(hitObject.Column);
            double columnDelta = double.IsNegativeInfinity(lastStartTime) ? double.PositiveInfinity : hitObject.StartTime - lastStartTime;
            bool isChordjack = chordSize >= 2 && columnDelta <= JackEvaluator.JACK_WINDOW_MS;
            double chordSpeedFactor = !double.IsPositiveInfinity(columnDelta)
                ? Math.Clamp(chord_speed_threshold_ms / columnDelta, 0.1, 2.0)
                : 1.0;
            // A chordjack repeats a chord on columns it just used (e.g. 4-2-2-4), already paid for by Jack,
            // so the dampening here only targets degenerate sustained full/near-full chord spam.
            double chordLoad = chordSize >= 2
                ? chord_load_per_extra_column * (chordSize - 1) * ChordEvaluator.FullChordDampen(hitObject, hitObject.PreviousHitObjects.Length, columnDelta)
                  * ChordEvaluator.NearFullChordDampen(hitObject, hitObject.PreviousHitObjects.Length, columnDelta)
                  * (isChordjack ? chordjack_nerf : 1.0) * chordSpeedFactor
                : 0.0;

            int heldColumns = hitObject.ConcurrentlyHeldColumns(ChordEvaluator.CHORD_TOLERANCE_MS);
            double heldSpeedFactor = hitObject.DeltaTime >= ChordEvaluator.CHORD_TOLERANCE_MS ? 1.0 / (hitObject.DeltaTime / 1000.0 + held_speed_factor_offset) : 1.0;
            double heldNoteLoad = held_long_note_weight * Math.Sqrt(heldColumns) * heldSpeedFactor;

            return (crossColumnStrain * coordination_scale + chordLoad + heldNoteLoad) * hitObject.ManipulationFactor;
        }

        private static double boundaryPressure(ManiaDifficultyHitObject hitObject)
        {
            int column = hitObject.Column;
            int totalColumns = hitObject.PreviousHitObjects.Length;
            double now = hitObject.StartTime;
            double total = 0.0;

            if (column > 0)
                total += oneBoundaryPressure(hitObject, column, column - 1, totalColumns, now);

            if (column < totalColumns - 1)
                total += oneBoundaryPressure(hitObject, column + 1, column + 1, totalColumns, now);

            return total * TrillEvaluator.TrillFactor(hitObject);
        }

        private static double oneBoundaryPressure(ManiaDifficultyHitObject hitObject, int boundaryIndex, int otherColumn, int totalColumns, double now)
        {
            double otherLast = hitObject.LastStartTimeInColumn(otherColumn);

            if (double.IsNegativeInfinity(otherLast))
                return 0.0;

            double rawDeltaMs = now - otherLast;

            if (rawDeltaMs < ChordEvaluator.CHORD_TOLERANCE_MS)
                return 0.0;

            double deltaSeconds = rawDeltaMs / 1000.0;
            double intensity = boundary_scale / (deltaSeconds + boundary_min_delta_ms / 1000.0);
            double coefficient = CrossColumnEvaluator.Coefficient(boundaryIndex, totalColumns);
            bool otherActive = rawDeltaMs <= boundary_activity_window_ms;

            return intensity * coefficient * (otherActive ? 1.0 : (1.0 - coefficient));
        }
    }
}
