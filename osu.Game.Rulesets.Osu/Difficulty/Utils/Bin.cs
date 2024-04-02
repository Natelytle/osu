// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Osu.Difficulty.Utils
{
    public struct Bin
    {
        public double Difficulty;
        public double Time;

        public double Count;

        /// <summary>
        /// Create a 2 dimensional array of equally spaced bins. Count is linearly interpolated into the nearest bins using euclidean distance.
        /// For example, on one dimension if we have bins with values [1,2,3,4,5] and want to insert the value 3.2,
        /// we will add 0.8 total to the count of 3's on that dimension and 0.2 total to the count of 4's.
        /// </summary>
        public static Bin[,] CreateBins(List<double> difficulties, List<double> times, int binDimensionLength)
        {
            double maxDifficulty = difficulties.Max();
            double endTime = times.Last();

            var bins = new Bin[binDimensionLength, binDimensionLength];

            for (int i = 0; i < binDimensionLength; i++)
            {
                for (int j = 0; j < binDimensionLength; j++)
                {
                    bins[i, j].Difficulty = maxDifficulty * (i + 1) / binDimensionLength;
                    bins[i, j].Time = endTime * (j + 1) / binDimensionLength;
                }
            }

            // These should always be the same, but just in case.
            int minimumCount = Math.Min(difficulties.Count, times.Count);

            for (int i = 0; i < minimumCount; i++)
            {
                double difficultyBinIndex = binDimensionLength * (difficulties[i] / maxDifficulty) - 1;
                double timeBinIndex = binDimensionLength * (times[i] / endTime) - 1;

                // Cap the upper bounds to the dimension length. If they're higher, then dt/tt will be 0 anyway, so it doesn't matter.
                int difficultyLowerBound = (int)Math.Floor(difficultyBinIndex);
                int difficultyUpperBound = Math.Min(difficultyLowerBound + 1, binDimensionLength - 1);
                double dt = difficultyBinIndex - difficultyLowerBound;

                int timeLowerBound = (int)Math.Floor(timeBinIndex);
                int timeUpperBound = Math.Min(timeLowerBound + 1, binDimensionLength - 1);
                double tt = timeBinIndex - timeLowerBound;

                // Store the time and difficulty values into the nearest 4 buckets.
                // The lower bounds can be -1, corresponding to buckets with 0 difficulty or at 0 time.
                // We don't store those since they don't contribute to difficulty.
                if (difficultyLowerBound >= 0 && timeLowerBound >= 0)
                {
                    bins[difficultyLowerBound, timeLowerBound].Count += distance(1 - dt, 1 - tt);
                }

                if (difficultyLowerBound >= 0)
                {
                    bins[difficultyLowerBound, timeUpperBound].Count += distance(1 - dt, tt);
                }

                if (timeLowerBound >= 0)
                {
                    bins[difficultyUpperBound, timeLowerBound].Count += distance(dt, 1 - tt);
                }

                bins[difficultyUpperBound, timeUpperBound].Count += distance(dt, tt);
            }

            return bins;

            double distance(double d1, double d2) => Math.Sqrt(d1 * d1 + d2 * d2);
        }
    }
}
