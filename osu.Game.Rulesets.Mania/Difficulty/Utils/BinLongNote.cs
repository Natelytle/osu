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
        public static List<BinLongNote> CreateBins(List<(double head, double tail)> difficulties, int dimensionLength)
        {
            var headDifficulties = difficulties.ConvertAll(d => d.head);
            var tailDifficulties = difficulties.ConvertAll(d => d.tail);

            double maxHeadDifficulty = headDifficulties.Max();
            double maxTailDifficulty = tailDifficulties.Max();

            var binsArray = new BinLongNote[dimensionLength * dimensionLength];

            for (int i = 0; i < dimensionLength; i++)
            {
                double headDifficulty = maxHeadDifficulty * i / (dimensionLength - 1);

                for (int j = 0; j < dimensionLength; j++)
                {
                    binsArray[dimensionLength * i + j].HeadDifficulty = headDifficulty;

                    // We don't create a 0 difficulty bin because 0 difficulty notes don't contribute to star rating.
                    binsArray[dimensionLength * i + j].TailDifficulty = maxTailDifficulty * j / (dimensionLength - 1);
                }
            }

            for (int i = 0; i < headDifficulties.Count; i++)
            {
                double headBinIndex = maxHeadDifficulty > 0 ? (dimensionLength - 1) * (headDifficulties[i] / maxHeadDifficulty) : 0;
                double tailBinIndex = maxTailDifficulty > 0 ? (dimensionLength - 1) * (tailDifficulties[i] / maxTailDifficulty) : 0;

                int headLowerBound = (int)headBinIndex;
                int headUpperBound = Math.Min(headLowerBound + 1, dimensionLength - 1);
                double ht = headBinIndex - headLowerBound;

                int tailLowerBound = (int)tailBinIndex;
                int tailUpperBound = Math.Min(tailLowerBound + 1, dimensionLength - 1);
                double tt = tailBinIndex - headLowerBound;

                binsArray[dimensionLength * headLowerBound + tailLowerBound].Count += (1 - ht) * (1 - tt);
                binsArray[dimensionLength * headUpperBound + tailLowerBound].Count += ht * (1 - tt);
                binsArray[dimensionLength * headLowerBound + tailUpperBound].Count += (1 - ht) * tt;
                binsArray[dimensionLength * headUpperBound + tailUpperBound].Count += ht * tt;
            }

            var binsList = binsArray.ToList();

            // For a slight performance improvement, we remove bins that don't contribute to difficulty.
            binsList.RemoveAll(bin => bin.Count == 0);

            return binsList;
        }
    }
}
