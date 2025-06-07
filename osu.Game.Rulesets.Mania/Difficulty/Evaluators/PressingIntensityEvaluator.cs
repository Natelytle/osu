// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class PressingIntensityEvaluator
    {
        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject?[] concurrentObjects)
        {
            double pressingIntensity = 0;

            foreach (ManiaDifficultyHitObject? maniaCurr in concurrentObjects)
            {
                if (maniaCurr is null) continue;

                if (maniaCurr.DeltaTime == 0)
                {
                    pressingIntensity += 0.85;
                    continue;
                }

                var maniaPrev = (ManiaDifficultyHitObject?)maniaCurr.Previous(0);

                double lnCount = maniaPrev is not null ? calculateLnAmount(maniaPrev.StartTime, maniaCurr.StartTime, maniaPrev.CurrentHitObjects, maniaCurr.CurrentHitObjects) : 0;

                double difficulty = (1000 / maniaCurr.DeltaTime) * (1 + 6 * lnCount) * streamBooster(maniaCurr.DeltaTime);

                difficulty *= maniaCurr.DeltaTime < 50 ? 0.85 + 0.15 * (1 + Math.Pow((maniaCurr.DeltaTime - 50) / 50, 3)) : 1;

                pressingIntensity += difficulty;
            }

            return pressingIntensity;
        }

        private static double streamBooster(double delta)
        {
            double val = 7500 / delta;

            if (val > 160 && val < 360)
            {
                return 1 + 1.7e-7 * (val - 160) * Math.Pow(val - 360, 2);
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
