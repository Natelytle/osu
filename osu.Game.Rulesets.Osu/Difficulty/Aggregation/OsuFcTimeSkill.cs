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
    public abstract class OsuFcTimeSkill : Skill
    {
        protected OsuFcTimeSkill(Mod[] mods)
            : base(mods)
        {
        }

        // FC time specific constants
        private const double time_threshold_minutes = 24;
        private const double time_threshold_ms = time_threshold_minutes * 60000;
        private const double max_delta_time = 5000;

        // Bin specific constants
        private const double bin_threshold_note_count = 64;
        private const int difficulty_bin_count = 8;
        private const int time_bin_count = 16;

        private const double epsilon = 1e-4;

        private readonly List<double> difficulties = new List<double>();
        private readonly List<double> times = new List<double>();

        /// <summary>
        /// Returns the strain value at <see cref="DifficultyHitObject"/>. This value is calculated with or without respect to previous objects.
        /// </summary>
        protected abstract double StrainValueAt(DifficultyHitObject current);

        public override void Process(DifficultyHitObject current)
        {
            difficulties.Add(StrainValueAt(current));

            times.Add(times.LastOrDefault() + Math.Min(current.DeltaTime, max_delta_time));
        }

        protected abstract double HitProbability(double skill, double difficulty);

        public override double DifficultyValue()
        {
            if (difficulties.Count == 0 || difficulties.Max() <= epsilon)
                return 0;

            // We only initialize bins if we have enough notes to use them.
            List<Bin>? binList = null;

            if (difficulties.Count > bin_threshold_note_count)
            {
                binList = Bin.CreateBins(difficulties, times, difficulty_bin_count, time_bin_count);
            }

            // Lower bound and upper bound are generally unimportant
            return RootFinding.FindRootExpand(skill => timeSpentRetryingAtSkill(skill, binList) - time_threshold_ms, 0, 10);
        }

        private double timeSpentRetryingAtSkill(double skill, List<Bin>? binList = null)
        {
            if (skill <= 0) return double.PositiveInfinity;

            double t = 0;
            double hitProbabilityProduct = 1;

            // We use bins, falling back to exact difficulty calculation if not available.
            if (binList is not null)
            {
                for (int timeIndex = time_bin_count - 1; timeIndex >= 0; timeIndex--)
                {
                    double deltaTime = times.LastOrDefault() / time_bin_count;

                    for (int difficultyIndex = 0; difficultyIndex < difficulty_bin_count; difficultyIndex++)
                    {
                        Bin bin = binList[difficulty_bin_count * timeIndex + difficultyIndex];

                        hitProbabilityProduct *= Math.Pow(HitProbability(skill, bin.Difficulty), bin.Count);
                    }

                    t += deltaTime / hitProbabilityProduct - deltaTime;
                }
            }
            else
            {
                for (int n = difficulties.Count - 1; n >= 0; n--)
                {
                    double deltaTime = n > 0 ? times[n] - times[n - 1] : times[n];

                    hitProbabilityProduct *= HitProbability(skill, difficulties[n]);
                    t += deltaTime / hitProbabilityProduct - deltaTime;
                }
            }

            return t;
        }

        /// <summary>
        /// The coefficients of a quartic fitted to the miss counts at each skill level.
        /// </summary>
        /// <returns>The coefficients for ax^4+bx^3+cx^2. The 4th coefficient for dx^1 can be deduced from the first 3 in the performance calculator.</returns>
        public Polynomial GetMissPenaltyCurve()
        {
            double[] missCounts = new double[7];
            double[] penalties = { 1, 0.95, 0.9, 0.8, 0.6, 0.3, 0 };

            Polynomial missPenaltyCurve = new Polynomial();

            // If there are no notes, we just return the polynomial with all coefficients 0.
            if (difficulties.Count == 0 || difficulties.Max() == 0)
                return missPenaltyCurve;

            double fcSkill = DifficultyValue();

            var bins = Bin.CreateBins(difficulties, times, difficulty_bin_count, time_bin_count);

            for (int i = 0; i < penalties.Length; i++)
            {
                if (i == 0)
                {
                    missCounts[i] = 0;
                    continue;
                }

                double penalizedSkill = fcSkill * penalties[i];

                // We take the log to squash miss counts, which have large absolute value differences, but low relative differences, into a straighter line for the polynomial.
                missCounts[i] = Math.Log(getMissCountAtSkill(penalizedSkill, bins) + 1);
            }

            missPenaltyCurve.Fit(missCounts);

            return missPenaltyCurve;
        }

        /// <summary>
        /// Find the lowest misscount that a player with the provided <paramref name="skill"/> would likely achieve within 12 minutes of retrying.
        /// </summary>
        private double getMissCountAtSkill(double skill, List<Bin> bins)
        {
            double maxDiff = difficulties.Max();
            double endTime = times.Max();

            if (maxDiff == 0)
                return 0;
            if (skill <= 0)
                return difficulties.Count;

            IterativePoissonBinomial poiBin = new IterativePoissonBinomial();

            return Math.Max(0, RootFinding.FindRootExpand(x => retryTimeRequiredToObtainMissCount(x) - time_threshold_minutes, -50, 1000, accuracy: 0.01));

            double retryTimeRequiredToObtainMissCount(double missCount)
            {
                poiBin.Reset();

                double totalTime = 0;

                if (difficulties.Count > time_bin_count * difficulty_bin_count)
                {
                    double binTimeSteps = endTime / time_bin_count;

                    for (int timeIndex = 0; timeIndex < time_bin_count; timeIndex++)
                    {
                        for (int difficultyIndex = 0; difficultyIndex < difficulty_bin_count; difficultyIndex++)
                        {
                            Bin bin = bins[timeIndex * difficulty_bin_count + difficultyIndex];

                            double missProb = 1 - HitProbability(skill, bin.Difficulty);
                            poiBin.AddBinnedProbabilities(missProb, bin.Count);
                        }

                        totalTime += binTimeSteps * poiBin.CDF(missCount);
                    }
                }
                else
                {
                    for (int i = 0; i < difficulties.Count; i++)
                    {
                        double deltaTime = i > 0 ? times[i] - times[i - 1] : times[i];

                        double missProb = 1 - HitProbability(skill, difficulties[i]);
                        poiBin.AddProbability(missProb);

                        totalTime += deltaTime * poiBin.CDF(missCount);
                    }
                }

                if (poiBin.CDF(missCount) < 1e-10)
                    return double.PositiveInfinity;

                return (totalTime / poiBin.CDF(missCount) - endTime) / 60000;
            }
        }

        /// <summary>
        /// Calculates the number of strains weighted against the top strain.
        /// The result is scaled by clock rate as it affects the total number of strains.
        /// </summary>
        public virtual double CountTopWeightedStrains()
        {
            if (difficulties.Count == 0)
                return 0.0;

            double consistentTopStrain = DifficultyValue() / 10; // What would the top strain be if all strain values were identical

            if (consistentTopStrain == 0)
                return difficulties.Count;

            // Use a weighted sum of all strains. Constants are arbitrary and give nice values
            return difficulties.Sum(s => 1.1 / (1 + Math.Exp(-10 * (s / consistentTopStrain - 0.88))));
        }
    }
}
