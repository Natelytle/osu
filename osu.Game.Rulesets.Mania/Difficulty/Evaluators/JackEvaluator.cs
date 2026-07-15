// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators.Jack;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class JackEvaluator
    {
        private const double jack_multiplier = 0.62159;

        public const double JACK_WINDOW_MS = 350.0;

        // Added to the column gap before inverting it into a tap rate, softening very fast jacks.
        private const double tap_rate_offset_ms = 60;

        private const double strain_exponent = 1.29407;

        // Logistic speed bonus on the tap rate.
        private const double speed_bonus_strength = 0.70000;
        private const double speed_bonus_midpoint = 5.0;
        private const double speed_bonus_slope = 0.5;

        // Chord-jack bonus (grows the strain with chord size, faster chords count more).
        private const double chordjack_buff = 0.17460;
        private const double chordjack_bonus_min = 0.1;
        private const double chordjack_nerf = 0.45397;
        private const double chordjack_speed_pivot_ms = 140.625;

        // Speed multiplier applied to chords (rowSize >= 2), interpolated across three speed tiers.
        private const double chordjack_slow_ms = 140.0;
        private const double chordjack_fast_ms = 100.0;
        private const double chordjack_veryfast_ms = 84.0;
        private const double chordjack_slow_mult = 0.6;
        private const double chordjack_fast_mult = 1.2;
        private const double chordjack_veryfast_mult = 0.75;

        private const double held_ln_buff = 0.6;

        private const double single_jack_nerf_strength = 0.90;
        private const double single_jack_nerf_center = 5.5;
        private const double single_jack_nerf_width = 0.7;

        private const double incidental_jack_nerf_strength = 0.9;
        private const double incidental_jack_ratio_lo = 2.2;
        private const double incidental_jack_ratio_hi = 3.2;
        private const double incidental_jack_cd_lo = 122.0;
        private const double incidental_jack_cd_hi = 150.0;
        private const int incidental_jack_context_radius = 6;
        private const double incidental_jack_context_lo = 0.72;
        private const double incidental_jack_context_hi = 0.90;

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            var previous = (ManiaDifficultyHitObject?)current.Previous();

            double columnDelta = current.ColumnDelta;

            if (columnDelta > JACK_WINDOW_MS)
                return 0.0;

            double tapRate = 1000.0 / (Math.Max(columnDelta, 1.0) + tap_rate_offset_ms);

            double jackDifficulty = tapRate; // Start difficulty with the tap rate.

            int rowSize = ChordUtils.DepthInChord(current);
            int totalColumns = current.PreviousHitObjects.Length;

            jackDifficulty *= calculateChordJackBonus(current, rowSize, columnDelta);
            jackDifficulty *= calculateSpeedBonus(tapRate);

            // Rescale difficulty
            jackDifficulty = DiffUtils.Pow(jackDifficulty, strain_exponent);

            jackDifficulty *= calculateRowSizeMultiplier(current, rowSize, columnDelta);
            jackDifficulty *= calculateConcurrentHoldBonus(current, totalColumns);

            double baseBeforeFullRow = jackDifficulty * jack_multiplier;
            jackDifficulty *= MinijackEvaluator.Evaluate(current, previous, totalColumns, columnDelta, baseBeforeFullRow);

            jackDifficulty *= current.ManipulationFactor * current.StaminaFactor * SpeedjackEvaluator.Evaluate(current) * AnchorEvaluator.Evaluate(current);

            jackDifficulty *= calculateSingleJackNerf(rowSize, tapRate);
            jackDifficulty *= calculateIncidentalJackNerf(current, rowSize);

            return jackDifficulty * jack_multiplier;
        }

        private static double calculateChordJackBonus(ManiaDifficultyHitObject current, int rowSize, double columnDelta)
        {
            double chordSpeedFactor = Math.Clamp(chordjack_speed_pivot_ms / columnDelta, 0.1, 2.0);

            double chordJackBonus = Math.Max(chordjack_bonus_min,
                (1.0 + chordjack_buff * chordSpeedFactor * (rowSize - 1))
                * ChordUtils.FullChordDampen(current, current.PreviousHitObjects.Length, columnDelta)
                * ChordUtils.NearFullChordDampen(current, current.PreviousHitObjects.Length, columnDelta));

            return chordJackBonus;
        }

        private static double calculateSpeedBonus(double tapRate) => 1.0 + speed_bonus_strength * DiffUtils.Logistic(tapRate, speed_bonus_midpoint, speed_bonus_slope);

        private static double calculateRowSizeMultiplier(ManiaDifficultyHitObject current, int rowSize, double columnDelta)
        {
            double rowSizeMultiplier = 1.0;

            if (rowSize >= 2)
            {
                rowSizeMultiplier *= chordjack_nerf;

                double bpmScale = DiffUtils.Smoothstep(chordjack_slow_ms - columnDelta, 0.0, chordjack_slow_ms - chordjack_fast_ms);
                double chordSpeedMult = chordjack_slow_mult + (chordjack_fast_mult - chordjack_slow_mult) * bpmScale;

                // Roll the buff back down for very fast chord jacks so the scaling slows past ~160bpm.
                double fastRolloff = DiffUtils.Smoothstep(chordjack_fast_ms - columnDelta, 0.0, chordjack_fast_ms - chordjack_veryfast_ms);
                chordSpeedMult += (chordjack_veryfast_mult - chordjack_fast_mult) * fastRolloff;

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
            double concurrentHoldBonus = 1.0 + held_ln_buff * heldFraction;

            return concurrentHoldBonus;
        }

        private static double calculateSingleJackNerf(int rowSize, double tapRate)
        {
            if (rowSize >= 2)
                return 1.0;

            double bell = DiffUtils.SmoothstepBellCurve(tapRate, single_jack_nerf_center, single_jack_nerf_width);

            return 1.0 - (1.0 - single_jack_nerf_strength) * bell;
        }

        private static double calculateIncidentalJackNerf(ManiaDifficultyHitObject current, int rowSize)
        {
            if (rowSize >= 2)
                return 1.0;

            double rowGap = current.DeltaTime;

            if (rowGap <= 1.0)
                return 1.0;

            double ratio = current.ColumnDelta / rowGap;
            double ratioGate = DiffUtils.Smoothstep(ratio, incidental_jack_ratio_lo, incidental_jack_ratio_hi);
            double slowGate = DiffUtils.Smoothstep(current.ColumnDelta, incidental_jack_cd_lo, incidental_jack_cd_hi);
            double purityGate = DiffUtils.Smoothstep(singleNoteContextFraction(current), incidental_jack_context_lo, incidental_jack_context_hi);

            return 1.0 - incidental_jack_nerf_strength * ratioGate * slowGate * purityGate;
        }

        private static double singleNoteContextFraction(ManiaDifficultyHitObject current)
        {
            int single = 0;
            int total = 0;

            ManiaRow? row = current.Row;

            for (int i = 0; i <= incidental_jack_context_radius && row != null; i++, row = row.Previous())
            {
                total++;
                if (row.Size == 1) single++;
            }

            row = current.Row.Next();

            for (int i = 0; i < incidental_jack_context_radius && row != null; i++, row = row.Next())
            {
                total++;
                if (row.Size == 1) single++;
            }

            return total > 0 ? (double)single / total : 0.0;
        }
    }
}
