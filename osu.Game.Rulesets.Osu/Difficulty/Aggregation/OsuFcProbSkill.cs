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
    public abstract class OsuFcProbSkill : Skill
    {
        protected OsuFcProbSkill(Mod[] mods)
            : base(mods)
        {
        }

        // We return the skill level that, on average, requires 50 retries to attain a full combo.
        private const double attempt_threshold = 50;
        private const double probability_threshold = 1 / attempt_threshold;

        private const double bin_threshold_note_count = 64;
        private const int difficulty_bin_count = 32;

        private const double epsilon = 1e-4;

        /// <summary>
        /// Returns the strain value at <see cref="DifficultyHitObject"/>. This value is calculated with or without respect to previous objects.
        /// </summary>
        protected abstract double StrainValueAt(DifficultyHitObject current);

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            return StrainValueAt(current);
        }

        protected abstract double HitProbability(double skill, double difficulty);

        public override double DifficultyValue()
        {
            if (ObjectDifficulties.Count == 0 || ObjectDifficulties.Max() <= epsilon)
                return 0;

            // We only initialize bins if we have enough notes to use them.
            List<Bin>? binList = null;

            if (ObjectDifficulties.Count > bin_threshold_note_count)
            {
                binList = Bin.CreateBins(ObjectDifficulties, difficulty_bin_count);
            }

            // Lower bound and upper bound are generally unimportant
            return RootFinding.FindRootExpand(skill => probabilityOfFcAtSkill(skill, binList) - probability_threshold, 0, 10);
        }

        private double probabilityOfFcAtSkill(double skill, List<Bin>? binList = null)
        {
            if (skill <= 0)
                return 0;

            double fcProbability = 1;

            // We use bins, falling back to exact difficulty calculation if not available.
            if (binList is not null)
            {
                foreach (Bin bin in binList)
                {
                    fcProbability *= Math.Pow(HitProbability(skill, bin.Difficulty), bin.NoteCount);
                }
            }
            else
            {
                foreach (double difficulty in ObjectDifficulties)
                {
                    fcProbability *= HitProbability(skill, difficulty);
                }
            }

            return fcProbability;
        }

        /// <summary>
        /// The coefficients of a quartic fitted to the miss counts at each skill level.
        /// </summary>
        /// <returns>The coefficients for ax^4+bx^3+cx^2. The 4th coefficient for dx^1 can be deduced from the first 3 in the performance calculator.</returns>
        public Polynomial GetMissPenaltyCurve()
        {
            Dictionary<double, double> missCounts = new Dictionary<double, double>();

            Polynomial missPenaltyCurve = new Polynomial();

            // If there are no notes, we just return the polynomial with all coefficients 0.
            if (ObjectDifficulties.Count == 0 || ObjectDifficulties.Max() == 0)
                return missPenaltyCurve;

            double fcSkill = DifficultyValue();

            var bins = Bin.CreateBins(ObjectDifficulties, difficulty_bin_count);

            foreach (double skillProportion in Polynomial.SKILL_PROPORTIONS)
            {
                if (skillProportion == 0)
                {
                    missCounts[skillProportion] = 0;
                    continue;
                }

                double penalizedSkill = fcSkill * skillProportion;

                // We take the log to squash miss counts, which have large absolute value differences, but low relative differences, into a straighter line for the polynomial.
                missCounts[skillProportion] = Math.Log(getMissCountAtSkill(penalizedSkill, bins) + 1);
            }

            missPenaltyCurve.Fit(missCounts);

            return missPenaltyCurve;
        }

        /// <summary>
        /// Find the lowest miss count that a player with the provided <paramref name="skill"/> would likely achieve within 50 attempts.
        /// </summary>
        private double getMissCountAtSkill(double skill, List<Bin> bins)
        {
            double maxDiff = ObjectDifficulties.Max();

            if (maxDiff == 0)
                return 0;
            if (skill <= 0)
                return ObjectDifficulties.Count;

            var poiBin = ObjectDifficulties.Count > bin_threshold_note_count ? new PoissonBinomial(bins, skill, HitProbability) : new PoissonBinomial(ObjectDifficulties, skill, HitProbability);

            return Math.Max(0, RootFinding.FindRootExpand(x => poiBin.CDF(x) - probability_threshold, -50, 1000, accuracy: 1e-4));
        }

        /// <summary>
        /// Calculates the number of strains weighted against the top strain.
        /// The result is scaled by clock rate as it affects the total number of strains.
        /// </summary>
        public virtual double CountTopWeightedStrains(double difficultyValue)
        {
            if (ObjectDifficulties.Count == 0)
                return 0.0;

            // What would the top strain be if all strain values were identical.
            // We don't have decay weight in FC time, so we just use the old live one of 0.95.
            double consistentTopStrain = difficultyValue * (1 - 0.95);

            if (consistentTopStrain == 0)
                return ObjectDifficulties.Count;

            // Use a weighted sum of all strains. Constants are arbitrary and give nice values
            return ObjectDifficulties.Sum(s => 1.1 / (1 + Math.Exp(-10 * (s / consistentTopStrain - 0.88))));
        }
    }
}
