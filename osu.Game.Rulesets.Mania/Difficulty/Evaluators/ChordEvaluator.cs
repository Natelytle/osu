// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class ChordEvaluator
    {
        public const double CHORD_TOLERANCE_MS = 8.0;

        private const double full_chord_nerf = 0.50;

        private const double full_chord_run_ramp = 2.0;

        private const double near_full_chord_nerf = 0.0;

        private const double near_full_chord_run_ramp = 55.0;

        /// <summary>
        /// 16th note at 160 BPM - the crossover where chord presses earn full credit. Faster repeats get a
        /// higher dampening ceiling; slower repeats get a lower one.
        /// </summary>
        private const double chord_speed_threshold_ms = 140.625;

        public static int Size(DifficultyHitObject current)
        {
            int chordSize = 1;

            for (int i = 0; current.Previous(i) is { } previous && Math.Abs(previous.StartTime - current.StartTime) <= CHORD_TOLERANCE_MS; i++)
                chordSize++;

            return chordSize;
        }

        public static double FullChordDampen(DifficultyHitObject current, int totalColumns, double columnDelta)
        {
            double speedScale = Math.Clamp(columnDelta / chord_speed_threshold_ms, 0.0, 1.0);
            double ceiling = full_chord_nerf * speedScale;

            if (ceiling <= 0)
                return 1.0;

            double ramp = Math.Max(1.0, full_chord_run_ramp);
            int cap = (int)Math.Ceiling(ramp) + 1;
            double t = Math.Min(1.0, (chordRunAtLeast(current, totalColumns, cap) - 1) / ramp);
            return 1.0 - ceiling * t;
        }

        public static double NearFullChordDampen(DifficultyHitObject current, int totalColumns, double columnDelta)
        {
            if (totalColumns < 2)
                return 1.0;

            double speedScale = Math.Clamp(columnDelta / chord_speed_threshold_ms, 0.0, 1.0);
            double ceiling = near_full_chord_nerf * speedScale;

            if (ceiling <= 0)
                return 1.0;

            double ramp = Math.Max(1.0, near_full_chord_run_ramp);
            int cap = (int)Math.Ceiling(ramp) + 1;
            double t = Math.Min(1.0, (chordRunAtLeast(current, totalColumns - 1, cap) - 1) / ramp);
            return 1.0 - ceiling * t;
        }

        private static int chordRunAtLeast(DifficultyHitObject current, int minSize, int cap)
        {
            DifficultyHitObject groupStart = findGroupStart(current, out _);

            int run = 1;

            while (run < cap && groupStart.Previous(0) is { } previousGroupLast)
            {
                groupStart = findGroupStart(previousGroupLast, out int size);

                if (size < minSize)
                    break;

                run++;
            }

            return run;
        }

        private static DifficultyHitObject findGroupStart(DifficultyHitObject last, out int size)
        {
            DifficultyHitObject start = last;
            size = 1;

            for (int i = 0; last.Previous(i) is { } previous && Math.Abs(previous.StartTime - last.StartTime) <= CHORD_TOLERANCE_MS; i++)
            {
                start = previous;
                size++;
            }

            return start;
        }
    }
}
