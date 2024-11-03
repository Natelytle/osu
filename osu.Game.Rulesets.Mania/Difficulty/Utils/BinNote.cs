// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public struct BinNote
    {
        public double Difficulty;

        public double Count;

        /// <summary>
        /// Create an array of equally spaced bins. Count is linearly interpolated into each bin.
        /// For example, if we have bins with values [1,2,3,4,5] and want to insert the value 3.2,
        /// we will add 0.8 to the count of 3's and 0.2 to the count of 4's
        /// </summary>
        public static BinNote[] CreateBins(List<double> difficulties, int totalBins)
        {
            if (difficulties.Count == 0)
                return Array.Empty<BinNote>();

            double maxDifficulty = difficulties.Max();

            var bins = new BinNote[totalBins];

            for (int i = 0; i < totalBins; i++)
            {
                bins[i].Difficulty = maxDifficulty * i / (totalBins - 1);
            }

            foreach (double d in difficulties)
            {
                double binIndex = maxDifficulty > 0 ? totalBins * (d / maxDifficulty) : 0;

                int lowerBound = Math.Min((int)binIndex, totalBins - 1);
                double t = binIndex - lowerBound;

                int upperBound = lowerBound + 1;

                bins[lowerBound].Count += 1 - t;

                // this can be == bin_count for the maximum difficulty object, in which case t will be 0 anyway
                if (upperBound < totalBins)
                {
                    bins[upperBound].Count += t;
                }
            }

            return bins;
        }
    }
}
