// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuPerformanceCalculator : PerformanceCalculator
    {
        public const double PERFORMANCE_BASE_MULTIPLIER = 1.14; // This is being adjusted to keep the final pp value scaled around what it used to be when changing things.

        private double accuracy;
        private int scoreMaxCombo;
        private int countGreat;
        private int countOk;
        private int countMeh;
        private int countMiss;

        private double? deviation;

        private double effectiveMissCount;

        public OsuPerformanceCalculator()
            : base(new OsuRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var osuAttributes = (OsuDifficultyAttributes)attributes;

            accuracy = score.Accuracy;
            scoreMaxCombo = score.MaxCombo;
            countGreat = score.Statistics.GetValueOrDefault(HitResult.Great);
            countOk = score.Statistics.GetValueOrDefault(HitResult.Ok);
            countMeh = score.Statistics.GetValueOrDefault(HitResult.Meh);
            countMiss = score.Statistics.GetValueOrDefault(HitResult.Miss);
            deviation = calculateDeviation(score, osuAttributes);
            effectiveMissCount = calculateEffectiveMissCount(osuAttributes);

            double multiplier = PERFORMANCE_BASE_MULTIPLIER;

            if (score.Mods.Any(m => m is OsuModNoFail))
                multiplier *= Math.Max(0.90, 1.0 - 0.02 * effectiveMissCount);

            if (score.Mods.Any(m => m is OsuModSpunOut) && totalHits > 0)
                multiplier *= 1.0 - Math.Pow((double)osuAttributes.SpinnerCount / totalHits, 0.85);

            if (score.Mods.Any(h => h is OsuModRelax))
            {
                // https://www.desmos.com/calculator/bc9eybdthb
                // we use OD13.3 as maximum since it's the value at which great hitwidow becomes 0
                // this is well beyond currently maximum achievable OD which is 12.17 (DTx2 + DA with OD11)
                double okMultiplier = Math.Max(0.0, osuAttributes.OverallDifficulty > 0.0 ? 1 - Math.Pow(osuAttributes.OverallDifficulty / 13.33, 1.8) : 1.0);
                double mehMultiplier = Math.Max(0.0, osuAttributes.OverallDifficulty > 0.0 ? 1 - Math.Pow(osuAttributes.OverallDifficulty / 13.33, 5) : 1.0);

                // As we're adding Oks and Mehs to an approximated number of combo breaks the result can be higher than total hits in specific scenarios (which breaks some calculations) so we need to clamp it.
                effectiveMissCount = Math.Min(effectiveMissCount + countOk * okMultiplier + countMeh * mehMultiplier, totalHits);
            }

            double aimValue = computeAimValue(score, osuAttributes);
            double speedValue = computeSpeedValue(score, osuAttributes);
            double accuracyValue = computeAccuracyValue(score);
            double flashlightValue = computeFlashlightValue(score, osuAttributes);
            double totalValue = (aimValue + speedValue + accuracyValue + flashlightValue) * multiplier;

            return new OsuPerformanceAttributes
            {
                Aim = aimValue,
                Speed = speedValue,
                Accuracy = accuracyValue,
                Flashlight = flashlightValue,
                EffectiveMissCount = effectiveMissCount,
                Total = totalValue
            };
        }

        private double computeAimValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            double aimDifficulty = attributes.AimDifficulty;

            if (totalSuccessfulHits == 0 || deviation == null)
                return 0;

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (effectiveMissCount > 0)
            {
                // Since star rating is difficulty^AIM_EXP, we should raise the miss penalty to this power as well.
                aimDifficulty *= Math.Pow(linearInterpolatedMissPenalty(attributes), OsuDifficultyCalculator.AIM_EXP);
            }

            double aimValue = Math.Pow(aimDifficulty, 3);

            double approachRateFactor = 0.0;
            if (attributes.ApproachRate > 10.33)
                approachRateFactor = 0.3 * (attributes.ApproachRate - 10.33);
            else if (attributes.ApproachRate < 8.0)
                approachRateFactor = 0.05 * (8.0 - attributes.ApproachRate);

            if (score.Mods.Any(h => h is OsuModRelax))
                approachRateFactor = 0.0;

            aimValue *= 1.0 + approachRateFactor;

            if (score.Mods.Any(m => m is OsuModBlinds))
                aimValue *= 1.3 + (totalHits * (0.0016 / (1 + 2 * effectiveMissCount)) * Math.Pow(accuracy, 16)) * (1 - 0.003 * attributes.DrainRate * attributes.DrainRate);
            else if (score.Mods.Any(h => h is OsuModHidden))
            {
                // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
                aimValue *= 1.0 + 0.04 * (12.0 - attributes.ApproachRate);
            }

            aimValue *= 0.98 + Math.Pow(100.0 / 9, 2) / 2500; // OD 11 SS stays the same.
            aimValue *= 1 / (1 + Math.Pow(deviation.Value / 30, 4)); // Scale the aim value with deviation.

            return aimValue;
        }

        private double computeSpeedValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (score.Mods.Any(h => h is OsuModRelax) || deviation == null)
                return 0.0;

            double speedValue = Math.Pow(attributes.SpeedDifficulty, 3.0);

            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            speedValue *= lengthBonus;

            double approachRateFactor = 0.0;
            if (attributes.ApproachRate > 10.33)
                approachRateFactor = 0.3 * (attributes.ApproachRate - 10.33);

            speedValue *= 1.0 + approachRateFactor;

            if (score.Mods.Any(m => m is OsuModBlinds))
            {
                // Increasing the speed value by object count for Blinds isn't ideal, so the minimum buff is given.
                speedValue *= 1.12;
            }
            else if (score.Mods.Any(m => m is OsuModHidden))
            {
                // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
                speedValue *= 1.0 + 0.04 * (12.0 - attributes.ApproachRate);
            }

            speedValue *= 1 / (1 + Math.Pow(deviation.Value / 16, 5)); // Scale the speed value with speed deviation.

            speedValue *= 1 - Math.Min(effectiveMissCount / attributes.SpeedNoteCount, 1); // Scale the speed value with misses /slightly/.

            return speedValue;
        }

        private double computeAccuracyValue(ScoreInfo score)
        {
            if (score.Mods.Any(h => h is OsuModRelax) || deviation == null)
                return 0.0;

            double accuracyValue = 120 * Math.Pow(7.5 / deviation.Value, 2);

            // Increasing the accuracy value by object count for Blinds isn't ideal, so the minimum buff is given.
            if (score.Mods.Any(m => m is OsuModBlinds))
                accuracyValue *= 1.14;
            else if (score.Mods.Any(m => m is OsuModHidden))
                accuracyValue *= 1.08;

            if (score.Mods.Any(m => m is OsuModFlashlight))
                accuracyValue *= 1.02;

            return accuracyValue;
        }

        private double computeFlashlightValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (!score.Mods.Any(h => h is OsuModFlashlight) || deviation is null)
                return 0.0;

            double flashlightValue = Math.Pow(attributes.FlashlightDifficulty, 2.0) * 25.0;

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (effectiveMissCount > 0)
                flashlightValue *= 0.97 * Math.Pow(1 - Math.Pow(effectiveMissCount / totalHits, 0.775), Math.Pow(effectiveMissCount, .875));

            // Scale by max combo
            flashlightValue *= attributes.MaxCombo <= 0 ? 1.0 : Math.Min(Math.Pow(scoreMaxCombo, 0.8) / Math.Pow(attributes.MaxCombo, 0.8), 1.0);

            // Account for shorter maps having a higher ratio of 0 combo/100 combo flashlight radius.
            flashlightValue *= 0.7 + 0.1 * Math.Min(1.0, totalHits / 200.0) +
                               (totalHits > 200 ? 0.2 * Math.Min(1.0, (totalHits - 200) / 200.0) : 0.0);

            // Scale the flashlight value with deviation
            flashlightValue *= SpecialFunctions.Erf(50 / (Math.Sqrt(2) * deviation.Value));

            return flashlightValue;
        }

        /// <summary>
        /// Computes an upper bound on the player's tap deviation based on the OD, number of circles and sliders, and the hit judgements,
        /// assuming the player's mean hit error is 0. The estimation is consistent in that two SS scores on the same map with the same settings
        /// will always return the same deviation. Sliders are treated as circles with a 50 hit window. Misses are ignored because they are usually due to misaiming.
        /// 300s and 100s are assumed to follow a normal distribution, whereas 50s are assumed to follow a uniform distribution.
        /// </summary>
        private double? calculateDeviation(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (totalSuccessfulHits == 0)
                return null;

            // Create a new track to properly calculate the hit windows of 100s and 50s.
            var track = new TrackVirtual(1);
            score.Mods.OfType<IApplicableToTrack>().ForEach(m => m.ApplyToTrack(track));
            double clockRate = track.Rate;

            double hitWindow300 = 80 - 6 * attributes.OverallDifficulty;
            double hitWindow100 = (140 - 8 * ((80 - hitWindow300 * clockRate) / 6)) / clockRate;
            double hitWindow50 = (200 - 10 * ((80 - hitWindow300 * clockRate) / 6)) / clockRate;

            int circleCount = attributes.HitCircleCount;
            int missCountCircles = Math.Min(countMiss, circleCount);
            int mehCountCircles = Math.Min(countMeh, circleCount - missCountCircles);
            int okCountCircles = Math.Min(countOk, circleCount - missCountCircles - mehCountCircles);
            int greatCountCircles = Math.Max(0, circleCount - missCountCircles - mehCountCircles - okCountCircles);

            // Assume 100s, 50s, and misses happen on circles. If there are less non-300s on circles than 300s,
            // compute the deviation on circles.
            if (greatCountCircles > 0)
            {
                double n = circleCount - missCountCircles - mehCountCircles;
                const double z = 2.32634787404; // 99% critical value for the normal distribution (one-tailed).

                // Proportion of greats hit on circles, ignoring misses and 50s.
                double p = greatCountCircles / n;

                // We can be 99% confident that p is at least this value.
                double pLowerBound = (n * p + z * z / 2) / (n + z * z) - z / (n + z * z) * Math.Sqrt(n * p * (1 - p) + z * z / 4);

                // Compute the deviation assuming 300s and 100s are normally distributed, and 50s are uniformly distributed.
                // Begin with 300s and 100s first. Ignoring 50s, we can be 99% confident that the deviation is not higher than:
                double deviationOnCircles = hitWindow300 / (Math.Sqrt(2) * SpecialFunctions.ErfInv(pLowerBound));
                deviationOnCircles *= Math.Sqrt(1 - Math.Sqrt(2 / Math.PI) * hitWindow100 * Math.Exp(-0.5 * Math.Pow(hitWindow100 / deviationOnCircles, 2))
                    / (deviationOnCircles * SpecialFunctions.Erf(hitWindow100 / (Math.Sqrt(2) * deviationOnCircles))));

                // Then compute the variance for 50s.
                double mehVariance = (hitWindow50 * hitWindow50 + hitWindow100 * hitWindow50 + hitWindow100 * hitWindow100) / 3;

                // Find the total deviation.
                deviationOnCircles = Math.Sqrt(((greatCountCircles + okCountCircles) * Math.Pow(deviationOnCircles, 2) + mehCountCircles * mehVariance) / (greatCountCircles + okCountCircles + mehCountCircles));

                return deviationOnCircles;
            }

            // If there are more non-300s than there are circles, compute the deviation on sliders instead.
            // Here, all that matters is whether or not the slider was missed, since it is impossible
            // to get a 100 or 50 on a slider by mis-tapping it.
            int sliderCount = attributes.SliderCount;
            int missCountSliders = Math.Min(sliderCount, countMiss - missCountCircles);
            int greatCountSliders = sliderCount - missCountSliders;

            // We only get here if nothing was hit. In this case, there is no estimate for deviation.
            // Note that this is never negative, so checking if this is only equal to 0 makes sense.
            if (greatCountSliders == 0)
            {
                return null;
            }

            double greatProbabilitySlider = greatCountSliders / (sliderCount + 1.0);
            double deviationOnSliders = hitWindow50 / (Math.Sqrt(2) * SpecialFunctions.ErfInv(greatProbabilitySlider));

            return deviationOnSliders;
        }

        private double calculateEffectiveMissCount(OsuDifficultyAttributes attributes)
        {
            // Guess the number of misses + slider breaks from combo
            double comboBasedMissCount = 0.0;

            if (attributes.SliderCount > 0)
            {
                double fullComboThreshold = attributes.MaxCombo - 0.1 * attributes.SliderCount;
                if (scoreMaxCombo < fullComboThreshold)
                    comboBasedMissCount = fullComboThreshold / Math.Max(1.0, scoreMaxCombo);
            }

            // Clamp miss count to maximum amount of possible breaks
            comboBasedMissCount = Math.Min(comboBasedMissCount, countOk + countMeh + countMiss);

            return Math.Max(countMiss, comboBasedMissCount);
        }

        private double linearInterpolatedMissPenalty(OsuDifficultyAttributes attributes)
        {
            double[] misscounts = attributes.AimMisscounts;
            const double penalty_per_misscount = 1.0 / 20;

            int? index = null;

            for (int i = 0; i < 20; i++)
            {
                if (misscounts[i] < effectiveMissCount) continue;

                index = i;
                break;
            }

            // If no misscount past 0 misses is a greater value than effectiveMissCount,
            // return 0. This is an edge case that can occur in the case of a single note.
            if (index is null)
                return 0;

            // We don't save a misscount value for FCs, so only get the misscount from the previous bin if it exists.
            double lowestMisscount = index > 0 ? misscounts[index.Value - 1] : 0;
            double lowestPenalty = penalty_per_misscount * index.Value;

            double highestMisscount = misscounts[index.Value];
            double highestPenalty = penalty_per_misscount * (index.Value + 1);

            double penalty = Interpolation.Lerp(lowestPenalty, highestPenalty, (effectiveMissCount - lowestMisscount) / (highestMisscount - lowestMisscount));

            return 1 - penalty;
        }

        /// <summary>
        /// Imagine a map with n objects, where all objects have equal difficulty d.
        /// d * sqrt(2) * s(n,0) will return the FC difficulty of that map.
        /// d * sqrt(2) * s(n,m) will return the m-miss difficulty of that map.
        /// Since we are given FC difficulty, for a score with m misses, we can obtain
        /// the difficulty for m misses by multiplying the difficulty by s(n,m) / s(n,0).
        /// Note that the term d * sqrt(2) gets canceled when taking the ratio.
        /// </summary>
        private double calculateMissPenalty()
        {
            int n = totalHits;

            if (n == 0)
                return 0;

            return s(effectiveMissCount) / s(0);

            double s(double m)
            {
                const double z = 2.0537; // 98% critical value for the normal distribution (one-tailed).

                // Proportion of circles hit.
                double p = (n - m) / n;

                // We can be 98% confident that p is at least this value.
                double pLowerBound = (n * p + z * z / 2) / (n + z * z) - z / (n + z * z) * Math.Sqrt(n * p * (1 - p) + z * z / 4);

                return SpecialFunctions.ErfInv(pLowerBound);
            }
        }

        private int totalHits => countGreat + countOk + countMeh + countMiss;
        private int totalSuccessfulHits => countGreat + countOk + countMeh;
    }
}
