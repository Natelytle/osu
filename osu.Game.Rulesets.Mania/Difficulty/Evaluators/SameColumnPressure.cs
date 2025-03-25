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
                        double val = (1.0 / delta) * (1.0 / (delta + SunnySkill.LAMBDA_1 * Math.Pow(hitLeniency, 1.0 / 4.0)));
                        val *= jackNerfer(delta);

                        for (int t = (int)prev.StartTime; t < note.StartTime; t++)
                        {
                            perColumnPressure[t] = val;
                            perColumnDeltaTimes[t] = delta;
                        }
                    }

                    prev = note;
                }

                perColumnPressure = ListUtils.ApplySymmetricMovingAverage(perColumnPressure, 500);

                // Accumulate weighted values directly
                for (int t = 0; t < mapLength; t++)
                {
                    double weight = perColumnDeltaTimes[t] > 0 ? 1.0 / perColumnDeltaTimes[t] : 0;
                    sameColumnPressure[t] += Math.Pow(perColumnPressure[t], SunnySkill.LAMBDA_N) * weight;
                    sumWeights[t] += weight;
                }
            }

            for (int t = 0; t < mapLength; t++)
            {
                if (sumWeights[t] > 0)
                    sameColumnPressure[t] = Math.Pow(sameColumnPressure[t] / sumWeights[t], 1.0 / SunnySkill.LAMBDA_N);
            }

            return sameColumnPressure;
        }

        private static double jackNerfer(double delta) => 1 - 7e-5 * Math.Pow(0.15 + Math.Abs(delta - 0.08), -4);
    }
}
