// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using MathNet.Numerics;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.UnstableRateEstimator;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Taiko.Difficulty.UnstableRateEstimation
{
    public class TaikoUnstableRateEstimator : UnstableRateEstimator
    {
        private readonly double[] hitWindows = new double[2];

        public TaikoUnstableRateEstimator(IRulesetInfo ruleset, DifficultyAttributes attributes)
            : base(ruleset, attributes)
        {
        }

        /// <inheritdoc />
        /// <exception cref="T:MathNet.Numerics.Optimization.MaximumIterationsException">
        /// Thrown when the optimization algorithm fails to converge.
        /// </exception>
        public override double? ComputeEstimatedUnstableRate(ScoreInfo score, bool withMisses = true)
        {
            SetStatistics(score);

            if (TotalSuccessfulHits == 0)
                return null;

            TaikoDifficultyAttributes taikoAttributes = (TaikoDifficultyAttributes)Attributes;
            setHitWindows(taikoAttributes, score);

            // Determines the probability of a deviation leading to the score's hit evaluations. The curve's apex represents the most probable deviation.
            double likelihoodGradient(double d)
            {
                if (d <= 0)
                    return 0;

                double p300 = LogDiff(0, LogComplementProbOfHit(hitWindows[0], d));
                double p100 = LogDiff(LogComplementProbOfHit(hitWindows[0], d), LogComplementProbOfHit(hitWindows[1], d));
                double p0 = 0;

                // In some cases you may want to estimate without including misses. For example, estimating the results screen UR value.
                if (withMisses)
                    p0 = LogComplementProbOfHit(hitWindows[1], d);

                double gradient = Math.Exp(
                    (CountGreat * p300
                     + (CountOk + 0.5) * p100
                     + CountMiss * p0) / TotalHits
                );

                return -gradient;
            }

            double deviation = FindMinimum.OfScalarFunction(likelihoodGradient, 30);

            return deviation * 10;
        }

        private void setHitWindows(TaikoDifficultyAttributes attributes, ScoreInfo score)
        {
            // Create a new track to properly calculate the hit window of 100s.
            var track = new TrackVirtual(10000);
            score.Mods.OfType<IApplicableToTrack>().ForEach(m => m.ApplyToTrack(track));
            double clockRate = track.Rate;

            double overallDifficulty = (50 - attributes.GreatHitWindow * clockRate) / 3;
            hitWindows[0] = attributes.GreatHitWindow;
            hitWindows[1] = overallDifficulty <= 5 ? (120 - 8 * overallDifficulty) / clockRate : (80 - 6 * (overallDifficulty - 5)) / clockRate;
        }
    }
}
