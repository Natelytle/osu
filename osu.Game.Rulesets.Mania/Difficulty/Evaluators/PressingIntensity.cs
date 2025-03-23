// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class PressingIntensity
    {
        public static double[] EvaluatePressingIntensity(List<ManiaDifficultyHitObject> noteList, int totalColumns, int mapLength, double hitLeniency)
        {
            double[] pressingIntensity = new double[mapLength];

            ManiaDifficultyHitObject? prev = null;

            foreach (ManiaDifficultyHitObject note in noteList)
            {
                if (prev is not null)
                {
                    double deltaTime = 0.001 * (note.StartTime - prev.StartTime);

                    // if notes are less than 1ms apart
                    if (deltaTime < 1e-4)
                    {
                        pressingIntensity[(int)prev.StartTime] += Math.Pow(0.02 * (4 / hitLeniency - SunnySkill.LAMBDA_3), 1.0 / 4.0);
                        continue;
                    }

                    double lnCount = calculateLnAmount(prev.StartTime, note.StartTime, prev.CurrentHitObjects, note.CurrentHitObjects);

                    double v = 1 + SunnySkill.LAMBDA_2 * lnCount;

                    if (deltaTime < 2 * hitLeniency / 3.0)
                    {
                        for (int t = (int)prev.StartTime; t < note.StartTime; t++)
                        {
                            pressingIntensity[t] = 1 / deltaTime
                                                   * Math.Pow(0.08 * (1 / deltaTime) * (1 - SunnySkill.LAMBDA_3 * (1 / hitLeniency) * Math.Pow(deltaTime - hitLeniency / 2, 2)), 1 / 4.0)
                                                   * streamBooster(deltaTime) * v;
                        }
                    }
                    else
                    {
                        for (int t = (int)prev.StartTime; t < note.StartTime; t++)
                        {
                            pressingIntensity[t] = 1 / deltaTime
                                                   * Math.Pow(0.08 * (1 / deltaTime) * (1 - SunnySkill.LAMBDA_3 * (1 / hitLeniency) * Math.Pow(hitLeniency / 6, 2)), 1 / 4.0)
                                                   * streamBooster(deltaTime) * v;
                        }
                    }
                }

                prev = note;
            }

            pressingIntensity = ListUtils.ApplySymmetricMovingAverage(pressingIntensity, 500);

            return pressingIntensity;
        }

        private static double streamBooster(double delta)
        {
            double val = 15.0 / delta;

            if (val > 180 && val < 340)
            {
                return 1 + 0.2 * Math.Pow(val - 180, 3) * Math.Pow(val - 340, 6) * Math.Pow(10, -18);
            }

            return 1;
        }

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
                    double fullLnStart = Math.Max(currObj.StartTime + 80, startTime);
                    double partialLnStart = Math.Max(currObj.StartTime, startTime);
                    double partialLnEnd = Math.Min(currObj.StartTime + 80, lnEnd);

                    lnAmount += Math.Max(lnEnd - fullLnStart, 0) + 0.5 * Math.Max(partialLnEnd - partialLnStart, 0);
                }

                if (nextObj?.StartTime != currObj?.StartTime && nextObj is not null && nextObj.BaseObject is not Note)
                {
                    double lnEnd = Math.Min(nextObj.EndTime, endTime);
                    double fullLnStart = Math.Max(nextObj.StartTime + 80, startTime);
                    double partialLnStart = Math.Max(nextObj.StartTime, startTime);
                    double partialLnEnd = Math.Min(nextObj.StartTime + 80, lnEnd);

                    lnAmount += Math.Max(lnEnd - fullLnStart, 0) + 0.5 * Math.Max(partialLnEnd - partialLnStart, 0);
                }
            }

            return lnAmount / 1000;
        }
    }
}
