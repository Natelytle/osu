// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class DensityEvaluator
    {
        private const double pressing_multiplier = 0.7;

        private const double release_multiplier = 1.0;
        private const double release_note_count_numerator = 35.0;
        private const double release_note_count_offset = 8.0;

        private const double unevenness_multiplier_exponent = 2.0 / 3.0;

        private const double density_difficulty_exponent = 1.5;
        private const double density_difficulty_multiplier = 0.6;

        public static AccuracyDifficulties EvaluateDifficultiesOf(ManiaDifficultyHitObject current)
        {
            var data = current.DifficultyData;
            double pressingIntensity = pressing_multiplier * data.SampleFeatureAtTime(current.StartTime, data.PressingIntensity);
            double releaseFactor = release_multiplier * data.SampleFeatureAtTime(current.StartTime, data.ReleaseFactor);

            double activeKeyCount = data.SampleFeatureAtTime(current.StartTime, data.ActiveKeyCount);
            double localNoteCount = data.SampleFeatureAtTime(current.StartTime, data.LocalNoteCount);

            // Nerf our release factor when there's a low number of active columns, and divide it if the number of surround notes is high.
            releaseFactor *= DifficultyCalculationUtils.Smoothstep(activeKeyCount, 0, 4);
            releaseFactor *= release_note_count_numerator / (localNoteCount + release_note_count_offset);

            AccuracyDifficulties pressingDifficulties = new AccuracyDifficulties(pressingIntensity, AccuracyDifficulties.Lenience.Lenient);
            AccuracyDifficulties releaseDifficulties = new AccuracyDifficulties(releaseFactor, AccuracyDifficulties.Lenience.Harsh);

            double unevenness = data.SampleFeatureAtTime(current.StartTime, data.Unevenness);

            // Combine unevenness with pressing intensity and release difficulty to get our density difficulty for this note
            AccuracyDifficulties densityDifficulty = (pressingDifficulties + releaseDifficulties) * Math.Pow(unevenness, unevenness_multiplier_exponent);
            densityDifficulty = AccuracyDifficulties.Pow(densityDifficulty, density_difficulty_exponent) * density_difficulty_multiplier;

            return densityDifficulty;
        }
    }
}
