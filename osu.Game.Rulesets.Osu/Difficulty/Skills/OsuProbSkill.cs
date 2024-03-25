// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class OsuProbSkill : Skill
    {
        protected OsuProbSkill(Mod[] mods)
            : base(mods)
        {
        }

        /// The skill level returned from this class will have FcProbability chance of hitting every note correctly.
        /// A higher value rewards short, high difficulty sections, whereas a lower value rewards consistent, lower difficulty.
        protected abstract double FcProbability { get; }

        private const int bin_count = 32;

        private readonly List<double> difficulties = new List<double>();

        /// <summary>
        /// Returns the strain value at <see cref="DifficultyHitObject"/>. This value is calculated with or without respect to previous objects.
        /// </summary>
        protected abstract double StrainValueAt(DifficultyHitObject current);

        public override void Process(DifficultyHitObject current)
        {
            difficulties.Add(StrainValueAt(current));
        }

        protected abstract double HitProbability(double skill, double difficulty);

        private double fcDifficultyValue()
        {
            var bins = Bin.CreateBins(difficulties, bin_count);

            const double lower_bound = 0;
            double upperBoundEstimate = 3.0 * difficulties.Max();

            double skill = Chandrupatla.FindRootExpand(
                skill => fcProbability(skill) - FcProbability,
                lower_bound,
                upperBoundEstimate,
                accuracy: 1e-4);

            return skill;

            double fcProbability(double s)
            {
                if (s <= 0) return 0;

                return difficulties.Count < 2 * bin_count
                    ? difficulties.Aggregate<double, double>(1, (current, d) => current * HitProbability(s, d))
                    : bins.Aggregate(1.0, (current, bin) => current * Math.Pow(HitProbability(s, bin.Difficulty), bin.Count));
            }
        }

        private double missDifficultyValue(double missCount)
        {
            var bins = Bin.CreateBins(difficulties, bin_count);

            const double lower_bound = 0;
            double upperBoundEstimate = 3.0 * difficulties.Max();

            double skill = Chandrupatla.FindRootExpand(
                skill => missProbabilityAtSkillExact(skill) - FcProbability,
                lower_bound,
                upperBoundEstimate,
                accuracy: 1e-4);

            return skill;

            double missProbabilityAtSkillExact(double s)
            {
                if (s <= 0) return 0;

                return difficulties.Count < 2 * bin_count
                    ? new PoissonBinomial(difficulties, s, HitProbability).CDF(missCount)
                    : new PoissonBinomial(bins, s, HitProbability).CDF(missCount);
            }
        }

        // Assume full combo
        public override double DifficultyValue() => DifficultyValue(0);

        public double DifficultyValue(double missCount)
        {
            if (difficulties.Count == 0 || difficulties.Max() <= 1e-10 || missCount == difficulties.Count)
                return 0;

            return missCount == 0 ? fcDifficultyValue() : missDifficultyValue(missCount);
        }
    }
}
