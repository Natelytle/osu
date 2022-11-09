﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class ContinuousStrainSkill : Skill
    {

        /// <summary>
        /// The final multiplier to be applied to <see cref="DifficultyValue"/> after all other calculations.
        /// </summary>
        protected virtual double DifficultyMultiplier => 1.2;

        protected virtual double SectionLength => 400;
        protected virtual double DecayWeight => 0.9;
        protected abstract double StrainDecayBase { get; }

        private double currentStrain;

        private struct StrainValue
        {
            public double Strain;
            public int StrainCountChange;
        }

        private readonly List<StrainValue> strains = new List<StrainValue>();

        protected ContinuousStrainSkill(Mod[] mods)
            : base(mods)
        {
        }

        public override double DifficultyValue()
        {
            double result = 0.0;
            double currentWeight = 1;
            double frequency = 0;
            var sortedStrains = strains.OrderByDescending(x => (x.Strain, x.StrainCountChange)).ToList();

            double strainDecayRate = Math.Log(StrainDecayBase) * SectionLength / 1000;
            double sumDecayRate = Math.Log(DecayWeight);

            for(int i=0; i<sortedStrains.Count-1;++i)
            {
                var current = sortedStrains[i];
                var next = sortedStrains[i + 1];
                frequency += current.StrainCountChange;

                if (frequency > 0 && current.Strain > 0)
                {
                    double time = Math.Log(next.Strain / current.Strain) * (frequency / strainDecayRate);

                    double nextWeight = currentWeight * Math.Exp(sumDecayRate * time);
                    double combinedDecay = sumDecayRate + (strainDecayRate / frequency);
                    result += (next.Strain * nextWeight - current.Strain * currentWeight) / combinedDecay;
                    currentWeight = nextWeight;
                }
            }

            return result * DifficultyMultiplier;
        }


        protected abstract double StrainValueAt(DifficultyHitObject hitObject);

        public override void Process(DifficultyHitObject current)
        {
            strains.Add(new StrainValue { Strain = currentStrain * Math.Pow(StrainDecayBase, 1e-3 * current.DeltaTime), StrainCountChange = -1 });
            currentStrain = StrainValueAt(current);
            strains.Add(new StrainValue { Strain = currentStrain, StrainCountChange = 1 });
        }

    }
}
