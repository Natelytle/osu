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

            List<(double head, double tail)> ordered = difficulties.OrderBy(d => d.head).ToList();

            int totalBins = dimensionLength * dimensionLength;

            List<(double head, double tail)>[] difficultiesPerBin = new List<(double head, double tail)>[totalBins];

            var bins = new BinLongNote[totalBins];

            int notePreviousIndex = 0;

            for (int iHead = 0; iHead < dimensionLength; iHead++)
            {
                int noteIndex = (int)Math.Floor(ordered.Count * Math.Max(iHead, 1) / (dimensionLength - 1.0));

                List<(double head, double tail)> difficultiesInRow = ordered.Skip(notePreviousIndex).Take(noteIndex - notePreviousIndex).ToList();

                difficultiesInRow = difficultiesInRow.OrderBy(d => d.tail).ToList();

                int tailPreviousIndex = 0;

                for (int iTail = 0; iTail < dimensionLength; iTail++)
                {
                    if (iHead > 0)
                        bins[dimensionLength * iHead + iTail].HeadDifficulty = difficultiesInRow.Count > 0 ? difficultiesInRow.Max(d => d.tail) : bins[dimensionLength * (iHead - 1) + iTail].TailDifficulty;

                    if (iTail == 0) continue;

                    int tailIndex = (int)Math.Floor(difficultiesInRow.Count * iTail / (dimensionLength - 1.0));

                    List<(double head, double tail)> difficultiesInBin = difficultiesInRow.Skip(tailPreviousIndex).Take(tailIndex - tailPreviousIndex).ToList();

                    bins[dimensionLength * iHead + iTail].TailDifficulty = difficultiesInBin.Count > 0 ? difficultiesInBin.Max(d => d.tail) : bins[dimensionLength * iHead + (iTail - 1)].TailDifficulty;

                    difficultiesPerBin[dimensionLength * iHead + iTail] = difficultiesInBin;

                    tailPreviousIndex = tailIndex;
                }

                if (iHead == 0) continue;

                notePreviousIndex = noteIndex;
            }

            for (int iHead = 1; iHead < dimensionLength; iHead++)
            {
                double lowerHeadDifficulty = bins[dimensionLength * (iHead - 1)].HeadDifficulty;
                double upperHeadDifficulty = bins[dimensionLength * iHead].HeadDifficulty;

                for (int iTail = 1; iTail < dimensionLength; iTail++)
                {
                    double lowerTailDifficulty = bins[dimensionLength * iHead + (iTail - 1)].TailDifficulty;
                    double upperTailDifficulty = bins[dimensionLength * iHead + iTail].TailDifficulty;

                    foreach ((double head, double tail) d in difficultiesPerBin[dimensionLength * iHead + iTail])
                    {
                        double tHead = upperHeadDifficulty - lowerHeadDifficulty != 0 ? (d.head - lowerHeadDifficulty) / (upperHeadDifficulty - lowerHeadDifficulty) : 0;
                        double tTail = upperHeadDifficulty - lowerHeadDifficulty != 0 ? (d.tail - lowerTailDifficulty) / (upperTailDifficulty - lowerTailDifficulty) : 0;

                        bins[dimensionLength * (iHead - 1) + (iTail - 1)].Count += (1 - tHead) * (1 - tTail);
                        bins[dimensionLength * iHead + (iTail - 1)].Count += tHead * (1 - tTail);
                        bins[dimensionLength * (iHead - 1) + iTail].Count += (1 - tHead) * tTail;
                        bins[dimensionLength * iHead + iTail].Count += tHead * tTail;
                    }
                }
            }

            return bins;
        }
    }
}
