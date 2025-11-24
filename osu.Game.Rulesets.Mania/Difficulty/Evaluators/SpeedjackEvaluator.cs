// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    internal sealed class SpeedjackEvaluator : ManiaEvaluator
    {
        private const double convergence_time_seconds = 60.0;

        private readonly double tau = convergence_time_seconds / Math.Log(100);

        private double stamina;
        private double lastTime;

        public SpeedjackEvaluator(ManiaDifficultyHitObject firstObj)
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

            int sharedColumns = currentChord.Notes
                                            .Select(n => n.Column)
                                            .Intersect(previousChord.Notes.Select(n => n.Column))
                                            .Count();

            if (sharedColumns == 0)
                return 0;

            double density = Math.Min(1, (double)sharedColumns / currentChord.Notes.Count);
            double baseValue = BpmToRatingCurve(currentChord.Bpm4) * Math.Pow(1 - density, 1.3);

            double k = 1.0 - Math.Exp(-dt / tau);
            stamina += (baseValue - stamina) * k;

            stamina = Math.Clamp(stamina, 0.0, 14.0);

            return stamina;
        }

        protected override double BpmToRatingCurve(double bpm) =>
            Math.Pow(2.0, bpm / 46.5);
    }
}
