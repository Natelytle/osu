// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mania.Difficulty.Utils.AccuracySimulation;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaPerformanceCalculator : PerformanceCalculator
    {
        private int countPerfect;
        private int countGreat;
        private int countGood;
        private int countOk;
        private int countMeh;
        private int countMiss;
        private double scoreAccuracy;

        public ManiaPerformanceCalculator()
            : base(new ManiaRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var maniaAttributes = (ManiaDifficultyAttributes)attributes;

            countPerfect = score.Statistics.GetValueOrDefault(HitResult.Perfect);
            countGreat = score.Statistics.GetValueOrDefault(HitResult.Great);
            countGood = score.Statistics.GetValueOrDefault(HitResult.Good);
            countOk = score.Statistics.GetValueOrDefault(HitResult.Ok);
            countMeh = score.Statistics.GetValueOrDefault(HitResult.Meh);
            countMiss = score.Statistics.GetValueOrDefault(HitResult.Miss);
            scoreAccuracy = calculateCustomAccuracy();

            double multiplier = 1.0;

            if (score.Mods.Any(m => m is ModNoFail))
                multiplier *= 0.75;
            if (score.Mods.Any(m => m is ModEasy))
                multiplier *= 0.5;

            double difficultyValue = computeDifficultyValue(maniaAttributes);
            double totalValue = difficultyValue * multiplier;

            return new ManiaPerformanceAttributes
            {
                Difficulty = difficultyValue,
                Total = totalValue
            };
        }

        private double computeDifficultyValue(ManiaDifficultyAttributes attributes)
        {
            double skill = accuracyAdjustedSkillLevel(attributes);

            double difficultyValue = 6 * Math.Pow(skill, 2.2); // Star rating to pp curve

            return difficultyValue;
        }

        private double accuracyAdjustedSkillLevel(ManiaDifficultyAttributes attributes)
        {
            double[] skillLevels = { 1.00, 0.95, 0.90, 0.85, 0.80, 0.75, 0.70, 0.65, 0.60, 0.55, 0.50, 0.45, 0.40, 0.35, 0.30, 0.25, 0.20, 0.15, 0.10, 0.05, 0 };
            double[] accuracies = attributes.AccuracyCurve!;

            if (scoreAccuracy == 1)
                return attributes.SSValue * skillLevels[0];

            for (int i = 1; i < accuracies.Length; i++)
            {
                if (accuracies[i] > scoreAccuracy) continue;

                double highAccBound = accuracies[i - 1];
                double highAccSkill = skillLevels[i - 1];

                double lowAccBound = accuracies[i];
                double lowAccSkill = skillLevels[i];

                double scoreSkill = attributes.SSValue * Interpolation.Lerp(lowAccSkill, highAccSkill, (scoreAccuracy - lowAccBound) / (highAccBound - lowAccBound));

                return scoreSkill;
            }

            return 0;
        }

        private double totalHits => countPerfect + countOk + countGreat + countGood + countMeh + countMiss;

        /// <summary>
        /// Accuracy used to weight judgements independently from the score's actual accuracy.
        /// </summary>
        private double calculateCustomAccuracy()
        {
            if (totalHits == 0)
                return 0;

            return (countPerfect * AccuracySimulator.MAX_JUDGEMENT_WEIGHT + countGreat * 300 + countGood * 200 + countOk * 100 + countMeh * 50) / (totalHits * AccuracySimulator.MAX_JUDGEMENT_WEIGHT);
        }
    }
}
