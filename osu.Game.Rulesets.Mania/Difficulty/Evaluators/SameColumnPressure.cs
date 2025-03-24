// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class SameColumnPressure
    {
        public static double[] EvaluateSameColumnPressure(List<ManiaDifficultyHitObject>[] perColumnNoteList, int totalColumns, int mapLength, double hitLeniency)
        {
            double[] sameColumnPressure = new double[mapLength];
            double[] perColumnPressure = new double[mapLength];
            double[] perColumnDeltaTimes = new double[mapLength];
            double[] sumWeights = new double[mapLength];
            double[] sumValLambdaWeights = new double[mapLength];

            for (int col = 0; col < totalColumns; col++)
            {
                IEnumerable<ManiaDifficultyHitObject> columnNotes = perColumnNoteList[col];

                Array.Clear(perColumnPressure);
                Array.Clear(perColumnDeltaTimes);

                ManiaDifficultyHitObject? prev = null;

                foreach (ManiaDifficultyHitObject note in columnNotes)
                {
                    if (prev is not null && prev.StartTime < note.StartTime)
                    {
                        double delta = 0.001 * (note.StartTime - prev.StartTime);
                        double val = Math.Pow(delta, -1) * Math.Pow(delta + SunnySkill.LAMBDA_1 * Math.Pow(hitLeniency, 1.0 / 4.0), -1.0);

                        // the variables created earlier are filled with delta/val
                        for (int t = (int)prev.StartTime; t < note.StartTime; t++)
                        {
                            perColumnPressure[t] = val;
                            perColumnDeltaTimes[t] = delta;
                        }
                    }

                    prev = note;
                }

                perColumnPressure = ListUtils.ApplySymmetricMovingAverage(perColumnPressure, 500);

                // Process and accumulate weighted values directly
                for (int t = 0; t < mapLength; t++)
                {
                    double val = perColumnPressure[t];
                    double weight = perColumnDeltaTimes[t] > 0 ? 1.0 / perColumnDeltaTimes[t] : 0;

                    sumWeights[t] += weight;
                    sumValLambdaWeights[t] += Math.Pow(val, SunnySkill.LAMBDA_N) * weight;
                }
            }

            for (int t = 0; t < mapLength; t++)
            {
                double firstPart = sumWeights[t] > 0 ? sumValLambdaWeights[t] / sumWeights[t] : 0;

                sameColumnPressure[t] = Math.Pow(firstPart, 1.0 / SunnySkill.LAMBDA_N);
            }

            return sameColumnPressure;
        }
    }
}
