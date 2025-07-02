// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Strain : StrainSkill
    {
        private const double individual_decay_base = 0.125;
        private const double overall_decay_base = 0.30;

        private const double backwards_strain_influence = 1000;

        private readonly List<(double, double)>[] previousIndividualStrains;
        private readonly List<(double, double)> previousOverallStrains;

        public Strain(Mod[] mods, int totalColumns)
            : base(mods)
        {
            previousIndividualStrains = new List<(double, double)>[totalColumns];

            for (int i = 0; i < previousIndividualStrains.Length; i++)
                previousIndividualStrains[i] = new List<(double, double)>();

            previousOverallStrains = new List<(double, double)>();
        }

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            if (current.BaseObject is TailNote)
                return 0;

            var maniaCurrent = (ManiaDifficultyHitObject)current;

            double individualDifficulty = IndividualStrainEvaluator.EvaluateDifficultyOf(maniaCurrent);
            previousIndividualStrains[maniaCurrent.Column].Add((maniaCurrent.StartTime, individualDifficulty));

            double individualStrain = getCurrentStrainValue(maniaCurrent.StartTime, previousIndividualStrains[maniaCurrent.Column], individual_decay_base);

            double overallDifficulty = OverallStrainEvaluator.EvaluateDifficultyOf(maniaCurrent);
            previousOverallStrains.Add((maniaCurrent.StartTime, overallDifficulty));

            double overallStrain = getCurrentStrainValue(maniaCurrent.StartTime, previousOverallStrains, overall_decay_base);

            return individualStrain + overallStrain;
        }

        private double getCurrentStrainValue(double endTime, List<(double Time, double Diff)> previousDifficulties, double strainDecayBase)
        {
            if (previousDifficulties.Count < 2)
                return 0;

            double sum = 0;

            double highestNoteVal = 0;
            double prevDeltaTime = 0;

            int index = 1;

            while (index < previousDifficulties.Count)
            {
                double prevTime = previousDifficulties[index - 1].Time;
                double currTime = previousDifficulties[index].Time;

                double deltaTime = currTime - prevTime;
                double prevDifficulty = previousDifficulties[index - 1].Diff;

                // How much of the current deltaTime does not fall under the backwards strain influence value.
                double startTimeOffset = Math.Max(0, endTime - prevTime - backwards_strain_influence);

                // If the deltaTime doesn't fall into the backwards strain influence value at all, we can remove its corresponding difficulty.
                // We don't iterate index because the list moves backwards.
                if (startTimeOffset > deltaTime)
                {
                    previousDifficulties.RemoveAt(0);

                    continue;
                }

                highestNoteVal = Math.Max(prevDifficulty, applyDecay(highestNoteVal, prevDeltaTime, strainDecayBase));
                prevDeltaTime = deltaTime;

                sum += highestNoteVal * (strainDecayAntiderivative(startTimeOffset) - strainDecayAntiderivative(deltaTime));

                index++;
            }

            // CalculateInitialStrain stuff
            highestNoteVal = Math.Max(previousDifficulties.Last().Diff, highestNoteVal);
            double lastTime = previousDifficulties.Last().Time;
            sum += (strainDecayAntiderivative(0) - strainDecayAntiderivative(endTime - lastTime)) * highestNoteVal;

            return sum;

            double strainDecayAntiderivative(double t) => Math.Pow(strainDecayBase, t / 1000) / Math.Log(1.0 / strainDecayBase);
        }

        protected override double CalculateInitialStrain(double offset, DifficultyHitObject current)
        {
            var maniaCurrent = (ManiaDifficultyHitObject)current;

            double individualStrain = getCurrentStrainValue(offset, previousIndividualStrains[maniaCurrent.Column], individual_decay_base);
            double overallStrain = getCurrentStrainValue(offset, previousOverallStrains, overall_decay_base);

            return individualStrain + overallStrain;
        }

        private double applyDecay(double value, double deltaTime, double decayBase)
            => value * Math.Pow(decayBase, deltaTime / 1000);
    }
}
