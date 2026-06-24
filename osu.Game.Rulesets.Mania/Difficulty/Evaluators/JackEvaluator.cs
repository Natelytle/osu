// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class JackEvaluator
    {
        public const double JACK_WINDOW_MS = 350.0;

        private const double jack_rate_offset = 0.060;

        private const double chordjack_buff = 0.17460;
        private const double chordjack_multiplier_minimum = 0.1;
        private const double chordjack_nerf = 0.45397;
        private const double chord_speed_threshold_ms = 140.625;

        private const double jack_speed_buff = 0.70000;
        private const double jack_speed_buff_midpoint = 5.0;
        private const double jack_speed_buff_slope = 0.5;

        private const double jack_convex = 1.29407;

        private const double jack_scale = 0.62159;

        private const double held_ln_jack_buff = 0.6;

        private const double chord_speed_fast_ms = 100.0;
        private const double chord_speed_slow_ms = 140.0;
        private const double chord_speed_veryfast_ms = 84.0;
        private const double chord_speed_slow_mult = 0.6;
        private const double chord_speed_fast_mult = 1.2;
        private const double chord_speed_veryfast_mult = 0.75;

        private const double quad_minijack_buff = 2.5;
        private const int quad_minijack_min_chord = 4;
        private const double quad_minijack_fast_ms = 85.0;
        private const double quad_minijack_slow_ms = 110.0;
        private const double quad_minijack_manip_lo = 0.95;
        private const double quad_minijack_manip_hi = 0.99;

        private const double quad_minijack_run_ms = 110.0;
        private const int quad_minijack_run_cap = 32;
        private const double quad_minijack_run_start = 3.0;
        private const double quad_minijack_run_end = 4.0;

        private const double quad_minijack_vfast_hi_ms = 74.0;
        private const double quad_minijack_vfast_lo_ms = 66.0;

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject hitObject)
        {
            double lastStartTime = hitObject.LastStartTimeInColumn(hitObject.Column);
            double columnDelta = double.IsNegativeInfinity(lastStartTime) ? double.PositiveInfinity : hitObject.StartTime - lastStartTime;
            int chordSize = ChordEvaluator.Size(hitObject);

            if (columnDelta > JACK_WINDOW_MS)
                return 0.0;

            double tapRate = 1.0 / (Math.Max(columnDelta, 1.0) / 1000.0 + jack_rate_offset);

            double chordSpeedFactor = Math.Clamp(chord_speed_threshold_ms / columnDelta, 0.1, 2.0);

            double chordjackMultiplier = Math.Max(chordjack_multiplier_minimum,
                (1.0 + chordjack_buff * chordSpeedFactor * (chordSize - 1))
                * ChordEvaluator.FullChordDampen(hitObject, hitObject.PreviousHitObjects.Length, columnDelta)
                * ChordEvaluator.NearFullChordDampen(hitObject, hitObject.PreviousHitObjects.Length, columnDelta));
            double speedBuff = 1.0 + jack_speed_buff * DifficultyCalculationUtils.Logistic(tapRate, jack_speed_buff_midpoint, jack_speed_buff_slope);

            double rawStrain = tapRate * chordjackMultiplier * speedBuff;
            double strain = jack_scale * Math.Pow(rawStrain, jack_convex);

            if (chordSize >= 2)
            {
                strain *= chordjack_nerf;

                double bpmScale = DifficultyCalculationUtils.Smoothstep(chord_speed_slow_ms - columnDelta, 0.0, chord_speed_slow_ms - chord_speed_fast_ms);
                double chordSpeedMult = chord_speed_slow_mult + (chord_speed_fast_mult - chord_speed_slow_mult) * bpmScale;

                // Roll the buff back down for very fast chord jacks so the scaling slows past ~160bpm.
                double fastRolloff = DifficultyCalculationUtils.Smoothstep(chord_speed_fast_ms - columnDelta, 0.0, chord_speed_fast_ms - chord_speed_veryfast_ms);
                chordSpeedMult += (chord_speed_veryfast_mult - chord_speed_fast_mult) * fastRolloff;

                strain *= chordSpeedMult;
            }
            else
                strain *= TrillEvaluator.TrillFactor(hitObject);

            int totalColumns = hitObject.PreviousHitObjects.Length;

            if (totalColumns > 1)
            {
                double heldFraction = hitObject.ConcurrentlyHeldColumns(ChordEvaluator.CHORD_TOLERANCE_MS) / (double)(totalColumns - 1);
                strain *= 1.0 + held_ln_jack_buff * heldFraction;
            }

            var previous = hitObject.Previous(0);
            int fullChord = Math.Max(quad_minijack_min_chord, totalColumns);

            if (previous != null && ChordEvaluator.Size(previous) >= fullChord)
            {
                double speedGate = DifficultyCalculationUtils.Smoothstep(quad_minijack_slow_ms - columnDelta, 0.0, quad_minijack_slow_ms - quad_minijack_fast_ms);
                double manipGate = DifficultyCalculationUtils.ReverseLerp(hitObject.ManipulationFactor, quad_minijack_manip_lo, quad_minijack_manip_hi);

                int runLength = 1;
                ManiaDifficultyHitObject cur = hitObject;

                for (int back = 0; back < quad_minijack_run_cap; back++)
                {
                    var prevInColumn = hitObject.PrevInColumn(back);

                    if (prevInColumn == null || cur.StartTime - prevInColumn.StartTime > quad_minijack_run_ms)
                        break;

                    runLength++;
                    cur = prevInColumn;
                }

                cur = hitObject;

                for (int forward = 0; forward < quad_minijack_run_cap; forward++)
                {
                    var nextInColumn = hitObject.NextInColumn(forward);

                    if (nextInColumn == null || nextInColumn.StartTime - cur.StartTime > quad_minijack_run_ms)
                        break;

                    runLength++;
                    cur = nextInColumn;
                }

                double runGate = 1.0 - DifficultyCalculationUtils.Smoothstep(runLength - quad_minijack_run_start, 0.0, quad_minijack_run_end - quad_minijack_run_start);

                double vFastGate = 1.0 - DifficultyCalculationUtils.Smoothstep(quad_minijack_vfast_hi_ms - columnDelta, 0.0, quad_minijack_vfast_hi_ms - quad_minijack_vfast_lo_ms);

                strain *= 1.0 + quad_minijack_buff * speedGate * manipGate * runGate * vFastGate;
            }

            return strain * hitObject.ManipulationFactor;
        }
    }
}
