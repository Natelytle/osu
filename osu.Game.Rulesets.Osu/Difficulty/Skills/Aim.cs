﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using MathNet.Numerics.RootFinding;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : Skill
    {
        public Aim(Mod[] mods, bool withSliders)
            : base(mods)
        {
            this.withSliders = withSliders;
        }

        private readonly bool withSliders;

        private const double prior_strain_multiplier = 0.5;
        private const double current_strain_multiplier = 200;

        private const double fc_probability = 0.02;
        private const int bin_count = 32;

        private double priorStrain;

        private double strainDecayBase => 0.15;

        private readonly List<double> difficulties = new List<double>();

        private static double hitProbability(double skill, double difficulty) => SpecialFunctions.Erf(skill / (Math.Sqrt(2) * difficulty));

        private struct Bin
        {
            public double Difficulty;
            public double Count;

            public double FcProbability(double skill)
            {
                return Math.Pow(hitProbability(skill, Difficulty), Count);
            }
        }

        private static double fcProbability(double skill, IEnumerable<Bin> bins)
        {
            if (skill <= 0) return 0;

            return bins.Aggregate(1.0, (current, bin) => current * bin.FcProbability(skill));
        }

        /// <summary>
        /// Create an array of equally spaced bins. Count is linearly interpolated into each bin.
        /// For example, if we have bins with values [1,2,3,4,5] and want to insert the value 3.2,
        /// we will add 0.8 to the count of 3's and 0.2 to the count of 4's
        /// </summary>
        private Bin[] createBins(double maxDifficulty)
        {
            var bins = new Bin[bin_count];

            for (int i = 0; i < bin_count; i++)
            {
                bins[i].Difficulty = maxDifficulty * (i + 1) / bin_count;
            }

            foreach (double d in difficulties)
            {
                double binIndex = bin_count * (d / maxDifficulty) - 1;

                int lowerBound = (int)Math.Floor(binIndex);
                double t = binIndex - lowerBound;

                //This can be -1, corresponding to the zero difficulty bucket.
                //We don't store that since it doesn't contribute to difficulty
                if (lowerBound >= 0)
                {
                    bins[lowerBound].Count += (1 - t);
                }

                int upperBound = lowerBound + 1;

                // this can be == bin_count for the maximum difficulty object, in which case t will be 0 anyway
                if (upperBound < bin_count)
                {
                    bins[upperBound].Count += t;
                }
            }

            return bins;
        }

        private double difficultyValueBinned()
        {
            double maxDiff = difficulties.Max();
            if (maxDiff <= 1e-10) return 0;

            var bins = createBins(maxDiff);

            double lowerBoundEstimate = 0.5 * maxDiff;
            double upperBoundEstimate = 3.0 * maxDiff;

            double skill = Brent.FindRootExpand(
                skill => fcProbability(skill, bins) - fc_probability,
                lowerBoundEstimate,
                upperBoundEstimate,
                1e-4);

            return skill;
        }

        public override void Process(DifficultyHitObject current)
        {
            difficulties.Add(strainValueAt(current));
        }

        private double fcProbability(double skill)
        {
            if (skill <= 0) return 0;

            return difficulties.Aggregate<double, double>(1, (current, d) => current * hitProbability(skill, d));
        }

        private double difficultyValueExact()
        {
            double maxDiff = difficulties.Max();
            if (maxDiff <= 1e-10) return 0;

            double lowerBoundEstimate = 0.5 * maxDiff;
            double upperBoundEstimate = 3.0 * maxDiff;

            double skill = Brent.FindRootExpand(
                skill => fcProbability(skill) - fc_probability,
                lowerBoundEstimate,
                upperBoundEstimate,
                1e-4);

            return skill;
        }

        public override double DifficultyValue()
        {
            if (difficulties.Count == 0)
                return 0;

            return difficulties.Count < 2 * bin_count ? difficultyValueExact() : difficultyValueBinned();
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        private double strainValueAt(DifficultyHitObject current)
        {
            priorStrain *= strainDecay(current.DeltaTime);

            double currentObjectDifficulty = AimEvaluator.EvaluateDifficultyOf(current, withSliders) * current_strain_multiplier;
            double totalDifficulty = priorStrain * prior_strain_multiplier + currentObjectDifficulty;

            priorStrain += currentObjectDifficulty;

            return totalDifficulty;
        }
    }
}
