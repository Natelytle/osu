// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils.AccuracySimulation
{
    public struct Bin
    {
        public double Difficulty;
        public double Count;

        public static List<Bin> CreateBins(List<double> difficulties, int totalBins)
        {
            var bins = new List<Bin>(totalBins);

            if (difficulties.Count == 0)
                return bins;

            // Sort the difficulties
            var sorted = difficulties.OrderBy(d => d).ToList();
            int n = sorted.Count;

            // Divide into quantiles
            for (int i = 0; i < totalBins; i++)
            {
                int start = (int)((long)i * n / totalBins);
                int end = (int)((long)(i + 1) * n / totalBins);

                if (start >= end)
                {
                    bins.Add(new Bin { Difficulty = sorted[Math.Min(start, n - 1)], Count = 0 });
                    continue;
                }

                double count = end - start;
                double avgDifficulty = 0;

                for (int j = start; j < end; j++)
                    avgDifficulty += sorted[j];

                avgDifficulty /= count;

                bins.Add(new Bin
                {
                    Difficulty = avgDifficulty,
                    Count = count
                });
            }

            return bins;
        }
    }
}
