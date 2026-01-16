// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class StrainEvaluator
    {
        private const double combined_multiplier = 0.27;
        private const double combined_exponent = 2.0 / 3.0;

        // Combines all of the values from the evaluators into one resultant value.
        public static AccuracyDifficulties EvaluateDifficultiesOf(ManiaDifficultyHitObject current)
        {
            AccuracyDifficulties jackDifficulties = JackEvaluator.EvaluateDifficultiesOf(current);
            AccuracyDifficulties densityDifficulties = DensityEvaluator.EvaluateDifficultiesOf(current);
            AccuracyDifficulties streamDifficulties = StreamEvaluator.EvaluateDifficultiesOf(current, jackDifficulties, densityDifficulties);

            // We scale jack and density together, as their difficulties overlap somewhat.
            AccuracyDifficulties combinedDifficulties = AccuracyDifficulties.Pow(jackDifficulties + densityDifficulties, combined_exponent) * combined_multiplier;

            AccuracyDifficulties strainDifficulties = streamDifficulties + combinedDifficulties;
            AccuracyDifficulties baseDifficulties = new AccuracyDifficulties(1, AccuracyDifficulties.Lenience.Lenient);

            return AccuracyDifficulties.Norm(2, strainDifficulties, baseDifficulties);
        }
    }
}
