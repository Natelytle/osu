// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class SpeedstreamEvaluator
    {
        private const double convergence_time_seconds = 30.0;

        // private readonly double tau = convergence_time_seconds / Math.Log(100);

        // private double lastInterval;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.Previous(0) is null)
                return 0;

            ManiaDifficultyHitObject currObj = (ManiaDifficultyHitObject)current;

            // double dt = Math.Max(0, (obj.StartTime - obj.Previous(0).StartTime) / 1000.0);

            var currChord = currObj.CurrentChord;
            var prevChord = currObj.PreviousChord(0);

            if (prevChord == null)
                return 0;

            double chordSize = currChord.Notes.Count;

            double densityScore = 1.0 / Math.Pow(chordSize, 0.75);

            // Old implementation (gives 1 uniformity for every note in a chord except the first)
            // double interval = obj.StartTime - prevChord.StartTime;
            // double uniformity = lastInterval > 0 ? 1.0 - Math.Abs(interval - lastInterval) / interval : 1.0;
            // lastInterval = interval;

            // New implementation (same uniformity for every note in a chord)
            double uniformity = 1.0 - Math.Abs(currChord.DeltaTime - prevChord.DeltaTime) / currChord.DeltaTime;
            uniformity = Math.Clamp(uniformity, 0.0, 1.0);

            double baseValue = bpmToRatingCurve(currChord.HalfBpm) * densityScore * uniformity;

            return baseValue;

            // double k = 1.0 - Math.Exp(-dt / tau);
            //
            // stamina += (baseValue - stamina) * k;
            //
            // stamina = Math.Clamp(stamina, 0.0, 14.0);
            //
            // return stamina;
        }

        private static double bpmToRatingCurve(double bpm) => Math.Pow(bpm / 300.0, 2.0);
    }
}
