// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators.Aim;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : Skill
    {
        public readonly bool IncludeSliders;

        private readonly List<double> snapProbs = new List<double>();
        private readonly List<double> deltas = new List<double>();

        public Aim(Mod[] mods, bool includeSliders)
            : base(mods)
        {
            IncludeSliders = includeSliders;
        }

        private double currentStrain;

        private readonly List<double> sliderStrains = new List<double>();

        private double strainDecay(double ms) => DiffUtils.Pow(0.2, ms / 1000);

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            (double strain, double _, double _, double pSnap) = StrainValueAt(current);

            snapProbs.Add(pSnap);
            deltas.Add(current.DeltaTime);

            return strain;
        }

        protected (double strain, double snap, double flow, double pSnap) StrainValueAt(DifficultyHitObject current)
        {
            if (Mods.Any(m => m is OsuModAutopilot))
                return (0, 0, 0, 0);

            double decay = strainDecay(((OsuDifficultyHitObject)current).AdjustedDeltaTime);

            (double total, double snap, double flow, double pSnap) = calculateAdjustedDifficulty(current);

            currentStrain *= decay;
            currentStrain += total * (1 - decay);

            if (current.BaseObject is Slider)
                sliderStrains.Add(currentStrain);

            return (currentStrain, snap, flow, pSnap);
        }

        private (double total, double snap, double flow, double pSnap) calculateAdjustedDifficulty(DifficultyHitObject current)
        {
            const double skill_multiplier_snap = 71.0;
            const double skill_multiplier_agility = 1.63;
            const double skill_multiplier_flow = 247.0;

            double snapDifficulty = SnapAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders) * skill_multiplier_snap;
            double agilityDifficulty = AgilityEvaluator.EvaluateDifficultyOf(current) * skill_multiplier_agility;
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders) * skill_multiplier_flow;

            (double snap, double flow, double pSnap) = calculateTotalValue(snapDifficulty, agilityDifficulty, flowDifficulty);

            double total = snap * pSnap + flow * (1 - pSnap);

            if (Mods.Any(m => m is OsuModMagnetised))
            {
                float magnetisedStrength = Mods.OfType<OsuModMagnetised>().First().AttractionStrength.Value;
                double mult = 1.0 - magnetisedStrength;

                total *= mult;
                snap *= mult;
                flow *= mult;
            }

            double odMult = DiffUtils.Pow(Math.Max(0, ((OsuDifficultyHitObject)current).OverallDifficulty), 2) / 4000;
            total *= odMult;
            snap *= odMult;
            flow *= odMult;

            return (total, snap, flow, pSnap);
        }

        private (double snap, double flow, double pSnap) calculateTotalValue(double snapDifficulty, double agilityDifficulty, double flowDifficulty)
        {
            const double skill_multiplier_total = 1.12;
            const double combined_snap_norm_exponent = 1.2;

            // We compare flow to combined snap and agility because snap by itself doesn't have enough difficulty to be above flow on streams
            // Agility on the other hand is supposed to measure the rate of cursor velocity changes while snapping
            // So snapping every circle on a stream requires an enormous amount of agility at which point it's easier to flow
            double combinedSnapDifficulty = DiffUtils.Norm(combined_snap_norm_exponent, snapDifficulty, agilityDifficulty);

            double pSnap = calculateSnapFlowProbability(flowDifficulty / combinedSnapDifficulty);

            if (Mods.Any(m => m is OsuModTouchDevice))
            {
                // we don't adjust agility here since agility represents TD difficulty in a decent enough way
                snapDifficulty = DiffUtils.Pow(snapDifficulty, 0.89);
                combinedSnapDifficulty = DiffUtils.Norm(combined_snap_norm_exponent, snapDifficulty, agilityDifficulty);
            }

            if (Mods.Any(m => m is OsuModRelax))
            {
                combinedSnapDifficulty *= 0.75;
                flowDifficulty *= 0.6;
            }

            return (combinedSnapDifficulty * skill_multiplier_total, flowDifficulty * skill_multiplier_total, pSnap);
        }

        // A function that turns the ratio of snap : flow into the probability of snapping/flowing
        // It has the constraints:
        // P(snap) + P(flow) = 1 (the object is always either snapped or flowed)
        // P(snap) = f(snap/flow), P(flow) = f(flow/snap) (ie snap and flow are symmetric and reversible)
        // Therefore: f(x) + f(1/x) = 1
        // 0 <= f(x) <= 1 (cannot have negative or greater than 100% probability of snapping or flowing)
        // This logistic function is a solution, which fits nicely with the general idea of interpolation and provides a tuneable constant
        private static double calculateSnapFlowProbability(double ratio)
        {
            const double k = 7.27;

            if (ratio == 0)
                return 0;

            if (double.IsNaN(ratio))
                return 1;

            return DiffUtils.Logistic(-k * Math.Log(ratio));
        }

        public double GetDifficultSliders()
        {
            if (sliderStrains.Count == 0)
                return 0;

            double maxSliderStrain = sliderStrains.Max();

            if (maxSliderStrain == 0)
                return 0;

            return sliderStrains.Sum(strain => DiffUtils.Logistic(strain / maxSliderStrain, 0.5, 12.0));
        }

        public double CountTopWeightedSliders(double difficultyValue)
        {
            if (sliderStrains.Count == 0)
                return 0;

            double consistentTopStrain = difficultyValue * (1 - 0.9); // What would the top strain be if all strain values were identical

            if (consistentTopStrain == 0)
                return 0;

            // Use a weighted sum of all strains. Constants are arbitrary and give nice values
            return sliderStrains.Sum(s => DiffUtils.Logistic(s / consistentTopStrain, 0.88, 10, 1.1));
        }

        public override double DifficultyValue()
        {
            if (ObjectDifficulties.Max() == 0)
                return 0;

            double skill = RootFinding.FindRootExpand(skill => fcProbAtSkill(skill) - 0.02, 0, 10);

            return skill * 150;
        }

        private double fcProbAtSkill(double skill)
        {
            if (skill <= 0)
                return 0;

            List<ConditionalProbability> conditionalProbabilities = new List<ConditionalProbability>();

            for (int i = 0; i < ObjectDifficulties.Count; i++)
            {
                double diffPrev = i == 0 ? 0 : ObjectDifficulties[i - 1];
                double diff = ObjectDifficulties[i];

                double snapProb = snapProbs[i];

                double snapHitProb = DiffUtils.Erf(skill / (Math.Sqrt(2) * diff));
                ConditionalProbability flowHitProb = getFlowProbOf(skill, diff, diffPrev, deltas[i]);

                conditionalProbabilities.Add(new ConditionalProbability
                {
                    PGivenSuccess = snapHitProb * snapProb + flowHitProb.PGivenSuccess * (1 - snapProb),
                    PGivenFail = snapHitProb * snapProb + flowHitProb.PGivenFail * (1 - snapProb)
                });
            }

            return DependentProbabilityUtils.TotalProbabilitiesOf(conditionalProbabilities).Aggregate(1.0, (prod, x) => prod * x);
        }

        private ConditionalProbability getFlowProbOf(double skill, double d, double dPrev, double delta)
        {
            const double theta = 100.0;

            double rho = Math.Exp(-(delta / theta));
            double a = skill / dPrev, b = skill / d;

            double j = rectProb(a, b, rho);
            double pPrev = Math.Clamp(DiffUtils.Erf(a / Math.Sqrt(2)), 0.0000001, 0.9999999);
            double pCurr = DiffUtils.Erf(b / Math.Sqrt(2));

            return new ConditionalProbability
            {
                PGivenSuccess = Math.Clamp(j / pPrev, 0, 1),
                PGivenFail = (pCurr - j) / (1 - pPrev)
            };
        }

        private static double rectProb(double a, double b, double rho)
        {
            // Zero-difficulty note => infinite window => that note is always hit.
            bool aInf = double.IsPositiveInfinity(a);
            bool bInf = double.IsPositiveInfinity(b);

            if (aInf && bInf) return 1.0;

            if (aInf) return 2 * DiffUtils.NormalCdf(0, 1, b) - 1; // only note 2 constrains
            if (bInf) return 2 * DiffUtils.NormalCdf(0, 1, a) - 1; // only note 1 constrains

            if (rho >= 1.0)
                return 2 * DiffUtils.NormalCdf(0, 1, Math.Min(a, b)) - 1;

            double s = Math.Sqrt(1 - rho * rho);

            // P(note 2 hit | note 1 error = u)
            double inner(double u) => DiffUtils.NormalCdf(0, 1, (b - rho * u) / s) - DiffUtils.NormalCdf(0, 1, (-b - rho * u) / s);
            double integrand(double u) => DiffUtils.NormalPdf(0, 1, u) * inner(u);

            // Split where the inner term changes sharply, so the integrator behaves
            double k = b / rho;
            double[] cuts = { -a, Math.Max(-a, Math.Min(a, -k)), Math.Max(-a, Math.Min(a, k)), a };

            double total = 0;
            for (int i = 0; i < cuts.Length - 1; i++)
                total += simpson(integrand, cuts[i], cuts[i + 1], 200);

            return total;
        }

        private static double simpson(Func<double, double> f, double lo, double hi, int n)
        {
            if (hi <= lo)
                return 0;

            double h = (hi - lo) / n, sum = f(lo) + f(hi);
            for (int i = 1; i < n; i++)
                sum += f(lo + i * h) * (i % 2 == 0 ? 2 : 4);

            return sum * h / 3;
        }
    }
}
