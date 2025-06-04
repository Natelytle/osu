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
        /// Create an array of spaced bins. Count is linearly interpolated into each bin.
        /// For example, if we have bins with values [1,2,3,4,5] and want to insert the value 3.2,
        /// we will add 0.8 to the count of 3's and 0.2 to the count of 4's
        /// </summary>
        public static List<BinNote> CreateBins(List<double> difficulties, int totalBins)
        {
            if (difficulties.Count == 0)
                return new List<BinNote>();

            double maxDifficulty = difficulties.Max();

            var binsArray = new BinNote[totalBins];

            for (int i = 0; i < totalBins; i++)
            {
                binsArray[i].Difficulty = maxDifficulty * i / (totalBins - 1);
            }

            foreach (double d in difficulties)
            {
                double binIndex = maxDifficulty > 0 ? (totalBins - 1) * (d / maxDifficulty) : 0;

                int lowerBound = (int)binIndex;
                int upperBound = Math.Min(lowerBound + 1, totalBins - 1);
                double t = binIndex - lowerBound;

                binsArray[lowerBound].Count += 1 - t;
                binsArray[upperBound].Count += t;
            }

            var binsList = binsArray.ToList();

            // For a slight performance improvement, we remove bins that don't contribute to difficulty.
            // binsList.RemoveAll(bin => bin.Count == 0);

            return binsList;
        }
    }
}
