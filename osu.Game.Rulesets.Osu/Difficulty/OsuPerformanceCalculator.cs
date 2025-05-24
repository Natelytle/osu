// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuPerformanceCalculator : PerformanceCalculator
    {
        private bool usingClassicSliderAccuracy;
        private bool usingScoreV2;

        private int scoreMaxCombo;
        private int countGreat;
        private int countOk;
        private int countMeh;
        private int countMiss;

        private double adjustedAccuracy;
        private double adjustedCountGreat;
        private double adjustedCountOk;
        private double adjustedCountMeh;

        /// <summary>
        /// Missed slider ticks that includes missed reverse arrows. Will only be correct on non-classic scores
        /// </summary>
        private int countSliderTickMiss;

        /// <summary>
        /// Amount of missed slider tails that don't break combo. Will only be correct on non-classic scores
        /// </summary>
        private int countSliderEndsDropped;

        /// <summary>
        /// Estimated total amount of combo breaks
        /// </summary>
        private double effectiveMissCount;

        private double clockRate;
        private double greatHitWindow;
        private double okHitWindow;
        private double mehHitWindow;
        private double overallDifficulty;
        private double approachRate;

        private double? speedDeviation;

        private double aimEstimatedSliderBreaks;
        private double speedEstimatedSliderBreaks;

        public OsuPerformanceCalculator()
            : base(new OsuRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var osuAttributes = (OsuDifficultyAttributes)attributes;

            usingClassicSliderAccuracy = score.Mods.OfType<OsuModClassic>().Any(m => m.NoSliderHeadAccuracy.Value);
            usingScoreV2 = score.Mods.Any(m => m is ModScoreV2);

            scoreMaxCombo = score.MaxCombo;
            countGreat = score.Statistics.GetValueOrDefault(HitResult.Great);
            countOk = score.Statistics.GetValueOrDefault(HitResult.Ok);
            countMeh = score.Statistics.GetValueOrDefault(HitResult.Meh);
            countMiss = score.Statistics.GetValueOrDefault(HitResult.Miss);
            countSliderEndsDropped = osuAttributes.SliderCount - score.Statistics.GetValueOrDefault(HitResult.SliderTailHit);
            countSliderTickMiss = score.Statistics.GetValueOrDefault(HitResult.LargeTickMiss);
            effectiveMissCount = countMiss;

            var difficulty = score.BeatmapInfo!.Difficulty.Clone();

            score.Mods.OfType<IApplicableToDifficulty>().ForEach(m => m.ApplyToDifficulty(difficulty));

            clockRate = ModUtils.CalculateRateWithMods(score.Mods);

            HitWindows hitWindows = new OsuHitWindows();
            hitWindows.SetDifficulty(difficulty.OverallDifficulty);

            greatHitWindow = hitWindows.WindowFor(HitResult.Great) / clockRate;
            okHitWindow = hitWindows.WindowFor(HitResult.Ok) / clockRate;
            mehHitWindow = hitWindows.WindowFor(HitResult.Meh) / clockRate;

            double preempt = IBeatmapDifficultyInfo.DifficultyRange(difficulty.ApproachRate, 1800, 1200, 450) / clockRate;

            overallDifficulty = (80 - greatHitWindow) / 6;
            approachRate = preempt > 1200 ? (1800 - preempt) / 120 : (1200 - preempt) / 150 + 5;

            double comboBasedEstimatedMissCount = calculateComboBasedEstimatedMissCount(osuAttributes);
            double? scoreBasedEstimatedMissCount = null;

            if (usingClassicSliderAccuracy && score.LegacyTotalScore != null)
            {
                var legacyScoreMissCalculator = new OsuLegacyScoreMissCalculator(score, osuAttributes);
                scoreBasedEstimatedMissCount = legacyScoreMissCalculator.Calculate();

                effectiveMissCount = scoreBasedEstimatedMissCount.Value;
            }
            else
            {
                // Use combo-based miss count if this isn't a legacy score
                effectiveMissCount = comboBasedEstimatedMissCount;
            }

            effectiveMissCount = Math.Max(countMiss, effectiveMissCount);
            effectiveMissCount = Math.Min(totalHits, effectiveMissCount);

            double multiplier = OsuDifficultyCalculator.CalculateDifficultyMultiplier(score.Mods, totalHits, osuAttributes.SpinnerCount);

            if (score.Mods.Any(m => m is OsuModNoFail))
                multiplier *= Math.Max(0.90, 1.0 - 0.02 * effectiveMissCount);

            if (score.Mods.Any(h => h is OsuModRelax))
            {
                // https://www.desmos.com/calculator/vspzsop6td
                // we use OD13.3 as maximum since it's the value at which great hitwidow becomes 0
                // this is well beyond currently maximum achievable OD which is 12.17 (DTx2 + DA with OD11)
                double okMultiplier = 0.75 * Math.Max(0.0, overallDifficulty > 0.0 ? 1 - overallDifficulty / 13.33 : 1.0);
                double mehMultiplier = Math.Max(0.0, overallDifficulty > 0.0 ? 1 - Math.Pow(overallDifficulty / 13.33, 5) : 1.0);

                // As we're adding Oks and Mehs to an approximated number of combo breaks the result can be higher than total hits in specific scenarios (which breaks some calculations) so we need to clamp it.
                effectiveMissCount = Math.Min(effectiveMissCount + countOk * okMultiplier + countMeh * mehMultiplier, totalHits);
            }

            speedDeviation = calculateSpeedDeviation(osuAttributes);

            (adjustedCountGreat, adjustedCountOk, adjustedCountMeh) = calculateAdjustedAccuracy(OsuDifficultyCalculator.ADJUSTED_OVERALL_DIFFICULTY, osuAttributes);
            adjustedAccuracy = totalHits > 0 ? (adjustedCountGreat + adjustedCountOk / 3.0 + adjustedCountMeh / 6.0) / totalHits : 0;

            double aimValue = computeAimValue(score, osuAttributes);
            double speedValue = computeSpeedValue(score, osuAttributes);
            double accuracyValue = computeAccuracyValue(score, osuAttributes);
            double flashlightValue = computeFlashlightValue(score, osuAttributes);

            double totalValue =
                Math.Pow(
                    Math.Pow(aimValue, 1.1) +
                    Math.Pow(speedValue, 1.1) +
                    Math.Pow(accuracyValue, 1.1) +
                    Math.Pow(flashlightValue, 1.1), 1.0 / 1.1
                ) * multiplier;

            return new OsuPerformanceAttributes
            {
                Aim = aimValue,
                Speed = speedValue,
                Accuracy = accuracyValue,
                Flashlight = flashlightValue,
                EffectiveMissCount = effectiveMissCount,
                ComboBasedEstimatedMissCount = comboBasedEstimatedMissCount,
                ScoreBasedEstimatedMissCount = scoreBasedEstimatedMissCount,
                AimEstimatedSliderBreaks = aimEstimatedSliderBreaks,
                SpeedEstimatedSliderBreaks = speedEstimatedSliderBreaks,
                SpeedDeviation = speedDeviation,
                AdjustedAccuracy = adjustedAccuracy,
                AdjustedCountOk = adjustedCountOk,
                AdjustedCountMeh = adjustedCountMeh,
                Total = adjustedCountOk + adjustedCountMeh
            };
        }

        private double computeAimValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (score.Mods.Any(h => h is OsuModAutopilot))
                return 0.0;

            double aimDifficulty = attributes.AimDifficulty;

            if (attributes.SliderCount > 0 && attributes.AimDifficultSliderCount > 0)
            {
                double estimateImproperlyFollowedDifficultSliders;

                if (usingClassicSliderAccuracy)
                {
                    // When the score is considered classic (regardless if it was made on old client or not) we consider all missing combo to be dropped difficult sliders
                    int maximumPossibleDroppedSliders = totalImperfectHits;
                    estimateImproperlyFollowedDifficultSliders = Math.Clamp(Math.Min(maximumPossibleDroppedSliders, attributes.MaxCombo - scoreMaxCombo), 0, attributes.AimDifficultSliderCount);
                }
                else
                {
                    // We add tick misses here since they too mean that the player didn't follow the slider properly
                    // We however aren't adding misses here because missing slider heads has a harsh penalty by itself and doesn't mean that the rest of the slider wasn't followed properly
                    estimateImproperlyFollowedDifficultSliders = Math.Clamp(countSliderEndsDropped + countSliderTickMiss, 0, attributes.AimDifficultSliderCount);
                }

                double sliderNerfFactor = (1 - attributes.SliderFactor) * Math.Pow(1 - estimateImproperlyFollowedDifficultSliders / attributes.AimDifficultSliderCount, 3) + attributes.SliderFactor;
                aimDifficulty *= sliderNerfFactor;
            }

            double aimValue = OsuStrainSkill.DifficultyToPerformance(aimDifficulty);

            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            aimValue *= lengthBonus;

            if (effectiveMissCount > 0)
            {
                aimEstimatedSliderBreaks = calculateEstimatedSliderBreaks(attributes.AimTopWeightedSliderFactor, attributes);
                aimValue *= calculateMissPenalty(effectiveMissCount + aimEstimatedSliderBreaks, attributes.AimDifficultStrainCount);
            }

            // TC bonuses are excluded when blinds is present as the increased visual difficulty is unimportant when notes cannot be seen.
            if (score.Mods.Any(m => m is OsuModBlinds))
                aimValue *= 1.3 + (totalHits * (0.0016 / (1 + 2 * effectiveMissCount)) * Math.Pow(adjustedAccuracy, 16)) * (1 - 0.003 * attributes.DrainRate * attributes.DrainRate);
            else if (score.Mods.Any(m => m is OsuModTraceable))
            {
                // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
                aimValue *= 1.0 + 0.04 * (12.0 - approachRate);
            }

            aimValue *= adjustedAccuracy;

            return aimValue;
        }

        private double computeSpeedValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (score.Mods.Any(h => h is OsuModRelax) || speedDeviation == null)
                return 0.0;

            double speedValue = OsuStrainSkill.DifficultyToPerformance(attributes.SpeedDifficulty);

            double lengthBonus = 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            speedValue *= lengthBonus;

            if (effectiveMissCount > 0)
            {
                speedEstimatedSliderBreaks = calculateEstimatedSliderBreaks(attributes.SpeedTopWeightedSliderFactor, attributes);
                speedValue *= calculateMissPenalty(effectiveMissCount + speedEstimatedSliderBreaks, attributes.SpeedDifficultStrainCount);
            }

            // TC bonuses are excluded when blinds is present as the increased visual difficulty is unimportant when notes cannot be seen.
            if (score.Mods.Any(m => m is OsuModBlinds))
            {
                // Increasing the speed value by object count for Blinds isn't ideal, so the minimum buff is given.
                speedValue *= 1.12;
            }
            else if (score.Mods.Any(m => m is OsuModTraceable))
            {
                // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
                speedValue *= 1.0 + 0.04 * (12.0 - approachRate);
            }

            double speedHighDeviationMultiplier = calculateSpeedHighDeviationNerf(attributes);
            speedValue *= speedHighDeviationMultiplier;

            // Calculate accuracy assuming the worst case scenario
            double relevantTotalDiff = Math.Max(0, totalHits - attributes.SpeedNoteCount);
            double relevantCountGreat = Math.Max(0, adjustedCountGreat - relevantTotalDiff);
            double relevantCountOk = Math.Max(0, adjustedCountOk - Math.Max(0, relevantTotalDiff - adjustedCountGreat));
            double relevantCountMeh = Math.Max(0, adjustedCountMeh - Math.Max(0, relevantTotalDiff - adjustedCountGreat - adjustedCountOk));
            double relevantAccuracy = attributes.SpeedNoteCount == 0 ? 0 : (relevantCountGreat * 6.0 + relevantCountOk * 2.0 + relevantCountMeh) / (attributes.SpeedNoteCount * 6.0);

            // Scale the speed value with accuracy and OD.
            speedValue *= Math.Pow((adjustedAccuracy + relevantAccuracy) / 2.0, (14.5 - OsuDifficultyCalculator.ADJUSTED_OVERALL_DIFFICULTY) / 2);

            return speedValue;
        }

        private double computeAccuracyValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (score.Mods.Any(h => h is OsuModRelax))
                return 0.0;

            // This percentage only considers HitCircles of any value - in this part of the calculation we focus on hitting the timing hit window.
            double betterAccuracyPercentage;
            int amountHitObjectsWithAccuracy = attributes.HitCircleCount;
            if (!usingClassicSliderAccuracy || usingScoreV2)
                amountHitObjectsWithAccuracy += attributes.SliderCount;

            if (amountHitObjectsWithAccuracy > 0)
                betterAccuracyPercentage = ((adjustedCountGreat - Math.Max(totalHits - amountHitObjectsWithAccuracy, 0)) * 6 + adjustedCountOk * 2 + adjustedCountMeh) / (amountHitObjectsWithAccuracy * 6);
            else
                betterAccuracyPercentage = 0;

            // It is possible to reach a negative accuracy with this formula. Cap it at zero - zero points.
            if (betterAccuracyPercentage < 0)
                betterAccuracyPercentage = 0;

            // Lots of arbitrary values from testing.
            // Considering to use derivation from perfect accuracy in a probabilistic manner - assume normal distribution.
            double accuracyValue = Math.Pow(1.52163, OsuDifficultyCalculator.ADJUSTED_OVERALL_DIFFICULTY) * Math.Pow(betterAccuracyPercentage, 24) * 2.83;

            // Bonus for many hitcircles - it's harder to keep good accuracy up for longer.
            accuracyValue *= Math.Min(1.15, Math.Pow(amountHitObjectsWithAccuracy / 1000.0, 0.3));

            // Increasing the accuracy value by object count for Blinds isn't ideal, so the minimum buff is given.
            if (score.Mods.Any(m => m is OsuModBlinds))
                accuracyValue *= 1.14;
            else if (score.Mods.Any(m => m is OsuModHidden || m is OsuModTraceable))
                accuracyValue *= 1.08;

            if (score.Mods.Any(m => m is OsuModFlashlight))
                accuracyValue *= 1.02;

            return accuracyValue;
        }

        private double computeFlashlightValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (!score.Mods.Any(h => h is OsuModFlashlight))
                return 0.0;

            double flashlightValue = Flashlight.DifficultyToPerformance(attributes.FlashlightDifficulty);

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (effectiveMissCount > 0)
                flashlightValue *= 0.97 * Math.Pow(1 - Math.Pow(effectiveMissCount / totalHits, 0.775), Math.Pow(effectiveMissCount, .875));

            flashlightValue *= getComboScalingFactor(attributes);

            // Scale the flashlight value with accuracy _slightly_.
            flashlightValue *= 0.5 + adjustedAccuracy / 2.0;

            return flashlightValue;
        }

        private double calculateComboBasedEstimatedMissCount(OsuDifficultyAttributes attributes)
        {
            if (attributes.SliderCount <= 0)
                return countMiss;

            double missCount = countMiss;

            if (usingClassicSliderAccuracy)
            {
                // Consider that full combo is maximum combo minus dropped slider tails since they don't contribute to combo but also don't break it
                // In classic scores we can't know the amount of dropped sliders so we estimate to 10% of all sliders on the map
                double fullComboThreshold = attributes.MaxCombo - 0.1 * attributes.SliderCount;

                if (scoreMaxCombo < fullComboThreshold)
                    missCount = fullComboThreshold / Math.Max(1.0, scoreMaxCombo);

                // In classic scores there can't be more misses than a sum of all non-perfect judgements
                missCount = Math.Min(missCount, totalImperfectHits);
            }
            else
            {
                double fullComboThreshold = attributes.MaxCombo - countSliderEndsDropped;

                if (scoreMaxCombo < fullComboThreshold)
                    missCount = fullComboThreshold / Math.Max(1.0, scoreMaxCombo);

                // Combine regular misses with tick misses since tick misses break combo as well
                missCount = Math.Min(missCount, countSliderTickMiss + countMiss);
            }

            return missCount;
        }

        private double calculateEstimatedSliderBreaks(double topWeightedSliderFactor, OsuDifficultyAttributes attributes)
        {
            if (!usingClassicSliderAccuracy || countOk == 0)
                return 0;

            double missedComboPercent = 1.0 - (double)scoreMaxCombo / attributes.MaxCombo;
            double estimatedSliderBreaks = Math.Min(countOk, effectiveMissCount * topWeightedSliderFactor);

            // scores with more oks are more likely to have slider breaks
            double okAdjustment = ((countOk - estimatedSliderBreaks) + 0.5) / countOk;

            // There is a low probability of extra slider breaks on effective miss counts close to 1, as score based calculations are good at indicating if only a single break occurred.
            estimatedSliderBreaks *= DifficultyCalculationUtils.Smoothstep(effectiveMissCount, 1, 2);

            return estimatedSliderBreaks * okAdjustment * DifficultyCalculationUtils.Logistic(missedComboPercent, 0.33, 15);
        }

        private (double great, double ok, double meh) calculateAdjustedAccuracy(double od, OsuDifficultyAttributes attributes)
        {
            int accuracyObjectCount = usingClassicSliderAccuracy ? attributes.HitCircleCount : attributes.HitCircleCount + attributes.SliderCount;

            (double great, double ok, double meh, double miss) = getRelevantCounts(accuracyObjectCount);

            // redistribute mehs into accuracy
            double inaccuracies = 1.5 * accuracyObjectCount * (1 - (great + ok / 3 + meh / 6 + miss) / accuracyObjectCount);

            // Add 1 to inaccuracies as prior
            double p = (accuracyObjectCount - (inaccuracies + 1)) / accuracyObjectCount;

            // assume that every 50 notes, the player hits one outlier with 4x the deviation.
            const double outlier_rate = 1 / 50.0;
            const double outlier_multiplier = 2.5;

            double contaminatedNormalCdf(double hitWindow, double d) => DifficultyCalculationUtils.Erf(hitWindow / (Math.Sqrt(2) * d)) * (1 - outlier_rate)
                                                                        + DifficultyCalculationUtils.Erf(hitWindow / (Math.Sqrt(2) * d * outlier_multiplier)) * outlier_rate;

            // Compute deviation assuming inaccuracies are normally distributed.
            double? deviation = p > 0
                ? RootFinding.FindRootExpand(d => contaminatedNormalCdf(greatHitWindow, d) - p, 0, 20)
                : null;

            if (deviation is null)
                return (0, 0, 0);

            HitWindows hitWindows = new OsuHitWindows();
            hitWindows.SetDifficulty(od);

            double newGreatHitWindow = hitWindows.WindowFor(HitResult.Great);
            double newOkHitWindow = hitWindows.WindowFor(HitResult.Ok);
            double newMehHitWindow = hitWindows.WindowFor(HitResult.Meh);

            double c300 = accuracyObjectCount * contaminatedNormalCdf(newGreatHitWindow, deviation.Value);
            double c100 = accuracyObjectCount * contaminatedNormalCdf(newOkHitWindow, deviation.Value) - c300;
            double c50 = accuracyObjectCount - c300 - c100;

            // We added 1 to the count when calculating accuracy. remove it using a hack
            c300 = Math.Min(c300 * (accuracyObjectCount + 1) / accuracyObjectCount, accuracyObjectCount);
            c100 = Math.Max(c100 * (accuracyObjectCount + 1) / accuracyObjectCount - 1, 0);
            c50 = c50 * (accuracyObjectCount + 1) / accuracyObjectCount;

            if (usingClassicSliderAccuracy)
            {
                c300 += attributes.SliderCount * DifficultyCalculationUtils.Erf(newMehHitWindow / (Math.Sqrt(2) * deviation.Value));
                c100 += attributes.SliderCount - attributes.SliderCount * DifficultyCalculationUtils.Erf(newMehHitWindow / (Math.Sqrt(2) * deviation.Value));
            }

            c300 += attributes.SpinnerCount;

            double successfulHitsProportion = totalSuccessfulHits / (double)totalHits;
            double mapCompletionProportion = totalHits / (double)(attributes.HitCircleCount + attributes.SliderCount + attributes.SpinnerCount);

            c300 *= successfulHitsProportion * mapCompletionProportion;
            c100 *= successfulHitsProportion * mapCompletionProportion;
            c50 *= successfulHitsProportion * mapCompletionProportion;

            return (c300, c100, c50);
        }

        /// <summary>
        /// Estimates player's deviation on speed notes using <see cref="calculateDeviation"/>, assuming worst-case.
        /// Treats all speed notes as hit circles.
        /// </summary>
        private double? calculateSpeedDeviation(OsuDifficultyAttributes attributes)
        {
            if (totalSuccessfulHits == 0)
                return null;

            // Calculate accuracy assuming the worst case scenario
            double speedNoteCount = attributes.SpeedNoteCount + 0.1 * (totalHits - attributes.SpeedNoteCount);

            (double relevantCountGreat, double relevantCountOk, double relevantCountMeh, _) = getRelevantCounts(speedNoteCount);

            if (relevantCountGreat + relevantCountOk + relevantCountMeh <= 0)
                return null;

            // 99th percentile
            double z_score = 2.32634787404;

            // The sample proportion of successful hits.
            double n = Math.Max(1, relevantCountGreat + relevantCountOk);
            double p = relevantCountGreat / n;

            // We can be 99% confident that the population proportion is at least this value.
            double pLowerBound = Math.Min(p, (n * p + z_score * z_score / 2) / (n + z_score * z_score) - z_score / (n + z_score * z_score) * Math.Sqrt(n * p * (1 - p) + z_score * z_score / 4));

            double deviation;

            if (pLowerBound > 0)
            {
                // Compute deviation assuming greats and oks are normally distributed.
                deviation = greatHitWindow / (Math.Sqrt(2) * DifficultyCalculationUtils.ErfInv(pLowerBound));

                // Subtract the deviation provided by tails that land outside the ok hit window from the deviation computed above.
                // This is equivalent to calculating the deviation of a normal distribution truncated at +-okHitWindow.
                double okHitWindowTailAmount = Math.Sqrt(2 / Math.PI) * okHitWindow * Math.Exp(-0.5 * Math.Pow(okHitWindow / deviation, 2))
                                               / (deviation * DifficultyCalculationUtils.Erf(okHitWindow / (Math.Sqrt(2) * deviation)));

                deviation *= Math.Sqrt(1 - okHitWindowTailAmount);
            }
            else
            {
                // A tested limit value for the case of a score only containing oks.
                deviation = okHitWindow / Math.Sqrt(3);
            }

            // Compute and add the variance for mehs, assuming that they are uniformly distributed.
            double mehVariance = (mehHitWindow * mehHitWindow + okHitWindow * mehHitWindow + okHitWindow * okHitWindow) / 3;

            deviation = Math.Sqrt(((relevantCountGreat + relevantCountOk) * Math.Pow(deviation, 2) + relevantCountMeh * mehVariance) / (relevantCountGreat + relevantCountOk + relevantCountMeh));

            return deviation;
        }

        /// <summary>
        /// Computes the relevant hit result counts for a given object count under worst-case assumption.
        /// </summary>
        private (double great, double ok, double meh, double miss) getRelevantCounts(double objectCount)
        {
            double miss = Math.Min(countMiss, objectCount);
            double meh = Math.Min(countMeh, objectCount - miss);
            double ok = Math.Min(countOk, objectCount - miss - meh);
            double great = Math.Max(0, objectCount - miss - meh - ok);
            return (great, ok, meh, miss);
        }

        // Calculates multiplier for speed to account for improper tapping based on the deviation and speed difficulty
        // https://www.desmos.com/calculator/dmogdhzofn
        private double calculateSpeedHighDeviationNerf(OsuDifficultyAttributes attributes)
        {
            if (speedDeviation == null)
                return 0;

            double speedValue = OsuStrainSkill.DifficultyToPerformance(attributes.SpeedDifficulty);

            // Decides a point where the PP value achieved compared to the speed deviation is assumed to be tapped improperly. Any PP above this point is considered "excess" speed difficulty.
            // This is used to cause PP above the cutoff to scale logarithmically towards the original speed value thus nerfing the value.
            double excessSpeedDifficultyCutoff = 100 + 220 * Math.Pow(22 / speedDeviation.Value, 6.5);

            if (speedValue <= excessSpeedDifficultyCutoff)
                return 1.0;

            const double scale = 50;
            double adjustedSpeedValue = scale * (Math.Log((speedValue - excessSpeedDifficultyCutoff) / scale + 1) + excessSpeedDifficultyCutoff / scale);

            // 220 UR and less are considered tapped correctly to ensure that normal scores will be punished as little as possible
            double lerp = 1 - DifficultyCalculationUtils.ReverseLerp(speedDeviation.Value, 22.0, 27.0);
            adjustedSpeedValue = double.Lerp(adjustedSpeedValue, speedValue, lerp);

            return adjustedSpeedValue / speedValue;
        }

        // Miss penalty assumes that a player will miss on the hardest parts of a map,
        // so we use the amount of relatively difficult sections to adjust miss penalty
        // to make it more punishing on maps with lower amount of hard sections.
        private double calculateMissPenalty(double missCount, double difficultStrainCount) => 0.96 / ((missCount / (4 * Math.Pow(Math.Log(difficultStrainCount), 0.94))) + 1);
        private double getComboScalingFactor(OsuDifficultyAttributes attributes) => attributes.MaxCombo <= 0 ? 1.0 : Math.Min(Math.Pow(scoreMaxCombo, 0.8) / Math.Pow(attributes.MaxCombo, 0.8), 1.0);

        private int totalHits => countGreat + countOk + countMeh + countMiss;
        private int totalSuccessfulHits => countGreat + countOk + countMeh;
        private int totalImperfectHits => countOk + countMeh + countMiss;
    }
}
