// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class Unevenness
    {
        public static double[] EvaluateUnevenness(List<ManiaDifficultyHitObject>[] perColumnNoteList, int totalColumns, double[] aCorners, double[] allCorners)
        {
            double[] unevenness = new double[aCorners.Length];
            double[] currentColumnDeltaTimes = new double[aCorners.Length];
            double?[] previousColumnDeltaTimes = new double?[aCorners.Length];
            int cornerPointer = 0;

            for (int i = 0; i < aCorners.Length; i++)
                unevenness[i] = 1;

            for (int col = 0; col < totalColumns; col++)
            {
                List<ManiaDifficultyHitObject> columnNotes = perColumnNoteList[col];

                ManiaDifficultyHitObject? prev = null;

                foreach (ManiaDifficultyHitObject note in columnNotes)
                {
                    if (prev is not null && prev.StartTime < note.StartTime)
                    {
                        bool previousColumnUsed = col != 0 && note.StartTime - note.CurrentHitObjects[col - 1]?.EndTime < 150;

                        double delta = 0.001 * (note.StartTime - prev.StartTime);

                        // find the first corner at the start time of the previous note
                        while (cornerPointer < aCorners.Length && aCorners[cornerPointer] < prev.StartTime) cornerPointer++;
                        int firstCornerIndex = cornerPointer;

                        // find the first corner at the start time of the previous note
                        while (cornerPointer < aCorners.Length && aCorners[cornerPointer] < note.StartTime) cornerPointer++;
                        int lastCornerIndex = cornerPointer;

                        for (int j = firstCornerIndex; j < lastCornerIndex; j++)
                        {
                            // We only update previous column delta times if the previous column is used.
                            // A map with 7 keys used like X-X-X-X effectively becomes a 4 key map.
                            if (previousColumnUsed)
                                previousColumnDeltaTimes[j] = currentColumnDeltaTimes[j];

                            currentColumnDeltaTimes[j] = delta;
                        }
                    }

                    prev = note;
                }

                if (col == 0)
                    continue;

                for (int i = 0; i < aCorners.Length; i++)
                {
                    if (previousColumnDeltaTimes[i] == null) continue;

                    double prevColumnDeltaTime = previousColumnDeltaTimes[i]!.Value;
                    double currColumnDeltaTime = currentColumnDeltaTimes[i];

                    double currColumnUnevenness = Math.Abs(currColumnDeltaTime - prevColumnDeltaTime) + Math.Max(0, Math.Max(prevColumnDeltaTime, currColumnDeltaTime) - 0.3);

                    if (currColumnUnevenness < 0.02)
                    {
                        unevenness[i] *= Math.Min(0.75 + 0.5 * Math.Max(prevColumnDeltaTime, currColumnDeltaTime), 1);
                    }
                    else if (currColumnUnevenness < 0.07)
                    {
                        unevenness[i] *= Math.Min(0.65 + 5 * currColumnUnevenness + 0.5 * Math.Max(prevColumnDeltaTime, currColumnDeltaTime), 1);
                    }
                }
            }

            unevenness = CornerUtils.AverageCornersWithinWindow(aCorners, unevenness, 500);

            unevenness = CornerUtils.InterpolateValues(allCorners, aCorners, unevenness);

            return unevenness;
        }
    }
}
