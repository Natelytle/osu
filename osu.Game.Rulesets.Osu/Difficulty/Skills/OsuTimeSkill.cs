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
    public abstract class OsuTimeSkill : Skill
    {
        protected OsuTimeSkill(Mod[] mods)
            : base(mods)
        {
        }

        // The width of each dimension of the bins. Since the array of bins is 2 dimensional, the number of bins is equal to these values multiplied together.
        private const int difficulty_bin_count = 8;
        private const int time_bin_count = 12;

        // Assume players spend 12 minutes retrying a map before they FC
        private const double time_threshold = 12;

        private readonly List<double> difficulties = new List<double>();
        private readonly List<double> times = new List<double>();

        /// <summary>
        /// Returns the strain value at <see cref="DifficultyHitObject"/>. This value is calculated with or without respect to previous objects.
        /// </summary>
        protected abstract double StrainValueAt(DifficultyHitObject current);

        public override void Process(DifficultyHitObject current)
        {
            difficulties.Add(StrainValueAt(current));

            // Cap the delta time of a given note at 5 seconds to not reward absurdly long breaks
            times.Add(times.LastOrDefault() + Math.Min(current.DeltaTime, 5000));
        }

        protected abstract double HitProbability(double skill, double difficulty);

        public double DifficultyValueExact()
        {
            double maxDiff = difficulties.Max();
            if (maxDiff <= 1e-10) return 0;

            const double lower_bound_estimate = 0;
            double upperBoundEstimate = 3.0 * maxDiff;

            double skill = RootFinding.FindRootExpand(
                skill => fcTime(skill) - time_threshold * 60000,
                lower_bound_estimate,
                upperBoundEstimate);

            return skill;

            double fcTime(double s)
            {
                if (s <= 0) return double.PositiveInfinity;

                double t = 0;
                double prodOfHitProbabilities = 1;

                for (int n = difficulties.Count - 1; n >= 0; n--)
                {
                    double deltaTime = n > 0 ? times[n] - times[n - 1] : times[n];

                    prodOfHitProbabilities *= HitProbability(s, difficulties[n]);
                    t += deltaTime / prodOfHitProbabilities - deltaTime;
                }

                return t;
            }
        }

        public double DifficultyValueBinned()
        {
            double maxDiff = difficulties.Max();
            if (maxDiff <= 1e-10) return 0;

            var bins = Bin.CreateBins(difficulties, times, difficulty_bin_count, time_bin_count);

            const double lower_bound_estimate = 0;
            double upperBoundEstimate = 3.0 * maxDiff;

            double skill = RootFinding.FindRootExpand(
                skill => fcTime(skill) - time_threshold * 60000,
                lower_bound_estimate,
                upperBoundEstimate);

            return skill;

            double fcTime(double s)
            {
                if (s <= 0) return double.PositiveInfinity;

                double t = 0;
                double prodOfHitProbabilities = 1;

                for (int timeIndex = time_bin_count - 1; timeIndex >= 0; timeIndex--)
                {
                    double deltaTime = times.LastOrDefault() / time_bin_count;

                    for (int difficultyIndex = 0; difficultyIndex < difficulty_bin_count; difficultyIndex++)
                    {
                        Bin bin = bins[difficulty_bin_count * timeIndex + difficultyIndex];

                        prodOfHitProbabilities *= Math.Pow(HitProbability(s, bin.Difficulty), bin.Count);
                    }

                    t += deltaTime / prodOfHitProbabilities - deltaTime;
                }

                return t;
            }
        }

        public override double DifficultyValue()
        {
            if (difficulties.Count == 0) return 0;

            return difficulties.Count > 4 * difficulty_bin_count ? DifficultyValueBinned() : DifficultyValueExact();
        }
    }
}
