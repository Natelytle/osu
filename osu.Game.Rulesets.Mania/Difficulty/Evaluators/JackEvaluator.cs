// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class JackEvaluator
    {
        /// <summary>
        /// The maximum gap between two notes in the same column for them to still be considered part of a jack.
        /// </summary>
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

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject hitObject)
        {
            double lastStartTime = hitObject.LastStartTimeInColumn(hitObject.Column);
            double columnDelta = double.IsNegativeInfinity(lastStartTime) ? double.PositiveInfinity : hitObject.StartTime - lastStartTime;
            int chordSize = ChordEvaluator.Size(hitObject);

            if (columnDelta > JACK_WINDOW_MS)
                return 0.0;

            double tapRate = 1.0 / (Math.Max(columnDelta, 1.0) / 1000.0 + jack_rate_offset);

            double chordSpeedFactor = Math.Clamp(chord_speed_threshold_ms / columnDelta, 0.1, 2.0);
            double chordjackMultiplier = Math.Max(chordjack_multiplier_minimum, 1.0 + chordjack_buff * chordSpeedFactor * (chordSize - 1));
            double speedBuff = 1.0 + jack_speed_buff * DifficultyCalculationUtils.Logistic(tapRate, jack_speed_buff_midpoint, jack_speed_buff_slope);

            double rawStrain = tapRate * chordjackMultiplier * speedBuff;
            double strain = jack_scale * Math.Pow(rawStrain, jack_convex);

            if (chordSize >= 2)
                strain *= chordjack_nerf;
            else
                strain *= TrillEvaluator.TrillFactor(hitObject);

            return strain;
        }
    }
}
