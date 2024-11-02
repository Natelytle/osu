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
        /// <param name="totalBins">The number of bins. Must be a perfect square.</param>
        public static BinLongNote[] CreateBins(List<(double head, double tail)> difficulties, int totalBins)
        {
            if (difficulties.Count == 0)
                return Array.Empty<BinLongNote>();

            List<double> headDifficulties = difficulties.ConvertAll(d => d.head).ToList();
            List<double> tailDifficulties = difficulties.ConvertAll(d => d.tail).ToList();

            double maxHeadDifficulty = headDifficulties.Max();
            double maxTailDifficulty = tailDifficulties.Max();

            var bins = new BinLongNote[totalBins];

            int dimensionLength = (int)Math.Sqrt(totalBins);

            for (int i = 0; i < dimensionLength; i++)
            {
                // If there are enough notes for each note to have a separate time value, we can keep the time bins
                // closer to the actual notes by using percentile time values instead of evenly spacing them.
                // int timePercentileIndex = Math.Min(i * times.Count / (timeDimensionLength - 1), times.Count - 1);
                // double time = times.Count >= timeDimensionLength ? times[timePercentileIndex] : endTime * (i + 1) / timeDimensionLength;

                double headDifficulty = maxHeadDifficulty * i / dimensionLength;

                for (int j = 0; j < dimensionLength; j++)
                {
                    bins[dimensionLength * i + j].HeadDifficulty = headDifficulty;
                    bins[dimensionLength * i + j].TailDifficulty = maxTailDifficulty * j / dimensionLength;
                }
            }

            for (int i = 0; i < difficulties.Count; i++)
            {
                double headDifficultyBinIndex = maxHeadDifficulty > 0 ? dimensionLength * (headDifficulties[i] / maxHeadDifficulty) : 0;
                double tailDifficultyBinIndex = maxTailDifficulty > 0 ? dimensionLength * (tailDifficulties[i] / maxTailDifficulty) : 0;

                // Cap the upper bounds to dimension length - 1. If they're higher, then dt/tt will be 0 anyway, so it doesn't matter.
                int headLowerBound = Math.Min(fastFloor(headDifficultyBinIndex), dimensionLength - 2);
                int headUpperBound = Math.Min(headLowerBound + 1, dimensionLength - 1);
                double ht = headDifficultyBinIndex - headLowerBound;

                int tailLowerBound = Math.Min(fastFloor(tailDifficultyBinIndex), dimensionLength - 2);
                int tailUpperBound = Math.Min(tailLowerBound + 1, dimensionLength - 1);
                double tt = tailDifficultyBinIndex - tailLowerBound;

                if (tailLowerBound >= 0 && headLowerBound >= 0)
                {
                    bins[dimensionLength * headLowerBound + tailLowerBound].Count += (1 - ht) * (1 - tt);
                }

                if (tailLowerBound >= 0)
                {
                    bins[dimensionLength * headUpperBound + tailLowerBound].Count += ht * (1 - tt);
                }

                if (headLowerBound >= 0)
                {
                    bins[dimensionLength * headLowerBound + tailUpperBound].Count += (1 - ht) * tt;
                }

                bins[dimensionLength * headUpperBound + tailUpperBound].Count += ht * tt;
            }

            return bins;
        }

        // Faster implementation of the floor function to speed up binning times.
        private static int fastFloor(double x) => x is >= 0 or -1 ? (int)x : (int)(x - 1);
    }
}
