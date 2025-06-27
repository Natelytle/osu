// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class KeyUsage
    {
        public static double[] GetKeyUsages(List<ManiaDifficultyHitObject>[] perColumnNoteList, double[] baseCorners)
        {
            double[] keyUsages = new double[baseCorners.Length];

            for (int column = 0; column < perColumnNoteList.Length; column++)
            {
                int cornerPointer = 0;

                foreach (ManiaDifficultyHitObject note in perColumnNoteList[column])
                {
                    double activeStart = Math.Max(note.StartTime - 150, 0);
                    double activeEnd = Math.Min(note.EndTime + 150, baseCorners[^1]);

                    // Move cornerPointer to first corner after activeStart
                    while (cornerPointer < baseCorners.Length && baseCorners[cornerPointer] < activeStart - 150)
                        cornerPointer++;
                    int startIdx = cornerPointer;

                    // Move cornerPointer to first corner after activeEnd
                    while (cornerPointer < baseCorners.Length && baseCorners[cornerPointer] < activeEnd + 150)
                        cornerPointer++;
                    int endIdx = cornerPointer;

                    for (int i = startIdx; i < endIdx; i++)
                        keyUsages[i] = 1; // OR across all columns
                }
            }

            return keyUsages;
        }

        public static double[][] GetKeyUsages400(List<ManiaDifficultyHitObject>[] perColumnNoteList, double[] baseCorners)
        {
            double[][] keyUsages = new double[perColumnNoteList.Length][];
            double maxCorner = baseCorners[^1];
            const double pow400 = 160000; // 400^2
            const double weight_base = 3.75;
            const double note_cap = 1500;
            const double inv150 = 1.0 / 150;

            for (int column = 0; column < perColumnNoteList.Length; column++)
            {
                keyUsages[column] = new double[baseCorners.Length];
                int cornerPointer = 0;
                int corner400Pointer = 0;

                foreach (var note in perColumnNoteList[column])
                {
                    double activeStart = note.StartTime;
                    double activeEnd = note.EndTime;
                    double activeStart400 = Math.Max(activeStart - 400, 0);
                    double activeEnd400 = Math.Min(activeEnd + 400, maxCorner);

                    // find the first corner at activeStart - 400.
                    while (corner400Pointer < baseCorners.Length && baseCorners[corner400Pointer] < activeStart400) corner400Pointer++;
                    int start400Idx = corner400Pointer;
                    cornerPointer = Math.Max(corner400Pointer, cornerPointer);

                    // find the first corner at activeStart.
                    while (cornerPointer < baseCorners.Length && baseCorners[cornerPointer] < activeStart) cornerPointer++;
                    int startIdx = cornerPointer;

                    // find the first corner at activeEnd.
                    while (cornerPointer < baseCorners.Length && baseCorners[cornerPointer] < activeEnd) cornerPointer++;
                    int endIdx = cornerPointer;
                    corner400Pointer = cornerPointer;

                    // find the first corner at activeEnd + 400.
                    while (corner400Pointer < baseCorners.Length && baseCorners[corner400Pointer] < activeEnd400) corner400Pointer++;
                    int end400Idx = corner400Pointer;

                    double duration = activeEnd - activeStart;
                    double contribution = weight_base + Math.Min(duration, note_cap) * inv150;

                    for (int i = startIdx; i < endIdx; i++)
                        keyUsages[column][i] += contribution;

                    for (int i = start400Idx; i < startIdx; i++)
                    {
                        double d = baseCorners[i] - activeStart;
                        keyUsages[column][i] += weight_base - weight_base * (d * d / pow400);
                    }

                    for (int i = endIdx; i < end400Idx; i++)
                    {
                        double d = baseCorners[i] - activeEnd;
                        keyUsages[column][i] += weight_base - weight_base * (d * d / pow400);
                    }

                    // Reset pointer for next note
                    corner400Pointer = start400Idx;
                }
            }

            return keyUsages;
        }
    }
}
