// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Strain : ManiaAccuracySkill
    {
        // private const double individual_decay_base = 0.125;
        // private const double overall_decay_base = 0.30;

        // private const double individual_multiplier = 0.43;
        private const double pi_multiplier = 0.2;
        private const double scp_multiplier = 0.2;

        private readonly List<double> pressingIntensityStrains = new List<double>();
        private readonly List<double>[] sameColumnPressureStrains;

        public Strain(Mod[] mods, double od, int totalColumns)
            : base(mods, od)
        {
            sameColumnPressureStrains = new List<double>[totalColumns];

            for (int i = 0; i < totalColumns; i++)
                sameColumnPressureStrains[i] = new List<double>();
        }

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            var maniaCurrent = (ManiaDifficultyHitObject)current;

            // Update combined strain values here.
            double pressingIntensity = PressingIntensityEvaluator.EvaluateDifficultyOf(maniaCurrent.ConcurrentHitObjects) * pi_multiplier;
            double pressingIntensityStrain = calculateWeightedAverage(current, pressingIntensity, pressingIntensityStrains, 1000);
            pressingIntensityStrains.Add(pressingIntensity);

            double sameColumnPressureStrain = 0;
            double sameColumnPressureWeightSum = 0;

            // Update concurrent strain values here.
            if (current.DeltaTime > 0)
            {
                for (int column = 0; column < maniaCurrent.ConcurrentHitObjects.Length; column++)
                {
                    var obj = maniaCurrent.ConcurrentHitObjects[column];

                    double weight;

                    if (obj is null)
                    {
                        weight = 1.0 / (maniaCurrent.StartTime - maniaCurrent.CurrentHitObjects[column]?.StartTime + maniaCurrent.CurrentHitObjects[column]?.ColumnDeltaTime) ?? 0;
                        sameColumnPressureStrain += Math.Pow(calculateWeightedAverageInColumn(current, 0, sameColumnPressureStrains, 1000, column), 5) * weight;
                        sameColumnPressureWeightSum += weight;
                        continue;
                    }

                    double sameColumnPressure = SameColumnPressureEvaluator.EvaluateDifficultyOf(obj) * scp_multiplier;
                    weight = 1.0 / obj.ColumnDeltaTime;
                    sameColumnPressureStrain += Math.Pow(calculateWeightedAverageInColumn(current, sameColumnPressure, sameColumnPressureStrains, 1000, column), 5) * weight;
                    sameColumnPressureWeightSum += weight;
                    sameColumnPressureStrains[column].Add(sameColumnPressure);
                }

                sameColumnPressureStrain = Math.Pow(sameColumnPressureStrain / sameColumnPressureWeightSum, 1 / 5.0);
            }

            return sameColumnPressureStrain + pressingIntensityStrain;
        }

        private double calculateWeightedAverage(DifficultyHitObject current, double currentDifficulty, List<double> previousDifficulties, double timeBackwards)
        {
            double cumulativeDeltaTime = current.DeltaTime;
            double difficultySum = currentDifficulty * current.DeltaTime;

            for (int i = 0; i < pressingIntensityStrains.Count; i++)
            {
                if (cumulativeDeltaTime > timeBackwards)
                {
                    previousDifficulties.RemoveRange(0, i);
                    break;
                }

                double previousDeltaTime = current.Previous(i).DeltaTime;

                // Assume all notes are at most 20ms of difficulty.
                difficultySum += previousDifficulties[^(i + 1)] * Math.Min(previousDeltaTime, timeBackwards - cumulativeDeltaTime);

                cumulativeDeltaTime += previousDeltaTime;
            }

            return difficultySum / timeBackwards;
        }

        private double calculateWeightedAverageInColumn(DifficultyHitObject current, double currentDifficulty, List<double>[] previousDifficulties, double timeBackwards, int column)
        {
            ManiaDifficultyHitObject? columnNote = ((ManiaDifficultyHitObject)current).Column == column ? ((ManiaDifficultyHitObject)current).CurrentHitObjects[column] : ((ManiaDifficultyHitObject)current).CurrentHitObjects[column]?.NextInColumn(0);

            if (columnNote is null)
                return 0;

            double cumulativeDeltaTime = columnNote.ColumnDeltaTime + current.StartTime - columnNote.StartTime;
            double difficultySum = currentDifficulty * columnNote.ColumnDeltaTime;

            for (int i = 0; i < previousDifficulties[column].Count; i++)
            {
                if (cumulativeDeltaTime > timeBackwards)
                {
                    previousDifficulties[column].RemoveRange(0, i);
                    break;
                }

                // Hack gotta fix this later
                if (i == 0 && ((ManiaDifficultyHitObject)current).Column != column)
                {
                    continue;
                }

                double previousDeltaTime = columnNote.PrevInColumn(i)!.ColumnDeltaTime;

                // Assume all notes are at most 20ms of difficulty.
                difficultySum += previousDifficulties[column][^(i + 1)] * Math.Min(previousDeltaTime, timeBackwards - cumulativeDeltaTime);

                cumulativeDeltaTime += previousDeltaTime;
            }

            return difficultySum / timeBackwards;
        }

        private double applyDecay(double value, double deltaTime, double decayBase)
            => value * Math.Pow(decayBase, deltaTime / 1000);
    }
}
