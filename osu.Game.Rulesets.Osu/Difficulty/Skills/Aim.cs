// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuStrainSkill
    {
        public readonly bool IncludeSliders;

        public Aim(Mod[] mods, bool includeSliders)
            : base(mods)
        {
            IncludeSliders = includeSliders;
        }

        private double currentStrain;
        private double lastStrain;

        private double skillMultiplier => 26.4;
        private double strainDecayBase => 0.15;

        private readonly List<double> sliderStrains = new List<double>();
        private readonly List<(double, double)> previousStrains = new List<(double, double)>();

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double CalculateInitialStrain(double deltaTime) => lastStrain * strainDecay(deltaTime);

        protected override IEnumerable<ObjectStrain> StrainValuesAt(DifficultyHitObject current)
        {
            var osuCurrent = (OsuDifficultyHitObject)current;

            double previousTime = osuCurrent.PreviousMovement?.StartTime ?? current.Previous(0)?.StartTime ?? 0;
            lastStrain = currentStrain;

            var firstMovement = osuCurrent.Movements[0];

            currentStrain *= strainDecay(firstMovement.Time);
            currentStrain += AimEvaluator.EvaluateDifficultyOfMovement(current, firstMovement) * skillMultiplier;

            yield return new ObjectStrain
            {
                Time = current.StartTime,
                PreviousTime = previousTime,
                Value = currentStrain,
            };

            previousTime = firstMovement.StartTime;

            if (current.BaseObject is Slider)
                sliderStrains.Add(currentStrain);

            previousStrains.Add((osuCurrent.StartTime, currentStrain));

            double movementStrain = currentStrain;

            for (int i = 1; i < osuCurrent.Movements.Count; i++)
            {
                lastStrain = movementStrain;

                var movement = osuCurrent.Movements[i];

                double difficulty = 0;

                if (IncludeSliders && movement.IsNested)
                {
                    double ratioMultiplier = Math.Pow(Math.Pow(1 - osuCurrent.PathLengthToMovementLengthRatio, 2) + 1, 1);
                    difficulty = AimEvaluator.EvaluateDifficultyOfMovement(current, movement) * skillMultiplier * 3.5 * ratioMultiplier;
                }

                movementStrain = getCurrentStrainValue(movement.StartTime, previousStrains);
                previousStrains.Add((osuCurrent.StartTime, difficulty));

                double totalMovementStrain = difficulty + movementStrain;

                yield return new ObjectStrain
                {
                    Time = movement.StartTime,
                    PreviousTime = previousTime,
                    Value = totalMovementStrain,
                };

                if (current.BaseObject is Slider)
                    sliderStrains.Add(totalMovementStrain);

                previousTime = movement.StartTime;
            }

            currentStrain = movementStrain;
        }

        private const double backwards_strain_influence = 1000;

        private double getCurrentStrainValue(double endTime, List<(double Time, double Diff)> previousDifficulties)
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

                highestNoteVal = Math.Max(prevDifficulty, strainDecay(prevDeltaTime));
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

        public double GetDifficultSliders()
        {
            if (sliderStrains.Count == 0)
                return 0;

            double maxSliderStrain = sliderStrains.Max();

            if (maxSliderStrain == 0)
                return 0;

            return sliderStrains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxSliderStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders() => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, DifficultyValue());
    }
}
