// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class Unevenness
    {
        public static double[] EvaluateUnevenness(List<ManiaDifficultyHitObject>[] perColumnNoteList, int totalColumns, int mapLength, double hitLeniency)
        {
            double[] unevenness = new double[mapLength];
            double[] currentColumnDeltaTimes = new double[mapLength];

            for (int i = 0; i < mapLength; i++)
                unevenness[i] = 1;

            for (int col = 0; col < totalColumns; col++)
            {
                double[] previousColumnDeltaTimes = currentColumnDeltaTimes.ToArray();

                List<ManiaDifficultyHitObject> columnNotes = perColumnNoteList[col];

                for (int i = 1; i < columnNotes.Count; i++)
                {
                    ManiaDifficultyHitObject prev = columnNotes[i - 1];
                    ManiaDifficultyHitObject curr = columnNotes[i];

                    double delta = 0.001 * (curr.StartTime - prev.StartTime);

                    // the variables created earlier are filled with delta/val
                    for (int t = (int)prev.StartTime; t < curr.StartTime; t++)
                    {
                        currentColumnDeltaTimes[t] = delta;
                    }
                }

                if (col == 0)
                    continue;

                for (int t = 0; t < mapLength; t++)
                {
                    double currColumnUnevenness = Math.Abs(currentColumnDeltaTimes[t] - previousColumnDeltaTimes[t]) + Math.Max(0, Math.Max(previousColumnDeltaTimes[t], currentColumnDeltaTimes[t]) - 0.3);

                    if (currColumnUnevenness < 0.02)
                    {
                        unevenness[t] *= Math.Min(0.75 + 0.5 * Math.Max(previousColumnDeltaTimes[t], currentColumnDeltaTimes[t]), 1);
                    }
                    else if (currColumnUnevenness < 0.07)
                    {
                        unevenness[t] *= Math.Min(0.65 + 5 * currColumnUnevenness + 0.5 * Math.Max(previousColumnDeltaTimes[t], currentColumnDeltaTimes[t]), 1);
                    }
                }
            }

            unevenness = ListUtils.ApplyAdaptiveMovingAverage(unevenness, 500);

            return unevenness;
        }
    }
}
