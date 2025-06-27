// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class KeyUsage
    {
        public static bool[][] GetKeyUsages(List<ManiaDifficultyHitObject>[] perColumnNoteList, double[] baseCorners)
        {
            bool[][] keyUsages = new bool[perColumnNoteList.Length][];

            for (int column = 0; column < perColumnNoteList.Length; column++)
            {
                keyUsages[column] = new bool[baseCorners.Length];
                int cornerPointer = 0;

                foreach (ManiaDifficultyHitObject note in perColumnNoteList[column])
                {
                    double activeStart = Math.Max(note.StartTime - 150, 0);
                    double activeEnd = Math.Min(note.EndTime + 150, baseCorners[^1]);

                    // find the first corner at activeStart.
                    while (cornerPointer < baseCorners.Length && baseCorners[cornerPointer] < activeStart - 150) cornerPointer++;
                    int startIdx = cornerPointer;

                    // find the first corner at activeEnd.
                    while (cornerPointer < baseCorners.Length && baseCorners[cornerPointer] < activeEnd + 150) cornerPointer++;
                    int endIdx = cornerPointer;

                    for (int i = startIdx; i < endIdx; i++)
                        keyUsages[column][i] = true;
                }
            }

            return keyUsages;
        }

        public static double[][] GetKeyUsages400(List<ManiaDifficultyHitObject>[] perColumnNoteList, double[] baseCorners)
        {
            double[][] keyUsages = new double[perColumnNoteList.Length][];

            for (int column = 0; column < perColumnNoteList.Length; column++)
            {
                keyUsages[column] = new double[baseCorners.Length];
                int cornerPointer = 0;
                int corner400Pointer = 0;

                foreach (ManiaDifficultyHitObject note in perColumnNoteList[column])
                {
                    double activeStart = note.StartTime;
                    double activeStart400 = Math.Max(activeStart - 400, 0);
                    double activeEnd = note.EndTime;
                    double activeEnd400 = Math.Min(activeEnd - 400, baseCorners[^1]);

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

                    for (int i = startIdx; i < endIdx; i++)
                        keyUsages[column][i] += 3.75 + Math.Min(activeEnd - activeStart, 1500) / 150;

                    for (int i = start400Idx; i < startIdx; i++)
                        keyUsages[column][i] += 3.75 - 3.75 / Math.Pow(400, 2) * Math.Pow(baseCorners[i] - activeStart, 2);

                    for (int i = endIdx; i < end400Idx; i++)
                        keyUsages[column][i] += 3.75 - 3.75 / Math.Pow(400, 2) * Math.Pow(Math.Abs(baseCorners[i] - activeEnd), 2);

                    // Reset the pointer to the last position.
                    corner400Pointer = start400Idx;
                }
            }

            return keyUsages;
        }
    }
}
