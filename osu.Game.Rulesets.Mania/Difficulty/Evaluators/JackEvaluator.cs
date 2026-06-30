// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class JackEvaluator
    {
        private const double jack_multiplier = 0.62159;

        public const double JACK_WINDOW_MS = 350.0;

        private const double jack_rate_offset_ms = 60;

        private const double chordjack_buff = 0.17460;
        private const double chordjack_multiplier_minimum = 0.1;
        private const double chordjack_nerf = 0.45397;
        private const double chord_speed_threshold_ms = 140.625;

        private const double jack_speed_bonus_multiplier = 0.70000;
        private const double jack_speed_bonus_midpoint = 5.0;
        private const double jack_speed_bonus_slope = 0.5;

        private const double jack_convex = 1.29407;

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

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            var previous = (ManiaDifficultyHitObject?)current.Previous();

            double columnDelta = current.ColumnDelta;

            if (columnDelta > JACK_WINDOW_MS)
                return 0.0;

            double tapRate = 1000.0 / (Math.Max(columnDelta, 1.0) + jack_rate_offset_ms);

            double jackDifficulty = tapRate; // Start difficulty with the tap rate.

            int rowSize = ChordUtils.Size(current);
            int totalColumns = current.PreviousHitObjects.Length;

            jackDifficulty *= calculateChordJackBonus(current, rowSize, columnDelta);
            jackDifficulty *= calculateSpeedBonus(tapRate);

            // Rescale difficulty
            jackDifficulty = DiffUtils.Pow(jackDifficulty, jack_convex);

            jackDifficulty *= calculateRowSizeMultiplier(current, rowSize, columnDelta);

            jackDifficulty *= calculateConcurrentHoldBonus(current, totalColumns);
            jackDifficulty *= calculateFullRowBonus(current, previous, totalColumns, columnDelta);

            jackDifficulty *= current.ManipulationFactor * current.StaminaFactor;

            return jackDifficulty * jack_multiplier;
        }

        private static double calculateChordJackBonus(ManiaDifficultyHitObject current, int rowSize, double columnDelta)
        {
            double chordSpeedFactor = Math.Clamp(chord_speed_threshold_ms / columnDelta, 0.1, 2.0);

            double chordJackBonus = Math.Max(chordjack_multiplier_minimum,
                (1.0 + chordjack_buff * chordSpeedFactor * (rowSize - 1))
                * ChordUtils.FullChordDampen(current, current.PreviousHitObjects.Length, columnDelta)
                * ChordUtils.NearFullChordDampen(current, current.PreviousHitObjects.Length, columnDelta));

            return chordJackBonus;
        }

        private static double calculateSpeedBonus(double tapRate) => 1.0 + jack_speed_bonus_multiplier * DiffUtils.Logistic(tapRate, jack_speed_bonus_midpoint, jack_speed_bonus_slope);

        private static double calculateRowSizeMultiplier(ManiaDifficultyHitObject current, int rowSize, double columnDelta)
        {
            double rowSizeMultiplier = 1.0;

            if (rowSize >= 2)
            {
                rowSizeMultiplier *= chordjack_nerf;

                double bpmScale = DiffUtils.Smoothstep(chord_speed_slow_ms - columnDelta, 0.0, chord_speed_slow_ms - chord_speed_fast_ms);
                double chordSpeedMult = chord_speed_slow_mult + (chord_speed_fast_mult - chord_speed_slow_mult) * bpmScale;

                // Roll the buff back down for very fast chord jacks so the scaling slows past ~160bpm.
                double fastRolloff = DiffUtils.Smoothstep(chord_speed_fast_ms - columnDelta, 0.0, chord_speed_fast_ms - chord_speed_veryfast_ms);
                chordSpeedMult += (chord_speed_veryfast_mult - chord_speed_fast_mult) * fastRolloff;

                rowSizeMultiplier *= chordSpeedMult;
            }
            else
                rowSizeMultiplier *= TrillUtils.TrillFactor(current);

            return rowSizeMultiplier;
        }

        private static double calculateConcurrentHoldBonus(ManiaDifficultyHitObject current, int totalColumns)
        {
            if (totalColumns == 1) return 1.0;

            double heldFraction = current.ConcurrentlyHeldColumns(ChordUtils.CHORD_TOLERANCE_MS) / (double)(totalColumns - 1);
            double concurrentHoldBonus = 1.0 + held_ln_jack_buff * heldFraction;

            return concurrentHoldBonus;
        }

        private static double calculateFullRowBonus(ManiaDifficultyHitObject current, ManiaDifficultyHitObject? previous, int totalColumns, double columnDelta)
        {
            int fullChord = Math.Max(quad_minijack_min_chord, totalColumns);

            if (previous == null || ChordUtils.Size(previous) < fullChord)
                return 1.0;

            double speedGate = DiffUtils.Smoothstep(quad_minijack_slow_ms - columnDelta, 0.0, quad_minijack_slow_ms - quad_minijack_fast_ms);
            double manipGate = DiffUtils.ReverseLerp(current.ManipulationFactor, quad_minijack_manip_lo, quad_minijack_manip_hi);

            int runLength = 1;
            ManiaDifficultyHitObject note = current;

            for (int back = 0; back < quad_minijack_run_cap; back++)
            {
                var prevInColumn = current.PrevInColumn(back);

                if (prevInColumn == null || note.StartTime - prevInColumn.StartTime > quad_minijack_run_ms)
                    break;

                runLength++;
                note = prevInColumn;
            }

            note = current;

            for (int forward = 0; forward < quad_minijack_run_cap; forward++)
            {
                var nextInColumn = current.NextInColumn(forward);

                if (nextInColumn == null || nextInColumn.StartTime - note.StartTime > quad_minijack_run_ms)
                    break;

                runLength++;
                note = nextInColumn;
            }

            double runGate = 1.0 - DiffUtils.Smoothstep(runLength, quad_minijack_run_start, quad_minijack_run_end);

            double vFastGate = 1.0 - DiffUtils.Smoothstep(columnDelta, quad_minijack_vfast_lo_ms, quad_minijack_vfast_hi_ms);

            double fullRowBonus = 1.0 + quad_minijack_buff * speedGate * manipGate * runGate * vFastGate;

            return fullRowBonus;
        }
    }
}
