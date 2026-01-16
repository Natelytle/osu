// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class JackEvaluator
    {
        private const double jack_multiplier = 1.0;
        private const double jack_decrease_threshold = 50;
        private const double jack_decrease_multiplier = 0.85;
        private const double jack_unevenness_key_exponent_numerator = 3.0;
        private const double jack_difficulties_exponent = 1.5;
        private const double jack_difficulties_multiplier = 0.4;

        public static AccuracyDifficulties EvaluateDifficultiesOf(ManiaDifficultyHitObject current)
        {
            var data = current.DifficultyData;
            double jackDifficulty = jack_multiplier * data.SampleFeatureAtTime(current.StartTime, data.SameColumnPressure);

            // Rescale the high end to decrease a little slower
            jackDifficulty = Math.Min(jackDifficulty, jack_decrease_threshold + jack_decrease_multiplier * (jackDifficulty - jack_decrease_threshold));

            double unevenness = data.SampleFeatureAtTime(current.StartTime, data.Unevenness);
            double activeKeyCount = data.SampleFeatureAtTime(current.StartTime, data.ActiveKeyCount);

            // Rescale based on unevenness
            jackDifficulty *= jackUnevennessKeyAdjustment(unevenness, activeKeyCount);

            // Now create our accuracy difficulties. We use lenient scaling to have less of a difference between 95% and 100% accuracy.
            AccuracyDifficulties jackDifficulties = new AccuracyDifficulties(jackDifficulty, AccuracyDifficulties.Lenience.Lenient);

            jackDifficulties = AccuracyDifficulties.Pow(jackDifficulties, jack_difficulties_exponent) * jack_difficulties_multiplier;

            return jackDifficulties;
        }

        private static double jackUnevennessKeyAdjustment(double unevenness, double activeKeyCount)
        {
            if (unevenness <= 0.0 || activeKeyCount <= 0.0)
                return 1.0;

            return Math.Pow(unevenness, jack_unevenness_key_exponent_numerator / activeKeyCount);
        }
    }
}
