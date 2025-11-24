// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using FFmpeg.AutoGen;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    internal sealed class SpeedstreamEvaluator : ManiaEvaluator
    {
        private const double convergence_time_seconds = 30.0;

        private readonly double tau = convergence_time_seconds / Math.Log(100);

        private double stamina;
        private double lastTime;
        private double lastInterval;

        public SpeedstreamEvaluator(ManiaDifficultyHitObject firstObj)
            : base(firstObj)
        {
            lastTime = firstObj.StartTime;
        }

        public override double EvaluateDifficultyOf(ManiaDifficultyHitObject obj)
        {
            double dt = Math.Max(0, (obj.StartTime - lastTime) / 1000.0);
            lastTime = obj.StartTime;

            var currentChord = GetChordFor(obj);
            var previousChord = GetPreviousChord(currentChord);

            if (previousChord == null)
                return 0;

            double chordSize = currentChord.Notes.Count;

            double densityScore = 1.0 / Math.Pow(chordSize, 0.75);

            double interval = obj.StartTime - previousChord.StartTime;
            double uniformity = lastInterval > 0
                ? 1.0 - Math.Abs(interval - lastInterval) / interval
                : 1.0;

            uniformity = Math.Clamp(uniformity, 0.0, 1.0);
            lastInterval = interval;

            double baseValue = BpmToRatingCurve(currentChord.Bpm2)
                             * densityScore
                             * uniformity;

            double k = 1.0 - Math.Exp(-dt / tau);

            stamina += (baseValue - stamina) * k;

            stamina = Math.Clamp(stamina, 0.0, 14.0);

            return stamina;
        }

        protected override double BpmToRatingCurve(double bpm) =>
            Math.Pow(bpm / 300.0, 2.0);
    }
}
