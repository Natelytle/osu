// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Utils
{
    public static class OsuStrainUtils
    {
        public static double GetStrainValueOf(IReadOnlyCollection<DifficultyPoint> difficultyPoints, double currentTime, double strainDecayWeight)
        {
            double accumulatedStrain = 0;

            foreach (DifficultyPoint point in difficultyPoints.Reverse())
            {
                accumulatedStrain += point.Difficulty * strainDecay(currentTime - point.Time) * (1 - strainDecay(point.DeltaTime));

                // Break once each point contributes nothing
                if (strainDecay(currentTime - point.Time) <= 0.0001)
                    break;
            }

            return accumulatedStrain;

            double strainDecay(double ms) => Math.Pow(strainDecayWeight, ms / 1000);
        }

        public static double CountTopWeightedSliders(IReadOnlyCollection<double> sliderStrains, double difficultyValue)
        {
            if (sliderStrains.Count == 0)
                return 0;

            double consistentTopStrain = difficultyValue / 10; // What would the top strain be if all strain values were identical

            if (consistentTopStrain == 0)
                return 0;

            // Use a weighted sum of all strains. Constants are arbitrary and give nice values
            return sliderStrains.Sum(s => DifficultyCalculationUtils.Logistic(s / consistentTopStrain, 0.88, 10, 1.1));
        }
    }
}
