// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class ReleaseEvaluator
    {
        private const double max_long_note_duration_ms = 1000.0;

        private const double long_note_gate_midpoint_ms = 110.90068;
        private const double long_note_gate_slope = 0.07;

        private const double long_note_base_load = 0.42;
        private const double long_note_duration_load = 0.90;

        // Releases very close together are harder to time apart.
        private const double overlapping_release_slope = 0.1;
        private const double overlapping_release_offset_ms = 30.0;

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject hitObject)
        {
            double load = 0.0;

            if (hitObject.BaseObject is HoldNote)
            {
                double duration = Math.Min(hitObject.EndTime - hitObject.StartTime, max_long_note_duration_ms);
                double longNoteGate = DifficultyCalculationUtils.Logistic(duration, long_note_gate_midpoint_ms, long_note_gate_slope);

                load += (long_note_base_load + long_note_duration_load * (duration / 1000.0)) * longNoteGate;

                double closestRelease = double.PositiveInfinity;

                for (int otherColumn = 0; otherColumn < hitObject.PreviousHitObjects.Length; otherColumn++)
                {
                    if (otherColumn == hitObject.Column)
                        continue;

                    if (Math.Abs(hitObject.LastStartTimeInColumn(otherColumn) - hitObject.StartTime) <= ChordEvaluator.CHORD_TOLERANCE_MS)
                        continue;

                    double otherEndTime = hitObject.LastEndTimeInColumn(otherColumn);

                    if (otherEndTime > hitObject.StartTime)
                        closestRelease = Math.Min(closestRelease, Math.Abs(hitObject.EndTime - otherEndTime));
                }

                if (!double.IsPositiveInfinity(closestRelease))
                    load += longNoteGate / (1.0 + Math.Exp(overlapping_release_slope * (closestRelease - overlapping_release_offset_ms)));
            }

            return load;
        }
    }
}
