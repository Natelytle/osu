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

            List<double> ordered = difficulties.OrderBy(d => d).ToList();

            var bins = new BinNote[totalBins];

            List<double>[] difficultiesPerBin = new List<double>[totalBins];

            int previousIndex = 0;

            for (int i = 1; i < totalBins; i++)
            {
                int noteIndex = (int)Math.Floor(ordered.Count * (double)i / (totalBins - 1));

                List<double> difficultiesInBin = ordered.Skip(previousIndex).Take(noteIndex - previousIndex).ToList();

                bins[i].Difficulty = difficultiesInBin.Count > 0 ? difficultiesInBin.Max() : bins[i - 1].Difficulty;

                difficultiesPerBin[i] = difficultiesInBin;

                previousIndex = noteIndex;
            }

            for (int i = 1; i < totalBins; i++)
            {
                double lowerDifficulty = bins[i - 1].Difficulty;
                double upperDifficulty = bins[i].Difficulty;

                foreach (double d in difficultiesPerBin[i])
                {
                    double t = upperDifficulty - lowerDifficulty != 0 ? (d - lowerDifficulty) / (upperDifficulty - lowerDifficulty) : 0;

                    bins[i - 1].Count += 1 - t;
                    bins[i].Count += t;
                }
            }

            return bins;
        }
    }
}
