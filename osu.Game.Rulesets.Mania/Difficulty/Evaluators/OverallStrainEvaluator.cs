// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class OverallStrainEvaluator
    {
        private const double difficulty_multiplier = 0.3;

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            double totalDifficulty = 0;
            double chordValue = 0;

            ManiaDifficultyHitObject?[] currentObjects = current.CurrentHitObjects;

            foreach (ManiaDifficultyHitObject? columnObj in currentObjects)
            {
                if (columnObj is null) continue;

                double deltaTime = current.StartTime - columnObj.StartTime;

                if (deltaTime == 0)
                {
                    chordValue += 8.5;
                    continue;
                }

                // Grace tolerance is the lesser of 30ms and the time between the furthest note in the column object's column and the current start time divided by 3.
                // This means a trill will never be treated as a grace note.
                double graceToleranceMax = Math.Min(30, Math.Max((columnObj.NextInColumn(0)?.StartTime - current.StartTime) / 3.0 ?? 30, (current.StartTime - columnObj.StartTime) / 3.0));
                double graceToleranceMin = graceToleranceMax / 2;

                // Chords are notes up to 15ms apart, which is our grace note tolerance.
                chordValue += 8.5 * DifficultyCalculationUtils.Smootherstep(deltaTime, graceToleranceMax, graceToleranceMin);

                double lnCount = calculateLnAmount(current.StartTime - 200, current.StartTime, currentObjects);

                double difficulty = 1000 / deltaTime;

                difficulty *= Math.Max(1 + lnCount / 400, streamBooster(columnObj.DeltaTime));

                difficulty *= deltaTime < 50 ? 0.85 + 0.15 * (1 + Math.Pow((deltaTime - 50) / 50, 3)) : 1;

                // We only want to reward non-chord-hittable notes, so we apply the opposite of the chord multiplier to it.
                difficulty *= DifficultyCalculationUtils.Smootherstep(deltaTime, graceToleranceMin, graceToleranceMax);

                totalDifficulty = Math.Max(difficulty, totalDifficulty);
            }

            totalDifficulty += chordValue;

            return totalDifficulty * difficulty_multiplier;
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

        private static double calculateLnAmount(double startTime, double endTime, ManiaDifficultyHitObject?[] endTimeObjects)
        {
            double lnAmount = 0;

            for (int column = 0; column < endTimeObjects.Length; column++)
            {
                ManiaDifficultyHitObject? obj = endTimeObjects[column];

                while (obj is not null && obj.EndTime > startTime)
                {
                    if (obj.BaseObject is not Note)
                    {
                        double lnEnd = Math.Min(obj.EndTime, endTime);
                        double fullLnStart = Math.Max(obj.StartTime + 120, startTime);
                        double partialLnStart = Math.Max(obj.StartTime + 60, startTime);
                        double partialLnEnd = Math.Min(obj.StartTime + 120, lnEnd);

                        lnAmount += Math.Max(lnEnd - fullLnStart, 0) + 1.3 * Math.Max(partialLnEnd - partialLnStart, 0);
                    }

                    obj = obj.PrevInColumn(0);
                }
            }

            return lnAmount;
        }
    }
}
