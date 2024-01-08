// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using MathNet.Numerics;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty.UnstableRateEstimation
{
    public class OsuDefaultUnstableRateEstimator : OsuUnstableRateEstimator
    {
        private readonly double[] hitWindows = new double[3];

        public OsuDefaultUnstableRateEstimator(IRulesetInfo ruleset, DifficultyAttributes attributes)
            : base(ruleset, attributes)
        {
        }

        public override double? ComputeEstimatedUnstableRate(ScoreInfo score, bool withMisses = true)
        {
            SetStatistics(score);

            if (TotalSuccessfulHits == 0)
                return null;

            OsuDifficultyAttributes osuAttributes = (OsuDifficultyAttributes)Attributes;
            setHitWindows(score, osuAttributes);

            double objectsWithHitAccuracy = osuAttributes.HitCircleCount + osuAttributes.SliderCount;

            // The probability that a player hits a circle is unknown, but we can estimate it to be
            // the number of greats on circles divided by the number of circles, and then add one
            // to the number of circles as a bias correction.
            double greatProbability = CountGreat / (objectsWithHitAccuracy - CountMiss - CountMeh + 1.0);

            // Compute the deviation assuming 300s and 100s are normally distributed, and 50s are uniformly distributed.
            // Begin with the normal distribution first.
            double deviation = hitWindows[0] / (Math.Sqrt(2) * SpecialFunctions.ErfInv(greatProbability));
            deviation *= Math.Sqrt(1 - Math.Sqrt(2 / Math.PI) * hitWindows[1] * Math.Exp(-0.5 * Math.Pow(hitWindows[1] / deviation, 2))
                / (deviation * SpecialFunctions.Erf(hitWindows[1] / (Math.Sqrt(2) * deviation))));

            // Then compute the variance for 50s.
            double mehVariance = (hitWindows[2] * hitWindows[2] + hitWindows[1] * hitWindows[2] + hitWindows[1] * hitWindows[1]) / 3;

            // Find the total deviation.
            deviation = Math.Sqrt(((CountGreat + CountOk) * Math.Pow(deviation, 2) + CountMeh * mehVariance) / (CountGreat + CountOk + CountMeh));

            return deviation * 10;
        }

        private void setHitWindows(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            // Create a new track to properly calculate the hit windows of 100s and 50s.
            var track = new TrackVirtual(1);
            score.Mods.OfType<IApplicableToTrack>().ForEach(m => m.ApplyToTrack(track));
            double clockRate = track.Rate;

            hitWindows[0] = 80 - 6 * attributes.OverallDifficulty;
            hitWindows[1] = (140 - 8 * ((80 - hitWindows[0] * clockRate) / 6)) / clockRate;
            hitWindows[2] = (200 - 10 * ((80 - hitWindows[0] * clockRate) / 6)) / clockRate;
        }
    }
}
