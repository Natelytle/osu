// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using System.Linq;
using osu.Framework.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class OsuContinuousSkill : ContinuousSkill
    {
        /// <summary>
        /// The final multiplier to be applied to <see cref="DifficultyValue"/> after all other calculations.
        /// </summary>
        protected override double DifficultyMultiplier => 1.27;

        /// <summary>
        /// The number of sections with the highest strains, which the peak strain reductions will apply to.
        /// This is done in order to decrease their impact on the overall difficulty of the map for this skill.
        /// </summary>
        protected virtual int ReducedSectionCount => 10;

        /// <summary>
        /// The baseline multiplier applied to the section with the biggest strain.
        /// </summary>
        protected virtual double ReducedStrainBaseline => 0.75;

        protected OsuContinuousSkill(Mod[] mods)
            : base(mods)
        {
        }

        public override double DifficultyValue()
        {
            double result = 0.0;
            double currentWeight = 1;
            double frequency = 0;
            var sortedStrains = Strains.OrderByDescending(x => x.Strain).ToList();

            double strainDecayRate = Math.Log(StrainDecayBase) / 1000;
            double sumDecayRate = Math.Log(DecayWeight) / SectionLength;

            double totalTime = 0;

            for (int i = 0; i < sortedStrains.Count - 1; i++)
            {
                var current = sortedStrains[i];
                var next = sortedStrains[i + 1];
                frequency += current.StrainCountChange;

                if (totalTime > ReducedSectionCount * SectionLength)
                    break;

                if (current.Strain > 0)
                {
                    totalTime += frequency > 0 ? Math.Log(next.Strain / current.Strain) * (frequency / strainDecayRate) : 0;

                    double scale = Math.Log10(Interpolation.Lerp(1, 10, Math.Clamp(totalTime / (ReducedSectionCount * SectionLength), 0, 1)));
                    sortedStrains[i] = current with { Strain = current.Strain * Interpolation.Lerp(ReducedStrainBaseline, 1.0, scale) };
                }
            }

            frequency = 0;

            // Resort strains after reducing beginning sections
            sortedStrains = sortedStrains.OrderByDescending(x => x.Strain).ToList();

            for (int i = 0; i < sortedStrains.Count - 1; i++)
            {
                var current = sortedStrains[i];
                var next = sortedStrains[i + 1];
                frequency += current.StrainCountChange;

                if (frequency > 0 && current.Strain > 0)
                {
                    double time = Math.Log(next.Strain / current.Strain) * (frequency / strainDecayRate);

                    double nextWeight = currentWeight * Math.Exp(sumDecayRate * time);
                    double combinedDecay = SectionLength * (sumDecayRate + (strainDecayRate / frequency));
                    result += (next.Strain * nextWeight - current.Strain * currentWeight) / combinedDecay;
                    currentWeight = nextWeight;
                }
            }

            return result * DifficultyMultiplier;
        }

        public static double DifficultyToPerformance(double difficulty) => Math.Pow(5.0 * Math.Max(1.0, difficulty / 0.0675) - 4.0, 3.0) / 100000.0;
    }
}
