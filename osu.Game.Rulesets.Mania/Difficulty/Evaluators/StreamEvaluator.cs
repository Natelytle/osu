// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class StreamEvaluator
    {
        private const double multiplier = 2.7;
        private const double combined_exponent = 2.0 / 3.0;
        private const double unevenness_key_exponent_numerator = 3.0;

        public static AccuracyDifficulties EvaluateDifficultiesOf(ManiaDifficultyHitObject current, AccuracyDifficulties jackDifficulties, AccuracyDifficulties densityDifficulties)
        {
            var data = current.DifficultyData;

            double crossColumnDifficulty = data.SampleFeatureAtTime(current.StartTime, data.CrossColumnPressure);

            AccuracyDifficulties crossColumnDifficulties = new AccuracyDifficulties(crossColumnDifficulty, AccuracyDifficulties.Lenience.Harsh);

            double unevenness = data.SampleFeatureAtTime(current.StartTime, data.Unevenness);
            double activeKeyCount = data.SampleFeatureAtTime(current.StartTime, data.ActiveKeyCount);

            // We recreate combined difficulties separately from strainEvaluator to reduce coupling a little bit.
            AccuracyDifficulties combinedDifficulties = AccuracyDifficulties.Pow(jackDifficulties + densityDifficulties, combined_exponent);

            // Calculate the stream difficulty of the map.
            // Divided by the combined difficulties of jack and density, to keep the stream value down in hard sections.
            AccuracyDifficulties streamDifficulties = crossColumnDifficulties / (crossColumnDifficulties + combinedDifficulties + 1.0);
            streamDifficulties *= streamUnevennessKeyAdjustment(unevenness, activeKeyCount);
            streamDifficulties = AccuracyDifficulties.Pow(streamDifficulties, 1.5);
            streamDifficulties = AccuracyDifficulties.Pow(combinedDifficulties, 0.5) * streamDifficulties;

            return streamDifficulties * multiplier;
        }

        private static double streamUnevennessKeyAdjustment(double unevenness, double activeKeyCount)
        {
            if (unevenness <= 0.0 || activeKeyCount <= 0.0)
                return 1.0;

            return Math.Pow(unevenness, unevenness_key_exponent_numerator / activeKeyCount);
        }
    }
}
