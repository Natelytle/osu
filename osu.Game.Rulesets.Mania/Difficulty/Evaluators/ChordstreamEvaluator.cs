// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class ChordstreamEvaluator
    {
        private const double convergence_time_seconds = 20.0;

        private static readonly double tau = convergence_time_seconds / Math.Log(100);

        private double stamina;
        // private double lastInterval;

        public double EvaluateDifficultyOf(ManiaDifficultyHitObject obj)
        {
            if (obj.Previous(0) is null)
                return 0;

            double dt = Math.Max(0, (obj.StartTime - obj.Previous(0).StartTime) / 1000.0);

            var currChord = obj.CurrentChord;
            var prevChord = obj.PreviousChord(0);

            if (prevChord == null)
                return 0;

            int chordSize = currChord.Notes.Count;

            if (chordSize <= 1)
                return stamina;

            double chordWeight = Math.Pow(chordSize, 1.0);

            // Old implementation (gives 1 uniformity for every note in a chord except the first)
            // double interval = obj.StartTime - prevChord.StartTime;
            // double uniformity = lastInterval > 0 ? 1.0 - Math.Abs(interval - lastInterval) / interval : 1.0;
            // lastInterval = interval;

            // New implementation (same uniformity for every note in a chord)
            double uniformity = 1.0 - Math.Abs(currChord.DeltaTime - prevChord.DeltaTime) / currChord.DeltaTime;
            uniformity = Math.Clamp(uniformity, 0.0, 1.0);

            double bpmFactor = Math.Pow(currChord.HalfBpm / 200.0, 1.2);
            double baseValue = chordWeight * bpmFactor * uniformity;

            double k = 1.0 - Math.Exp(-dt / tau);
            stamina += (baseValue - stamina) * k;

            stamina = Math.Clamp(stamina, 0.0, 14.0);

            return stamina;
        }
    }
}
