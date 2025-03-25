// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
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

        public static double[] EvaluateCrossColumnPressure(List<ManiaDifficultyHitObject>[] perColumnNoteList, int totalColumns, int mapLength, double hitLeniency)
        {
            double[] crossColumnPressure = new double[mapLength];
            double[] prevFastCross = new double[mapLength];

            for (int col = 0; col < totalColumns + 1; col++)
            {
                IEnumerable<ManiaDifficultyHitObject> pairedNotesList;

                if (col == 0)
                {
                    pairedNotesList = perColumnNoteList[col];
                }
                else if (col == totalColumns)
                {
                    pairedNotesList = perColumnNoteList[col - 1];
                }
                else
                {
                    // merges two columns together, forming pairs of notes adjacent in time
                    pairedNotesList = perColumnNoteList[col].Concat(perColumnNoteList[col - 1]);
                    pairedNotesList = pairedNotesList.OrderBy(obj => obj.StartTime);
                }

                ManiaDifficultyHitObject? prevPrev = null;
                ManiaDifficultyHitObject? prev = null;

                foreach (ManiaDifficultyHitObject note in pairedNotesList)
                {
                    if (prev is not null && prevPrev is not null && prev.StartTime < note.StartTime)
                    {
                        double delta = 0.001 * (prev.StartTime - prevPrev.StartTime);
                        double val = 0.16 * Math.Pow(Math.Max(hitLeniency, delta), -2);

                        double crossVal = cross_matrix[totalColumns][col];

                        if (col == 0 || col == totalColumns)
                            val *= 1 - crossVal;
                        else
                        {
                            // We provide a nerf to the value if either the adjacent and current columns don't include any notes within the past 150 milliseconds.
                            bool adjacentKeyUsed = prev.StartTime - prev.CurrentHitObjects[col - 1]?.EndTime < 150 || prevPrev.StartTime - prevPrev.CurrentHitObjects[col - 1]?.EndTime < 150;
                            bool currentKeyUsed = prev.StartTime - prev.CurrentHitObjects[col]?.EndTime < 150 || prevPrev.StartTime - prevPrev.CurrentHitObjects[col]?.EndTime < 150;

                            if (!(adjacentKeyUsed && currentKeyUsed))
                                val *= 1 - crossVal;
                        }

                        double fastCross = Math.Max(0, 0.4 * Math.Pow(Math.Max(Math.Max(delta, 0.06), -0.75 * hitLeniency), -2) - 80) * crossVal;

                        for (int t = (int)prev.StartTime; t < note.StartTime; t++)
                        {
                            crossColumnPressure[t] += val * cross_matrix[totalColumns][col];

                            // fastCross only applies to n columns, since we're iterating over n+1 we ignore the first column.
                            if (col != 0)
                                crossColumnPressure[t] += Math.Sqrt(prevFastCross[t] + fastCross);

                            prevFastCross[t] = fastCross;
                        }
                    }

                    prevPrev = prev;
                    prev = note;
                }
            }

            // smooths it out
            crossColumnPressure = ListUtils.ApplySymmetricMovingAverage(crossColumnPressure, 500);

            return crossColumnPressure;
        }
    }
}
