// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public static class ChordUtils
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

        /// <summary>
        /// The total number of notes in the chord <paramref name="current"/> belongs to. Independent of
        /// which note of the chord is queried: it anchors on the chord's last note then counts the group.
        /// </summary>
        public static int Size(DifficultyHitObject current)
        {
            DifficultyHitObject last = current;

            while (last.Next() is { } next && isSameChord(last, next))
                last = next;

            return DepthInChord(last);
        }

        /// <summary>
        /// How many notes of the chord have been reached at <paramref name="current"/> inclusive, i.e.
        /// <paramref name="current"/>'s 1-based position within its chord. Strain accumulates per note, so
        /// each chord note is scaled by how far into the chord it sits rather than by the full chord size.
        /// </summary>
        public static int DepthInChord(DifficultyHitObject current)
        {
            int depth = 1;

            while (current.Previous(depth - 1) is { } previous && isSameChord(current, previous))
                depth++;

            return depth;
        }

        private static bool isSameChord(DifficultyHitObject anchor, DifficultyHitObject other)
            => Math.Abs(other.StartTime - anchor.StartTime) <= CHORD_TOLERANCE_MS;

        public static double FullChordDampen(DifficultyHitObject current, int totalColumns, double columnDelta)
        {
            double speedScale = DiffUtils.ReverseLerp(columnDelta, 0.0, chord_speed_threshold_ms);
            double ceiling = full_chord_nerf * speedScale;

            if (ceiling <= 0)
                return 1.0;

            double ramp = Math.Max(1.0, full_chord_run_ramp);
            int cap = (int)Math.Ceiling(ramp) + 1;
            double t = DiffUtils.ReverseLerp(chordRunAtLeast(current, totalColumns, cap) - 1, 0.0, ramp);
            return 1.0 - ceiling * t;
        }

        public static double NearFullChordDampen(DifficultyHitObject current, int totalColumns, double columnDelta)
        {
            if (totalColumns < 2)
                return 1.0;

            double speedScale = DiffUtils.ReverseLerp(columnDelta, 0.0, chord_speed_threshold_ms);
            double ceiling = near_full_chord_nerf * speedScale;

            if (ceiling <= 0)
                return 1.0;

            double ramp = Math.Max(1.0, near_full_chord_run_ramp);
            int cap = (int)Math.Ceiling(ramp) + 1;
            double t = DiffUtils.ReverseLerp(chordRunAtLeast(current, totalColumns - 1, cap) - 1, 0.0, ramp);
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

            for (int i = 0; last.Previous(i) is { } previous && isSameChord(last, previous); i++)
            {
                start = previous;
                size++;
            }

            return start;
        }
    }
}
