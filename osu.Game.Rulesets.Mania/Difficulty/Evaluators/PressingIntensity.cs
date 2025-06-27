// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class PressingIntensity
    {
        public static double[] EvaluatePressingIntensity(List<ManiaDifficultyHitObject> noteList, double hitLeniency, double[] baseCorners, double[] allCorners)
        {
            double[] pressingIntensity = new double[baseCorners.Length];
            int cornerPointer = 0;

            ManiaDifficultyHitObject? prev = null;

            foreach (ManiaDifficultyHitObject note in noteList)
            {
                if (prev is not null)
                {
                    double deltaTime = 0.001 * (note.StartTime - prev.StartTime);

                    // find the first corner at the start time of the previous note
                    while (cornerPointer < baseCorners.Length && baseCorners[cornerPointer] < prev.StartTime) cornerPointer++;
                    int firstCornerIndex = cornerPointer;

                    // if notes are less than 1ms apart
                    if (deltaTime < 1e-4)
                    {
                        pressingIntensity[firstCornerIndex] += 1000 * Math.Pow(0.02 * (4 / hitLeniency - 24.0), 1.0 / 4.0);
                        continue;
                    }

                    double lnCount = calculateLnAmount(prev.StartTime, note.StartTime, prev.CurrentHitObjects, note.CurrentHitObjects);
                    double val = (1.0 / deltaTime) * (1 + 5.0 * lnCount) * streamBooster(deltaTime);

                    if (deltaTime < 2 * hitLeniency / 3.0)
                        val *= Math.Pow(0.08 * (1 / hitLeniency) * (1 - 8.0 * (1.0 / hitLeniency) * Math.Pow(deltaTime - hitLeniency / 2, 2)), 1 / 4.0);
                    else
                        val *= Math.Pow(0.08 * (1 / hitLeniency) * (1 - 8.0 * (1.0 / hitLeniency) * Math.Pow(hitLeniency / 6, 2)), 1 / 4.0);

                    // Uncomment when anchor is implemented
                    // val = Math.Min(val * calculateAnchor(), Math.Max(val, val * 2 - 10));

                    // find the first corner at the start time of the previous note
                    while (cornerPointer < baseCorners.Length && baseCorners[cornerPointer] < note.StartTime) cornerPointer++;
                    int lastCornerIndex = cornerPointer;

                    for (int i = firstCornerIndex; i < lastCornerIndex; i++)
                    {
                        pressingIntensity[i] = val;
                    }
                }

                prev = note;
            }

            pressingIntensity = CornerUtils.SmoothCornersWithinWindow(baseCorners, pressingIntensity, 500, 0.001);

            // Fits it to all corners
            pressingIntensity = CornerUtils.InterpolateValues(allCorners, baseCorners, pressingIntensity);

            return pressingIntensity;
        }

        private static double streamBooster(double delta)
        {
            double val = 7.5 / delta;

            if (val > 160 && val < 360)
            {
                return 1 + 1.7e-7 * (val - 160) * Math.Pow(val - 360, 2);
            }

            return 1;
        }

        private static double calculateAnchor() => 0;

        private static double calculateLnAmount(double startTime, double endTime, ManiaDifficultyHitObject?[] currentObjects, ManiaDifficultyHitObject?[] nextObjects)
        {
            double lnAmount = 0;

            for (int column = 0; column < currentObjects.Length; column++)
            {
                ManiaDifficultyHitObject? currObj = currentObjects[column];
                ManiaDifficultyHitObject? nextObj = nextObjects[column];

                if (currObj is not null && currObj.BaseObject is not Note)
                {
                    double lnEnd = Math.Min(currObj.EndTime, endTime);
                    double fullLnStart = Math.Max(currObj.StartTime + 120, startTime);
                    double partialLnStart = Math.Max(currObj.StartTime + 60, startTime);
                    double partialLnEnd = Math.Min(currObj.StartTime + 120, lnEnd);

                    lnAmount += Math.Max(lnEnd - fullLnStart, 0) + 1.3 * Math.Max(partialLnEnd - partialLnStart, 0);
                }

                if (nextObj?.StartTime != currObj?.StartTime && nextObj is not null && nextObj.BaseObject is not Note)
                {
                    double lnEnd = Math.Min(nextObj.EndTime, endTime);
                    double fullLnStart = Math.Max(nextObj.StartTime + 120, startTime);
                    double partialLnStart = Math.Max(nextObj.StartTime + 60, startTime);
                    double partialLnEnd = Math.Min(nextObj.StartTime + 120, lnEnd);

                    lnAmount += Math.Max(lnEnd - fullLnStart, 0) + 1.3 * Math.Max(partialLnEnd - partialLnStart, 0);
                }
            }

            return lnAmount / 1000;
        }
    }
}
