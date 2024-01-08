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
    public class OsuLegacyUnstableRateEstimator : OsuUnstableRateEstimator
    {
        private readonly double[] hitWindows = new double[3];

        public OsuLegacyUnstableRateEstimator(IRulesetInfo ruleset, DifficultyAttributes attributes)
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

            double circleCount = osuAttributes.HitCircleCount;
            double missCountCircles = Math.Min(CountMiss, circleCount);
            double mehCountCircles = Math.Min(CountMeh, circleCount - missCountCircles);
            double okCountCircles = Math.Min(CountOk, circleCount - missCountCircles - mehCountCircles);
            double greatCountCircles = Math.Max(0, circleCount - missCountCircles - mehCountCircles - okCountCircles);

            // Assume 100s, 50s, and misses happen on circles. If there are less non-300s on circles than 300s,
            // compute the deviation on circles.
            if (greatCountCircles > 0)
            {
                // The probability that a player hits a circle is unknown, but we can estimate it to be
                // the number of greats on circles divided by the number of circles, and then add one
                // to the number of circles as a bias correction.
                double greatProbabilityCircle = greatCountCircles / (circleCount - missCountCircles - mehCountCircles + 1.0);

                // Compute the deviation assuming 300s and 100s are normally distributed, and 50s are uniformly distributed.
                // Begin with the normal distribution first.
                double deviationOnCircles = hitWindows[0] / (Math.Sqrt(2) * SpecialFunctions.ErfInv(greatProbabilityCircle));
                deviationOnCircles *= Math.Sqrt(1 - Math.Sqrt(2 / Math.PI) * hitWindows[1] * Math.Exp(-0.5 * Math.Pow(hitWindows[1] / deviationOnCircles, 2))
                    / (deviationOnCircles * SpecialFunctions.Erf(hitWindows[1] / (Math.Sqrt(2) * deviationOnCircles))));

                // Then compute the variance for 50s.
                double mehVariance = (hitWindows[2] * hitWindows[2] + hitWindows[1] * hitWindows[2] + hitWindows[1] * hitWindows[1]) / 3;

                // Find the total deviation.
                deviationOnCircles = Math.Sqrt(((greatCountCircles + okCountCircles) * Math.Pow(deviationOnCircles, 2) + mehCountCircles * mehVariance) / (greatCountCircles + okCountCircles + mehCountCircles));

                return deviationOnCircles * 10;
            }

            // If there are more non-300s than there are circles, compute the deviation on sliders instead.
            // Here, all that matters is whether or not the slider was missed, since it is impossible
            // to get a 100 or 50 on a slider by mis-tapping it.
            double sliderCount = osuAttributes.SliderCount;
            double missCountSliders = Math.Min(sliderCount, CountMiss - missCountCircles);
            double greatCountSliders = sliderCount - missCountSliders;

            // We only get here if nothing was hit. In this case, there is no estimate for deviation.
            // Note that this is never negative, so checking if this is only equal to 0 makes sense.
            if (greatCountSliders == 0)
            {
                return null;
            }

            double greatProbabilitySlider = greatCountSliders / (sliderCount + 1.0);
            double deviationOnSliders = hitWindows[2] / (Math.Sqrt(2) * SpecialFunctions.ErfInv(greatProbabilitySlider));

            return deviationOnSliders * 10;
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
