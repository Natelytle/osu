
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.RootFinding;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using static MathNet.Numerics.SpecialFunctions;

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

        private double skillMultiplier => 125;

        // Assume players spend 20 minutes retrying a map before they FC
        private const double time_threshold = 20;

        private double currentStrain;

        private double strainDecayBase => 0.15;

        private readonly List<double> difficulties = new List<double>();
        private readonly List<double> deltaTimes = new List<double>();

        private static double hitProbability(double skill, double difficulty) => Erf(skill / (Math.Sqrt(2) * difficulty));

        public override void Process(DifficultyHitObject current)
        {
            difficulties.Add(strainValueAt(current));
            // Cap deltatimes at 5 seconds to not reward breaks
            deltaTimes.Add(Math.Min(current.DeltaTime, 5000));
        }

        private double fcTime(double skill)
        {
            if (skill <= 0) return double.PositiveInfinity;

            double t = 0;
            double prodOfHitProbabilities = 1;

            for (int n = difficulties.Count - 1; n >= 0; n--)
            {
                prodOfHitProbabilities *= hitProbability(skill, difficulties[n]);
                t += deltaTimes[n] / prodOfHitProbabilities - deltaTimes[n];
            }

            return t;
        }

        public override double DifficultyValue()
        {
            double maxDiff = difficulties.Max();
            if (maxDiff <= 1e-10) return 0;

            double lowerBoundEstimate = 0.5 * maxDiff;
            double upperBoundEstimate = 3.0 * maxDiff;

            double skill = Brent.FindRootExpand(
                skill => fcTime(skill) - time_threshold * 60000,
                lowerBoundEstimate,
                upperBoundEstimate,
                1e-4);

            return skill;
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        private double strainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            currentStrain += AimEvaluator.EvaluateDifficultyOf(current, withSliders) * skillMultiplier;

            return currentStrain;
        }
    }
}
