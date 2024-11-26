// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Osu.Difficulty.Utils
{
    public struct FcTimeBins
    {
        public double Difficulty;
        public double Time;

        public double Count;

        /// <summary>
        /// Create a (pseudo) 2 dimensional array of equally spaced bins. Count is linearly interpolated on each dimension into the nearest bins.
        /// For example, on one dimension if we have bins with values [1,2,3,4,5] and want to insert the value 3.2,
        /// we will add 0.8 total to the count of 3's on that dimension and 0.2 total to the count of 4's.
        /// </summary>
        public static FcTimeBins[] CreateBins(List<double> difficulties, List<double> times, int difficultyDimensionLength, int timeDimensionLength)
        {
            double maxDifficulty = difficulties.Max();
            double endTime = times.Max();

            var bins = new FcTimeBins[timeDimensionLength * difficultyDimensionLength];

            // Like times are grouped together, like difficulties are separate.
            for (int i = 0; i < timeDimensionLength; i++)
            {
                double time = endTime * i / (timeDimensionLength - 1);

                for (int j = 0; j < difficultyDimensionLength; j++)
                {
                    bins[difficultyDimensionLength * i + j].Time = time;

                    // We don't create a 0 difficulty bin because 0 difficulty notes don't contribute to star rating.
                    bins[difficultyDimensionLength * i + j].Difficulty = maxDifficulty * (j + 1) / difficultyDimensionLength;
                }
            }

            for (int i = 0; i < difficulties.Count; i++)
            {
                double timeBinIndex = endTime > 0 ? timeDimensionLength * (times[i] / endTime) : 0;
                double difficultyBinIndex = difficultyDimensionLength * (difficulties[i] / maxDifficulty) - 1;

                int timeLowerBound = Math.Min((int)timeBinIndex, timeDimensionLength - 1);
                int timeUpperBound = Math.Min(timeLowerBound + 1, timeDimensionLength - 1);
                double tt = timeBinIndex - timeLowerBound;

                int difficultyLowerBound = fastFloor(difficultyBinIndex);
                int difficultyUpperBound = Math.Min(difficultyLowerBound + 1, difficultyDimensionLength - 1);
                double dt = difficultyBinIndex - difficultyLowerBound;

                // Store the time and difficulty values into the nearest 4 buckets.
                // The lower bound of difficulty can be -1, corresponding to buckets with 0 difficulty or at 0 time.
                // We don't store those since they don't contribute to difficulty.
                if (difficultyLowerBound >= 0 && timeLowerBound >= 0)
                {
                    bins[difficultyDimensionLength * timeLowerBound + difficultyLowerBound].Count += (1 - tt) * (1 - dt);
                }

                if (difficultyLowerBound >= 0)
                {
                    bins[difficultyDimensionLength * timeUpperBound + difficultyLowerBound].Count += tt * (1 - dt);
                }

                if (timeLowerBound >= 0)
                {
                    bins[difficultyDimensionLength * timeLowerBound + difficultyUpperBound].Count += (1 - tt) * dt;
                }

                bins[difficultyDimensionLength * timeUpperBound + difficultyUpperBound].Count += tt * dt;
            }

            return bins;
        }

        // Faster implementation of the floor function to speed up binning times.
        private static int fastFloor(double x) => x is >= 0 or -1 ? (int)x : (int)(x - 1);
    }
}
