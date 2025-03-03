// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mania.Difficulty.Aggregation;
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

            double multiplier = 100;

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

            double difficultyValue = skill; // Math.Pow(skill, 2);

            // It's easy to spam retry short maps for a high accuracy value.
            // double shortMapNerf = 2 / (1 + Math.Exp(-totalHits / 20)) - 1;

            return difficultyValue;
        }

        private double accuracyAdjustedSkillLevel(ManiaDifficultyAttributes attributes)
        {
            double[] skillLevels = attributes.AccuracySkillLevels!;
            double[] accuracies = { 1.00, 0.998, 0.995, 0.99, 0.98, 0.97, 0.96, 0.95, 0.90, 0.80, 0.70 };

            if (scoreAccuracy == 1)
                return skillLevels[0];

            for (int i = 1; i < accuracies.Length; i++)
            {
                if (accuracies[i] > scoreAccuracy) continue;

                double highAccBound = accuracies[i - 1];
                double highAccSkill = skillLevels[i - 1];

                double lowAccBound = accuracies[i];
                double lowAccSkill = skillLevels[i];

                double penalty = Interpolation.Lerp(lowAccSkill, highAccSkill, (scoreAccuracy - lowAccBound) / (highAccBound - lowAccBound));

                return penalty;
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

            return (countPerfect * ManiaAccuracySkill.MAX_JUDGEMENT_WEIGHT + countGreat * 300 + countGood * 200 + countOk * 100 + countMeh * 50) / (totalHits * ManiaAccuracySkill.MAX_JUDGEMENT_WEIGHT);
        }
    }
}
