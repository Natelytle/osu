// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class SameColumnPressure
    {
        public static double[] EvaluateSameColumnPressure(List<ManiaDifficultyHitObject>[] perColumnNoteList, int totalColumns, double hitLeniency, double[] baseCorners, double[] allCorners)
        {
            double[] sameColumnPressure = new double[baseCorners.Length];
            double[] perColumnPressure = new double[baseCorners.Length];
            double[] perColumnDeltaTimes = new double[baseCorners.Length];
            double[] sumWeights = new double[baseCorners.Length];

            for (int col = 0; col < totalColumns; col++)
            {
                int cornerPointer = 0;

                IEnumerable<ManiaDifficultyHitObject> columnNotes = perColumnNoteList[col];

                Array.Clear(perColumnPressure);
                Array.Clear(perColumnDeltaTimes);

                ManiaDifficultyHitObject? prev = null;

                foreach (ManiaDifficultyHitObject note in columnNotes)
                {
                    if (prev is not null && prev.StartTime < note.StartTime)
                    {
                        double delta = 0.001 * (note.StartTime - prev.StartTime);
                        double val = (1.0 / delta) * (1.0 / (delta + 0.11 * Math.Pow(hitLeniency, 1.0 / 4.0)));
                        val *= jackNerfer(delta);

                        // find the first corner at the start time of the previous note
                        while (cornerPointer < baseCorners.Length && baseCorners[cornerPointer] < prev.StartTime) cornerPointer++;
                        int firstCornerIndex = cornerPointer;

                        // find the first corner at the start time of the previous note
                        while (cornerPointer < baseCorners.Length && baseCorners[cornerPointer] < note.StartTime) cornerPointer++;
                        int lastCornerIndex = cornerPointer;

                        for (int i = firstCornerIndex; i < lastCornerIndex; i++)
                        {
                            perColumnPressure[i] = val;
                            perColumnDeltaTimes[i] = delta;
                        }
                    }

                    prev = note;
                }

                perColumnPressure = CornerUtils.SmoothCornersWithinWindow(baseCorners, perColumnPressure, 500, 0.001);

                // Accumulate weighted values directly
                for (int i = 0; i < baseCorners.Length; i++)
                {
                    double weight = perColumnDeltaTimes[i] > 0 ? 1.0 / perColumnDeltaTimes[i] : 0;
                    sameColumnPressure[i] += Math.Pow(Math.Max(perColumnPressure[i], 0), 5.0) * weight;
                    sumWeights[i] += weight;
                }
            }

            for (int t = 0; t < baseCorners.Length; t++)
            {
                if (sumWeights[t] > 0)
                    sameColumnPressure[t] = Math.Pow(sameColumnPressure[t] / sumWeights[t], 1.0 / 5.0);
            }

            sameColumnPressure = CornerUtils.InterpolateValues(allCorners, baseCorners, sameColumnPressure);

            return sameColumnPressure;
        }

        private static double jackNerfer(double delta) => 1 - 7e-5 * Math.Pow(0.15 + Math.Abs(delta - 0.08), -4);
    }
}
