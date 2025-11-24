// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class ChordjackEvaluator
    {
        private const double convergence_time_seconds = 60.0;

        private static readonly double tau = convergence_time_seconds / Math.Log(100);

        private double stamina;

        public double EvaluateDifficultyOf(ManiaDifficultyHitObject obj)
        {
            if (obj.Previous(0) is null)
                return 0;

            double dt = Math.Max(0, (obj.StartTime - obj.Previous(0).StartTime) / 1000.0);

            var currentChord = obj.CurrentChord;
            var previousChord = obj.PreviousChord(0);

            if (previousChord == null)
                return 0;

            int sharedColumns = currentChord.Notes
                                            .Select(n => n.Column)
                                            .Intersect(previousChord.Notes.Select(n => n.Column))
                                            .Count();

            if (sharedColumns == 0)
                return 0;

            double density = Math.Min(1, (double)sharedColumns / currentChord.Notes.Count);
            double baseValue = bpmToRatingCurve(currentChord.QuarterBpm) * Math.Pow(density, 3);

            double k = 1.0 - Math.Exp(-dt / tau);
            stamina += (baseValue - stamina) * k;

            stamina = Math.Clamp(stamina, 0.0, 14.0);

            return stamina;
        }

        private static double bpmToRatingCurve(double bpm) => Math.Pow(2.0, bpm / 47.0);
    }
}
