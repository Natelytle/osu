// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class CrossColumnPressure
    {
        // weights for each column (plus the extra one)
        private static readonly double[][] cross_matrix =
        [
            [-1],
            [0.075, 0.075],
            [0.125, 0.05, 0.125],
            [0.125, 0.125, 0.125, 0.125],
            [0.175, 0.25, 0.05, 0.25, 0.175],
            [0.175, 0.25, 0.175, 0.175, 0.25, 0.175],
            [0.225, 0.35, 0.25, 0.05, 0.25, 0.35, 0.225],
            [0.225, 0.35, 0.25, 0.225, 0.225, 0.25, 0.35, 0.225],
            [0.275, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.275],
            [0.275, 0.45, 0.35, 0.25, 0.275, 0.275, 0.25, 0.35, 0.45, 0.275],
            [0.325, 0.55, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.55, 0.325]
        ];

        public static double[] EvaluateCrossColumnPressure(List<ManiaDifficultyHitObject>[] perColumnNoteList, int totalColumns, double hitLeniency, double[] baseCorners, double[] allCorners)
        {
            double[] crossColumnPressure = new double[baseCorners.Length];
            double[] prevFastCross = new double[baseCorners.Length];

            double[] columnWeights = cross_matrix[totalColumns];

            for (int col = 0; col < totalColumns + 1; col++)
            {
                int cornerPointer = 0;

                IEnumerable<ManiaDifficultyHitObject> pairedNotesList;

                if (col == 0)
                    pairedNotesList = perColumnNoteList[col];
                else if (col == totalColumns)
                    pairedNotesList = perColumnNoteList[col - 1];
                else
                    pairedNotesList = mergeSorted(perColumnNoteList[col], perColumnNoteList[col - 1]);
                ManiaDifficultyHitObject? prev = null;

                double crossVal = columnWeights[col];

                foreach (ManiaDifficultyHitObject note in pairedNotesList)
                {
                    if (prev is not null && prev.StartTime < note.StartTime)
                    {
                        double currStart = note.StartTime;
                        double prevStart = prev.StartTime;

                        double delta = 0.001 * (currStart - prevStart);
                        double val = 0.16 * Math.Pow(Math.Max(hitLeniency, delta), -2);

                        if (col == 0 || col == totalColumns)
                        {
                            val *= 1 - crossVal;
                        }
                        else
                        {
                            // We provide a nerf to the value if either the adjacent and current columns don't include any notes within the past 150 milliseconds.
                            bool adjacentKeyUsed = currStart - note.CurrentHitObjects[col - 1]?.EndTime < 150 || prevStart - prev.CurrentHitObjects[col - 1]?.EndTime < 150;
                            bool currentKeyUsed = currStart - note.CurrentHitObjects[col]?.EndTime < 150 || prevStart - prev.CurrentHitObjects[col]?.EndTime < 150;

                            if (!adjacentKeyUsed || !currentKeyUsed)
                                val *= 1 - crossVal;
                        }

                        double fastCross = Math.Max(0, 0.4 * Math.Pow(Math.Max(Math.Max(delta, 0.06), 0.75 * hitLeniency), -2) - 80) * crossVal;

                        // find the first corner at the start time of the previous note
                        while (cornerPointer < baseCorners.Length && baseCorners[cornerPointer] < prev.StartTime) cornerPointer++;
                        int firstCornerIndex = cornerPointer;

                        // find the first corner at the start time of the previous note
                        while (cornerPointer < baseCorners.Length && baseCorners[cornerPointer] < note.StartTime) cornerPointer++;
                        int lastCornerIndex = cornerPointer;

                        for (int i = firstCornerIndex; i < lastCornerIndex; i++)
                        {
                            crossColumnPressure[i] += val * crossVal;

                            // fastCross only applies to n columns, since we're iterating over n+1 we ignore the first column.
                            if (col != 0 && prevFastCross[i] > 0)
                                crossColumnPressure[i] += Math.Sqrt(prevFastCross[i] * fastCross);

                            prevFastCross[i] = fastCross;
                        }
                    }

                    prev = note;
                }
            }

            // Smooths it out
            crossColumnPressure = CornerUtils.SmoothCornersWithinWindow(baseCorners, crossColumnPressure, 500, 0.001);

            // Fits it to all corners
            crossColumnPressure = CornerUtils.InterpolateValues(allCorners, baseCorners, crossColumnPressure);

            return crossColumnPressure;
        }

        private static IEnumerable<ManiaDifficultyHitObject> mergeSorted(List<ManiaDifficultyHitObject> a, List<ManiaDifficultyHitObject> b)
        {
            int i = 0, j = 0;

            while (i < a.Count && j < b.Count)
            {
                if (a[i].StartTime <= b[j].StartTime)
                    yield return a[i++];
                else
                    yield return b[j++];
            }

            while (i < a.Count) yield return a[i++];
            while (j < b.Count) yield return b[j++];
        }
    }
}
