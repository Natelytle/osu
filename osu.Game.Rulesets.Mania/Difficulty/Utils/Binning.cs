// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class Bin
    {
        public double BaseDifficulty;
        public double Count;
        public double[] MeanAccuracyMultipliers = new double[8];

        /// <summary>
        /// Incrementally folds a note's accuracy multipliers into this bin's running mean.
        /// </summary>
        public void Add(AccuracyDifficulties difficulty, double weight)
        {
            for (int i = 0; i < MeanAccuracyMultipliers.Length; i++)
                MeanAccuracyMultipliers[i] = (MeanAccuracyMultipliers[i] * Count + difficulty.Multipliers[i] * weight) / (Count + weight);

            Count += weight;
        }

        public double AccuracyAt(double skill)
        {
            if (skill >= BaseDifficulty * MeanAccuracyMultipliers[0])
                return AccuracyValueMultipliers.ACCURACY_VALUES[0];
            if (skill <= 0)
                return AccuracyValueMultipliers.ACCURACY_VALUES[^1];

            for (int i = 1; i < MeanAccuracyMultipliers.Length; i++)
            {
                if (BaseDifficulty * MeanAccuracyMultipliers[i] > skill)
                    continue;

                double upperSkillBound = BaseDifficulty * MeanAccuracyMultipliers[i - 1];
                double lowerSkillBound = BaseDifficulty * MeanAccuracyMultipliers[i];

                double upperAccuracyBound = AccuracyValueMultipliers.ACCURACY_VALUES[i - 1];
                double lowerAccuracyBound = AccuracyValueMultipliers.ACCURACY_VALUES[i];

                return Interpolation.Lerp(lowerAccuracyBound, upperAccuracyBound, (skill - lowerSkillBound) / (upperSkillBound - lowerSkillBound));
            }

            return 0;
        }
    }

    /// <summary>
    /// Maintains a sparse, geometrically-spaced (log-scale) histogram of <see cref="AccuracyDifficulties"/>
    /// that notes can be folded into one at a time, without needing to know the eventual maximum
    /// difficulty up front and without ever needing to be rebuilt from scratch.
    /// </summary>
    public class BinnedDifficulties
    {
        // Ratio between consecutive bin edges. Smaller = finer resolution but more bins.
        // 1.05 => ~5% relative width per bin.
        private const double bin_ratio = 1.05;
        private static readonly double log_ratio = Math.Log(bin_ratio);

        private readonly Dictionary<int, Bin> bins = new Dictionary<int, Bin>();

        // Notes with BaseDifficulty <= 0 can't be placed on a log scale; they behave as
        // "trivially easy" in the unbinned AccuracyAt (skill >= 0 immediately hits the
        // top accuracy bracket), so we track them separately to preserve that behaviour.
        private readonly Bin zeroBin = new Bin { BaseDifficulty = 0 };

        public Bin[] Bins => bins.Values.Append(zeroBin).ToArray();

        public void Add(AccuracyDifficulties difficulty)
        {
            if (difficulty.BaseDifficulty <= 0)
            {
                zeroBin.Add(difficulty, 1);
                return;
            }

            int index = (int)Math.Floor(Math.Log(difficulty.BaseDifficulty) / log_ratio);

            double lowerEdge = Math.Pow(bin_ratio, index);
            double upperEdge = Math.Pow(bin_ratio, index + 1);

            double upperProportion = DiffUtils.ReverseLerp(difficulty.BaseDifficulty, lowerEdge, upperEdge);
            double lowerProportion = 1 - upperProportion;

            if (lowerProportion > 0)
                getOrCreate(index, lowerEdge).Add(difficulty, lowerProportion);
            if (upperProportion > 0)
                getOrCreate(index + 1, upperEdge).Add(difficulty, upperProportion);
        }

        private Bin getOrCreate(int index, double edgeDifficulty)
        {
            if (!bins.TryGetValue(index, out var bin))
                bins[index] = bin = new Bin { BaseDifficulty = edgeDifficulty };

            return bin;
        }
    }
}
