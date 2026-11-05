// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class AccuracyPenalties
    {
        public static readonly double[] PENALTY_VALUES = [1, 0.97, 0.91, 0.83, 0.75, 0.64, 0.65, 0.53, 0.41, 0.28, 0.15, 0];
        public readonly double[] Accuracies = new double[PENALTY_VALUES.Length];

        public (double, double, double, double, double, double, double, double, double, double, double) Unpack() => (
            Accuracies[0],
            Accuracies[1],
            Accuracies[2],
            Accuracies[3],
            Accuracies[4],
            Accuracies[5],
            Accuracies[6],
            Accuracies[7],
            Accuracies[8],
            Accuracies[9],
            Accuracies[10]
        );

        public static AccuracyPenalties CreateFromAttribs((double, double, double, double, double, double, double, double, double, double, double) values)
        {
            return new AccuracyPenalties
            {
                Accuracies =
                {
                    [0] = values.Item1,
                    [1] = values.Item2,
                    [2] = values.Item3,
                    [3] = values.Item4,
                    [4] = values.Item5,
                    [5] = values.Item6,
                    [6] = values.Item7,
                    [7] = values.Item8,
                    [8] = values.Item9,
                    [9] = values.Item10,
                    [10] = values.Item11
                }
            };
        }

        public static double GetMultiplierAt(AccuracyPenalties penalties, double accuracy)
        {
            if (accuracy >= 1) return 1;
            if (accuracy <= penalties.Accuracies.Last()) return 0;

            int idx = 1;

            while (penalties.Accuracies[idx] > accuracy)
                idx++;

            double lo = PENALTY_VALUES[idx - 1];
            double hi = PENALTY_VALUES[idx];

            double amount = DiffUtils.ReverseLerp(accuracy, penalties.Accuracies[idx - 1], penalties.Accuracies[idx]);

            return Interpolation.Lerp(lo, hi, amount);
        }
    }
}
