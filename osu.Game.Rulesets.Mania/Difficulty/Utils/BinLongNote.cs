// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public struct BinLongNote
    {
        public double HeadDifficulty;
        public double TailDifficulty;

        public double Count;

        /// <summary>
        /// Create a 2-dimensional array of equally spaced bins. Count is linearly interpolated on each dimension into the nearest bins.
        /// For example, on one dimension if we have bins with values [1,2,3,4,5] and want to insert the value 3.2,
        /// we will add 0.8 total to the count of 3's on that dimension and 0.2 total to the count of 4's.
        /// </summary>
        /// <param name="difficulties">The list of long note difficulties.</param>
        /// <param name="dimensionLength">The length of one dimension of bins.</param>
        public static BinLongNote[] CreateBins(List<(double head, double tail)> difficulties, int dimensionLength)
        {
            if (difficulties.Count == 0)
                return Array.Empty<BinLongNote>();

            List<double> headDifficulties = difficulties.ConvertAll(d => d.head).ToList();
            List<double> tailDifficulties = difficulties.ConvertAll(d => d.tail).ToList();

            double maxHeadDifficulty = headDifficulties.Max();
            double maxTailDifficulty = tailDifficulties.Max();

            int totalBins = dimensionLength * dimensionLength;

            var bins = new BinLongNote[totalBins];

            for (int i = 0; i < dimensionLength; i++)
            {
                double headDifficulty = maxHeadDifficulty * i / (dimensionLength - 1);

                for (int j = 0; j < dimensionLength; j++)
                {
                    bins[dimensionLength * i + j].HeadDifficulty = headDifficulty;
                    bins[dimensionLength * i + j].TailDifficulty = maxTailDifficulty * j / (dimensionLength - 1);
                }
            }

            for (int i = 0; i < difficulties.Count; i++)
            {
                double headDifficultyBinIndex = maxHeadDifficulty > 0 ? dimensionLength * (headDifficulties[i] / maxHeadDifficulty) : 0;
                double tailDifficultyBinIndex = maxTailDifficulty > 0 ? dimensionLength * (tailDifficulties[i] / maxTailDifficulty) : 0;

                // Cap the upper bounds to dimension length - 1. If they're higher, then dt/tt will be 0 anyway, so it doesn't matter.
                int headLowerBound = Math.Min((int)headDifficultyBinIndex, dimensionLength - 1);
                int headUpperBound = Math.Min(headLowerBound + 1, dimensionLength - 1);
                double ht = headDifficultyBinIndex - headLowerBound;

                int tailLowerBound = Math.Min((int)tailDifficultyBinIndex, dimensionLength - 1);
                int tailUpperBound = tailLowerBound + 1;
                double tt = tailDifficultyBinIndex - tailLowerBound;

                bins[dimensionLength * headLowerBound + tailLowerBound].Count += (1 - ht) * (1 - tt);

                if (headUpperBound < dimensionLength)
                {
                    bins[dimensionLength * headUpperBound + tailLowerBound].Count += ht * (1 - tt);
                }

                if (tailUpperBound < dimensionLength)
                {
                    bins[dimensionLength * headLowerBound + tailUpperBound].Count += (1 - ht) * tt;
                }

                if (headUpperBound < dimensionLength && tailUpperBound < dimensionLength)
                {
                    bins[dimensionLength * headUpperBound + tailUpperBound].Count += ht * tt;
                }
            }

            return bins;
        }
    }
}
