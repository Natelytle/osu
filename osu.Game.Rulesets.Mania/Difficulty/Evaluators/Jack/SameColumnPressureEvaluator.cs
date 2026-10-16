// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators.Jack
{
    /// <summary>
    /// Measures how much sustained same-column pressure is running at the moment a note is hit, instead of
    /// scoring one repeat at a time. Three things come out of it:
    /// <list type="bullet">
    /// <item><description>an integral of repeat pressure over the last second, so a jack run reads differently to a stray repeat,</description></item>
    /// <item><description>a power mean across columns, so one heavily jacked key dominates a hand that is otherwise calm,</description></item>
    /// <item><description>a gate that discounts jack difficulty landing inside a wide chord, because the columns either side are being hit at the same time.</description></item>
    /// </list>
    /// </summary>
    public static class SameColumnPressureEvaluator
    {
        /// <summary>
        /// How far back the pressure integral reaches. Matches a centred half-window of the same length, which
        /// is what this used to be precomputed over; being per-note it can only look backwards, so the whole
        /// length is taken as trailing.
        /// </summary>
        public const double PRESSURE_WINDOW_MS = 1000.0;

        /// <summary>
        /// What a fully jack-filled window is worth on top of the repeat the note is already making.
        /// A column that comes back to itself once in the window scores about <see cref="PRESSURE_WINDOW_MS"/>
        /// of this at most, and a column that never repeats scores none.
        ///
        /// Measured at zero. Jack is the one skill whose value correlates with how far a map's star rating
        /// misses its filename (r = +0.38, against |r| &lt; 0.11 for every other skill), and this is the only
        /// term that pushed jack up. The chord width gate below carried the bias fix on its own, so this
        /// stays wired up and ready to turn back up if the jack weighting gets corrected from the other side.
        /// </summary>
        private const double sustain_weight = 0.0;

        /// <summary>
        /// Keeps the sustain term from running away on maps that are nothing but jacks.
        /// </summary>
        private const double sustain_ceiling = 1.0;

        /// <summary>
        /// How sharply the cross-column mean favours the hardest column. Low values average the hand together,
        /// high values let one jacked key speak for all of them.
        /// </summary>
        private const double column_mean_power = 5.0;

        /// <summary>
        /// The repeat that the pressure integral is measured against. Repeats away from this are discounted,
        /// the same way they are on the curves the other evaluators use.
        /// </summary>
        private const double neutral_repeat_delta = 0.08;

        private const double jack_nerf_coefficient = 7e-5;
        private const double jack_nerf_base = 0.15;
        private const double jack_nerf_power = -4;

        /// <summary>
        /// How fast a column has to come back to itself before the repeat counts as pressure at all.
        /// </summary>
        private const double repeat_leniency = 0.11;

        /// <summary>
        /// Two adjacent columns repeating within this of each other are one even motion and cost the hand nothing.
        /// </summary>
        private const double even_difference_tight = 0.02;
        private const double even_difference_moderate = 0.07;

        private const double even_penalty_base = 0.75;
        private const double even_penalty_moderate = 0.65;
        private const double even_penalty_slope = 5.0;
        private const double even_penalty_delta = 0.5;

        /// <summary>
        /// Repeats slower than this leave the hand room to reset, so disagreeing with a neighbour costs nothing.
        /// </summary>
        private const double slow_repeat_cutoff = 0.11;
        private const double slow_repeat_penalty = 0.4;

        private const double chord_gate_weight = 1.0;
        private const double chord_gate_exponent = 3.0;

        private const double MS_TO_SECONDS = 0.001;

        /// <summary>
        /// The shortest gap two notes in one column can leave. Keeps the reciprocal below from running away on a
        /// degenerate map, and keeps the fifth power in the column mean from overflowing.
        /// </summary>
        private const double MIN_REPEAT_DELTA = 0.001;

        /// <summary>
        /// Stands in for a column with nothing to measure against.
        /// </summary>
        private const double NO_PATTERN = 1e9;

        /// <summary>
        /// How much of the trailing window is spent coming back to the same column, and how lopsided that is
        /// across the hand. Returns at least 1.0, so a map with no jack pressure in it is left alone.
        /// </summary>
        public static double EvaluateSustainMultiplier(ManiaDifficultyHitObject current)
        {
            double pressure = evaluateCombinedPressure(current);

            if (pressure <= 0.0)
                return 1.0;

            // A whole window of the repeat this note is making, which is the most the window can be worth.
            double reference = evaluateRepeatPressure(MS_TO_SECONDS * Math.Max(current.ColumnDelta, 1.0))
                               * MS_TO_SECONDS * PRESSURE_WINDOW_MS;

            if (reference <= 0.0)
                return 1.0;

            return 1.0 + sustain_weight * Math.Min(pressure / reference, sustain_ceiling);
        }

        /// <summary>
        /// Discounts a repeat that lands inside a wide chord. Every adjacent pair of the chord multiplies down,
        /// so the wider the chord the less the repeats inside it are worth.
        /// </summary>
        public static double EvaluateChordWidthGate(ManiaDifficultyHitObject current)
        {
            int[] columns = current.Row.Columns;

            if (columns.Length < 2)
                return 1.0;

            double evenness = 1.0;

            for (int i = 1; i < columns.Length; i++)
                evenness *= evaluateEvennessPenalty(columnDeltaAt(current, columns[i - 1]), columnDeltaAt(current, columns[i]));

            return Math.Pow(evenness, chord_gate_weight * chord_gate_exponent / current.Row.TotalColumns);
        }

        /// <summary>
        /// The weighted mean of every column's pressure, weighted towards the faster columns and taken to a
        /// high power so the hardest one carries the hand.
        /// </summary>
        private static double evaluateCombinedPressure(ManiaDifficultyHitObject current)
        {
            int columnCount = current.PreviousHitObjects.Length;

            double numerator = 0.0;
            double denominator = 0.0;

            for (int column = 0; column < columnCount; column++)
            {
                double pressure = evaluateColumnPressure(current, column, out double delta);

                if (pressure <= 0.0 || delta >= NO_PATTERN)
                    continue;

                double weight = 1.0 / Math.Max(delta, MIN_REPEAT_DELTA);

                numerator += Math.Pow(pressure, column_mean_power) * weight;
                denominator += weight;
            }

            return denominator > 1e-9 ? Math.Pow(numerator / denominator, 1.0 / column_mean_power) : 0.0;
        }

        /// <summary>
        /// Integrates a column's repeat pressure over the trailing window. Each repeat holds its pressure across
        /// the gap up to the next note in that column, and only the part of that gap inside the window counts.
        /// </summary>
        private static double evaluateColumnPressure(ManiaDifficultyHitObject current, int column, out double nearestDelta)
        {
            nearestDelta = NO_PATTERN;

            double windowStart = current.StartTime - PRESSURE_WINDOW_MS;
            double integral = 0.0;

            // The previous row's note in this column. Seeking a column by index would rescan it from the end
            // every step; this array already holds where that search would have landed.
            var cursor = current.PreviousHitObjects[column];

            while (cursor != null)
            {
                // A repeat needs a following note to measure against. The last note in a column has none.
                if (cursor.NextInColumn(0) is not { } successor)
                    break;

                double repeatStart = cursor.StartTime;
                double repeatEnd = successor.StartTime;

                if (repeatStart >= current.StartTime)
                    break;

                double overlap = Math.Min(repeatEnd, current.StartTime) - Math.Max(repeatStart, windowStart);

                if (overlap <= 0.0)
                    break;

                double delta = MS_TO_SECONDS * (repeatEnd - repeatStart);

                integral += evaluateRepeatPressure(delta) * overlap;

                if (nearestDelta >= NO_PATTERN)
                    nearestDelta = delta;

                if (repeatStart <= windowStart)
                    break;

                // Past the first step this stays inside the one column, which does not have to search for it.
                cursor = cursor.PrevInColumn(0);
            }

            return MS_TO_SECONDS * integral;
        }

        /// <summary>
        /// How hard one repeat of a column on itself is, before it is integrated over time. Faster is harder
        /// throughout; there is no repeat rate that is easier than its neighbours.
        /// </summary>
        private static double evaluateRepeatPressure(double delta)
        {
            delta = Math.Max(delta, MIN_REPEAT_DELTA);
            return (1.0 / delta) * (1.0 / (delta + repeat_leniency)) * evaluateRepeatNerf(delta);
        }
        /// <summary>
        /// A flat discount on repeats that sit near <see cref="neutral_repeat_delta"/>. The falloff is steep
        /// enough to matter over a narrow band, but never enough to make a slow repeat outrank a fast one.
        /// </summary>
        private static double evaluateRepeatNerf(double delta)
        {
            double deviation = Math.Abs(delta - neutral_repeat_delta);
            return 1.0 - jack_nerf_coefficient * Math.Pow(jack_nerf_base + deviation, jack_nerf_power);
        }

        /// <summary>
        /// What one adjacent pair of columns disagreeing is worth. The closest they can agree and still be
        /// scored as two hands is <see cref="even_difference_tight"/>; past
        /// <see cref="even_difference_moderate"/> they are too far apart to read as one motion at all.
        /// </summary>
        private static double evaluateEvennessPenalty(double leftDelta, double rightDelta)
        {
            double slowest = Math.Max(leftDelta, rightDelta);
            double difference = Math.Abs(leftDelta - rightDelta) + slow_repeat_penalty * Math.Max(0.0, slowest - slow_repeat_cutoff);

            if (difference < even_difference_tight)
                return Math.Min(even_penalty_base + even_penalty_delta * slowest, 1.0);

            if (difference < even_difference_moderate)
                return Math.Min(even_penalty_moderate + even_penalty_slope * difference + even_penalty_delta * slowest, 1.0);

            return 1.0;
        }

        /// <summary>
        /// The gap to the next note in <paramref name="column"/> as of this note, or <see cref="NO_PATTERN"/>
        /// when the column has nothing to measure.
        /// </summary>
        private static double columnDeltaAt(ManiaDifficultyHitObject current, int column)
        {
            if (column == current.Column)
                return current.ColumnDelta;

            if (current.PreviousHitObjects[column] is not { } before
                || before.NextInColumn(0) is not { } after)
                return NO_PATTERN;

            return MS_TO_SECONDS * (after.StartTime - before.StartTime);
        }
    }
}
