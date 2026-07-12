// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;
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

        private const double quad_minijack_run_rel = 1.5;
        private const int quad_minijack_run_cap = 32;
        private const double quad_minijack_run_start = 3.0;
        private const double quad_minijack_run_end = 4.0;

        private const double quad_minijack_recur_window_rel = 4.0;
        private const double quad_minijack_recur_lo = 1.0;
        private const double quad_minijack_recur_hi = 2.0;

        private const double speedjack_buff = 0.35;
        private const double speedjack_speed_hi_ms = 110.0;
        private const double speedjack_speed_lo_ms = 70.0;
        private const double speedjack_single_gate = 0.5;
        private const double speedjack_chord_taper = 0.8;
        private const int speedjack_clean_window = 6;
        private const int speedjack_max_shift = 1;

        private const double anchor_buff = 1.0;
        private const double anchor_window_ms = 400.0;
        private const double anchor_gate_lo = 0.40;
        private const double anchor_gate_hi = 0.85;

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            var previous = (ManiaDifficultyHitObject?)current.Previous();

            double columnDelta = current.ColumnDelta;

            if (columnDelta > JACK_WINDOW_MS)
                return 0.0;

            double tapRate = 1000.0 / (Math.Max(columnDelta, 1.0) + jack_rate_offset_ms);

            double jackDifficulty = tapRate; // Start difficulty with the tap rate.

            int rowSize = ChordUtils.DepthInChord(current);
            int totalColumns = current.PreviousHitObjects.Length;

            jackDifficulty *= calculateChordJackBonus(current, rowSize, columnDelta);
            jackDifficulty *= calculateSpeedBonus(tapRate);

            // Rescale difficulty
            jackDifficulty = DiffUtils.Pow(jackDifficulty, jack_convex);

            jackDifficulty *= calculateRowSizeMultiplier(current, rowSize, columnDelta);

            jackDifficulty *= calculateConcurrentHoldBonus(current, totalColumns);
            jackDifficulty *= calculateFullRowBonus(current, previous, totalColumns, columnDelta);

            jackDifficulty *= current.ManipulationFactor * current.StaminaFactor * calculateSpeedjackBonus(current) * calculateAnchorBonus(current);

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

            if (previous == null)
                return 1.0;

            bool sharesChordWithPrevious = current.Row.IsSameRow(previous.Row);

            if (sharesChordWithPrevious || previous.Row.Size < fullChord)
                return 1.0;

            double speedGate = DiffUtils.Smoothstep(quad_minijack_slow_ms - columnDelta, 0.0, quad_minijack_slow_ms - quad_minijack_fast_ms);
            double manipGate = DiffUtils.ReverseLerp(current.ManipulationFactor, quad_minijack_manip_lo, quad_minijack_manip_hi);

            double runWindow = quad_minijack_run_rel * columnDelta;

            int runLength = 1;
            ManiaDifficultyHitObject note = current;

            for (int back = 0; back < quad_minijack_run_cap; back++)
            {
                var prevInColumn = current.PrevInColumn(back);

                if (prevInColumn == null || note.StartTime - prevInColumn.StartTime > runWindow)
                    break;

                runLength++;
                note = prevInColumn;
            }

            note = current;

            for (int forward = 0; forward < quad_minijack_run_cap; forward++)
            {
                var nextInColumn = current.NextInColumn(forward);

                if (nextInColumn == null || nextInColumn.StartTime - note.StartTime > runWindow)
                    break;

                runLength++;
                note = nextInColumn;
            }

            double runGate = 1.0 - DiffUtils.Smoothstep(runLength, quad_minijack_run_start, quad_minijack_run_end);
            double recurGate = calculateFullChordRecurGate(current, fullChord, columnDelta);
            double fullRowBonus = 1.0 + quad_minijack_buff * speedGate * manipGate * runGate * recurGate;

            return fullRowBonus;
        }

        private static double calculateFullChordRecurGate(ManiaDifficultyHitObject current, int fullChord, double columnDelta)
        {
            double window = quad_minijack_recur_window_rel * columnDelta;
            int fullChords = 0;

            for (int i = 0; i < quad_minijack_run_cap; i++)
            {
                var previous = (ManiaDifficultyHitObject?)current.Previous(i);

                if (previous == null || current.StartTime - previous.StartTime > window)
                    break;

                if (ChordUtils.DepthInChord(previous) == 1 && previous.Row.Size >= fullChord)
                    fullChords++;
            }

            return 1.0 - DiffUtils.Smoothstep(fullChords, quad_minijack_recur_lo, quad_minijack_recur_hi);
        }

        private static double calculateSpeedjackBonus(ManiaDifficultyHitObject current)
        {
            ManiaRow row = current.Row;
            ManiaRow? previous = row.Previous(0);
            ManiaRow? previous2 = row.Previous(1);

            if (previous == null || previous2 == null)
                return 1.0;

            double timeSincePreviousRow = row.StartTime - previous.StartTime;
            double speedScale = DiffUtils.Smoothstep(speedjack_speed_hi_ms - timeSincePreviousRow, 0.0, speedjack_speed_hi_ms - speedjack_speed_lo_ms);

            if (speedScale <= 0.0)
                return 1.0;

            bool isFullRepeat = sameColumns(row.Columns, previous.Columns) || sameColumns(row.Columns, previous2.Columns);
            bool isRoll = columnShift(previous.Columns, row.Columns) != 0;
            bool sharesJack = sharesColumn(row.Columns, previous.Columns) || sharesColumn(row.Columns, previous2.Columns);

            if (isFullRepeat || isRoll || !sharesJack)
                return 1.0;

            double clean = 1.0 - localJumptrillRollDensity(row);

            if (clean <= 0.0)
                return 1.0;

            double sizeGate = row.Size <= 1
                ? speedjack_single_gate
                : 1.0 - speedjack_chord_taper * DiffUtils.Smoothstep(row.Size, 2.0, 4.0);

            return 1.0 + speedjack_buff * speedScale * sizeGate * clean;
        }

        private static double calculateAnchorBonus(ManiaDifficultyHitObject current)
        {
            int totalColumns = current.PreviousHitObjects.Length;

            if (totalColumns < 2)
                return 1.0;

            double[] usage = new double[totalColumns];
            double center = current.StartTime;

            addRowUsage(current.Row, usage, center);

            for (ManiaRow? row = current.Row.Previous(); row != null && center - row.StartTime <= anchor_window_ms; row = row.Previous(0))
                addRowUsage(row, usage, center);

            for (ManiaRow? row = current.Row.Next(); row != null && row.StartTime - center <= anchor_window_ms; row = row.Next(0))
                addRowUsage(row, usage, center);

            // Sort the per-column usages from busiest to least-used.
            Array.Sort(usage);
            Array.Reverse(usage);

            double walkSum = 0.0;
            double maxWalkSum = 0.0;

            for (int i = 0; i + 1 < totalColumns; i++)
            {
                double currentUsage = usage[i];
                double nextUsage = usage[i + 1];

                // Only step between two active columns and once the next column is unused then that means that we've left the anchor.
                if (nextUsage == 0.0)
                    break;

                double ratio = nextUsage / currentUsage;
                double difference = 0.5 - ratio;
                double balanceFactor = 1.0 - 4.0 * difference * difference;

                walkSum += currentUsage * balanceFactor;
                maxWalkSum += currentUsage;
            }

            double anchorValue = maxWalkSum != 0.0 ? walkSum / maxWalkSum : 0.0;

            return 1.0 + anchor_buff * DiffUtils.Smoothstep(anchorValue, anchor_gate_lo, anchor_gate_hi);
        }

        /// <summary>
        /// Adds each of <paramref name="row"/>'s columns to the per-column <paramref name="usage"/> tally,
        /// weighted by a quadratic falloff with the row's time distance from <paramref name="center"/>.
        /// </summary>
        private static void addRowUsage(ManiaRow row, double[] usage, double center)
        {
            double distance = Math.Abs(row.StartTime - center) / anchor_window_ms;
            double weight = 1.0 - distance * distance;

            if (weight <= 0.0)
                return;

            foreach (int column in row.Columns)
            {
                if (column >= 0 && column < usage.Length)
                    usage[column] += weight;
            }
        }

        private static double localJumptrillRollDensity(ManiaRow row)
        {
            int window = 0;
            int manipulable = 0;

            for (ManiaRow? current = row; current != null && window < speedjack_clean_window; current = current.Previous(0))
            {
                window++;

                ManiaRow? previous = current.Previous(0);
                ManiaRow? previous2 = current.Previous(1);

                if (previous == null || previous2 == null)
                    continue;

                if (current.StartTime - previous.StartTime > speedjack_speed_hi_ms)
                    continue;

                bool isJumptrill = sameColumns(current.Columns, previous2.Columns) && !sameColumns(current.Columns, previous.Columns);
                bool isRoll = columnShift(previous.Columns, current.Columns) != 0;

                if (isJumptrill || isRoll)
                    manipulable++;
            }

            return window > 0 ? (double)manipulable / window : 0.0;
        }

        private static bool sameColumns(int[] a, int[] b)
        {
            if (a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        private static bool sharesColumn(int[] a, int[] b)
        {
            foreach (int columnA in a)
            {
                foreach (int columnB in b)
                {
                    if (columnA == columnB)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// If <paramref name="b"/> is <paramref name="a"/> with every column shifted by the same constant
        /// k (with 0 &lt; |k| &lt;= <see cref="speedjack_max_shift"/>), returns k; otherwise returns 0.
        /// </summary>
        private static int columnShift(int[] a, int[] b)
        {
            if (a.Length != b.Length || a.Length == 0)
                return 0;

            int k = b[0] - a[0];

            if (k == 0 || Math.Abs(k) > speedjack_max_shift)
                return 0;

            for (int i = 1; i < a.Length; i++)
            {
                if (b[i] - a[i] != k)
                    return 0;
            }

            return k;
        }
    }
}
