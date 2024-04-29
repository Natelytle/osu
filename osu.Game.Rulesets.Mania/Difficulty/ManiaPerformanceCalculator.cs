// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;

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

            // Arbitrary initial value for scaling pp in order to standardize distributions across game modes.
            // The specific number has no intrinsic meaning and can be adjusted as needed.
            double multiplier = 8.0;

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
            double difficultyValue = Math.Pow(Math.Max(attributes.StarRating - 0.15, 0.05), 2.2); // Star rating to pp curve

            if (countPerfect != totalHits)
                difficultyValue *= Math.Pow(judgementPenalty(attributes), 2.2); // Skill penalty, raised to the power of 2.2 since PP is skill^2.2.

            return difficultyValue;
        }

        private double totalHits => countPerfect + countOk + countGreat + countGood + countMeh + countMiss;

        private double judgementPenalty(ManiaDifficultyAttributes attributes)
        {
            if (totalHits == 0)
                return 0;

            double likelihoodOfJudgements(double penalty)
            {
                if (penalty < 0 || penalty > 1)
                    return double.PositiveInfinity;

                double p300 = quartic(penalty, attributes.Coefficients300, true) / totalHits;
                double p200 = quartic(penalty, attributes.Coefficients200, true) / totalHits;
                double p100 = quartic(penalty, attributes.Coefficients100, true) / totalHits;
                double p50 = quartic(penalty, attributes.Coefficients50, true) / totalHits;
                double p0 = quartic(penalty, attributes.Coefficients0, false) / totalHits;

                double p320 = 1 - p300 - p200 - p100 - p50 - p0;

                double likelihood = Math.Pow(p320, countPerfect / totalHits)
                                    * Math.Pow(p300, countGreat / totalHits)
                                    * Math.Pow(p200, countGood / totalHits)
                                    * Math.Pow(p100, countOk / totalHits)
                                    * Math.Pow(p50, countMeh / totalHits)
                                    * Math.Pow(p0, countMiss / totalHits);

                return -likelihood;
            }

            var objective = new ValueObjectiveFunction(v => likelihoodOfJudgements(v[0]));

            double penalty = NelderMeadSimplex.Minimum(objective, 0.2, 1e-6, 1000);

            return 1 - penalty;
        }

        private double quartic(double x, (double, double, double) coefficients, bool endAtZero)
        {
            double a = coefficients.Item1;
            double b = coefficients.Item2;
            double c = coefficients.Item3;
            double d = endAtZero ? -(a + b + c) : Math.Log(totalHits + 1) - a - b - c;

            return Math.Clamp(Math.Exp(a * x * x * x * x + b * x * x * x + c * x * x + d * x) - 1, 1e-5, Math.Log(totalHits + 1));
        }
    }
}
