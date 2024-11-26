// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Aggregation
{
    public abstract class OsuProbSkill : Skill
    {
        protected OsuProbSkill(Mod[] mods)
            : base(mods)
        {
        }

        // Assume players spend 12 minutes retrying a map before they FC
        private const double fc_probability = 0.02;

        private const int difficulty_bin_count = 16;

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

        public double DifficultyValueExact()
        {
            double maxDiff = difficulties.Max();
            if (maxDiff <= 1e-10) return 0;

            const double lower_bound = 0;
            double upperBoundEstimate = 3.0 * maxDiff;

            double skill = RootFinding.FindRootExpand(
                skill => fcProbability(skill) - fc_probability,
                lower_bound,
                upperBoundEstimate,
                accuracy: 1e-4);

            return skill;

            double fcProbability(double s)
            {
                if (s <= 0) return 0;

                return difficulties.Aggregate<double, double>(1, (current, d) => current * HitProbability(s, d));
            }
        }

        public double DifficultyValueBinned()
        {
            double maxDiff = difficulties.Max();
            if (maxDiff <= 1e-10) return 0;

            var bins = DifficultyBins.CreateBins(difficulties, difficulty_bin_count);

            const double lower_bound = 0;
            double upperBoundEstimate = 3.0 * maxDiff;

            double skill = RootFinding.FindRootExpand(
                skill => fcProbability(skill) - fc_probability,
                lower_bound,
                upperBoundEstimate,
                accuracy: 1e-4);

            return skill;

            double fcProbability(double s)
            {
                if (s <= 0) return 0;

                return bins.Aggregate(1.0, (current, bin) => current * Math.Pow(HitProbability(s, bin.Difficulty), bin.Count));
            }
        }

        public override double DifficultyValue()
        {
            if (difficulties.Count == 0) return 0;

            return difficulties.Count > 2 * difficulty_bin_count ? DifficultyValueBinned() : DifficultyValueExact();
        }

        /// <summary>
        /// The coefficients of a quartic fitted to the miss counts at each skill level.
        /// </summary>
        /// <returns>The coefficients for ax^4+bx^3+cx^2. The 4th coefficient for dx^1 can be deduced from the first 3 in the performance calculator.</returns>
        public ExpPolynomial GetMissCountPolynomial()
        {
            double[] missCounts = new double[7];
            double[] penalties = { 1, 0.95, 0.9, 0.8, 0.6, 0.3, 0 };

            ExpPolynomial polynomial = new ExpPolynomial(3);

            // If there are no notes, we just return the polynomial with all coefficients 0.
            if (difficulties.Count == 0 || difficulties.Max() == 0)
                return polynomial;

            double fcSkill = DifficultyValue();

            DifficultyBins[] bins = DifficultyBins.CreateBins(difficulties, difficulty_bin_count);

            for (int i = 0; i < penalties.Length; i++)
            {
                if (i == 0)
                {
                    missCounts[i] = 0;
                    continue;
                }

                double penalizedSkill = fcSkill * penalties[i];

                missCounts[i] = getMissCountAtSkill(penalizedSkill, bins);
            }

            polynomial.Fit(missCounts);

            return polynomial;
        }

        /// <summary>
        /// Find the lowest misscount that a player with the provided <paramref name="skill"/> would likely achieve within 12 minutes of retrying.
        /// </summary>
        private double getMissCountAtSkill(double skill, DifficultyBins[] bins)
        {
            double maxDiff = difficulties.Max();

            if (maxDiff == 0)
                return 0;
            if (skill <= 0)
                return difficulties.Count;

            var poiBin = difficulties.Count > 64 ? new PoissonBinomial(bins, skill, HitProbability) : new PoissonBinomial(difficulties, skill, HitProbability);

            return Math.Max(0, RootFinding.FindRootExpand(x => poiBin.CDF(x) - fc_probability, -50, 1000, accuracy: 1e-4));
        }
    }
}
