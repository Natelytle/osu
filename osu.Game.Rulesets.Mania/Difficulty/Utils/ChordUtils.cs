// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;

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
        /// How many notes of the chord have been reached at <paramref name="current"/> inclusive, i.e.
        /// <paramref name="current"/>'s 1-based position within its chord. Strain accumulates per note, so
        /// each chord note is scaled by how far into the chord it sits rather than by the full chord size.
        /// </summary>
        public static int DepthInChord(ManiaDifficultyHitObject current) => current.Index - current.Row.Objects[0].Index + 1;

        public static double FullChordDampen(ManiaDifficultyHitObject current, int totalColumns, double columnDelta)
        {
            double speedScale = DiffUtils.ReverseLerp(columnDelta, 0.0, chord_speed_threshold_ms);
            double ceiling = full_chord_nerf * speedScale;

            if (ceiling <= 0)
                return 1.0;

            double ramp = Math.Max(1.0, full_chord_run_ramp);
            int cap = (int)Math.Ceiling(ramp) + 1;
            double t = DiffUtils.ReverseLerp(chordRunLengthOfAtLeastSize(totalColumns, current, cap) - 1, 0.0, ramp);
            return 1.0 - ceiling * t;
        }

        public static double NearFullChordDampen(ManiaDifficultyHitObject current, int totalColumns, double columnDelta)
        {
            if (totalColumns < 2)
                return 1.0;

            double speedScale = DiffUtils.ReverseLerp(columnDelta, 0.0, chord_speed_threshold_ms);
            double ceiling = near_full_chord_nerf * speedScale;

            if (ceiling <= 0)
                return 1.0;

            double ramp = Math.Max(1.0, near_full_chord_run_ramp);
            int cap = (int)Math.Ceiling(ramp) + 1;
            double t = DiffUtils.ReverseLerp(chordRunLengthOfAtLeastSize(totalColumns - 1, current, cap) - 1, 0.0, ramp);
            return 1.0 - ceiling * t;
        }

        private static int chordRunLengthOfAtLeastSize(int minSize, ManiaDifficultyHitObject current, int cap)
        {
            ManiaRow? currentRow = current.Row.Previous();

            int run = 1;

            while (currentRow?.Size >= minSize && run < cap)
            {
                currentRow = currentRow.Previous();
                run++;
            }

            return run;
        }
    }
}
