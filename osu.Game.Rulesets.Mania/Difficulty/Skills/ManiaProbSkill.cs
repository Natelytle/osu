// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public abstract class ManiaProbSkill : Skill
    {
        private const double mistap_multiplier = 3;
        private const double acc_multiplier = 200;
        private const double ss_prob = 0.02;

        private readonly List<double> difficulties = new List<double>();

        private readonly bool classicBehaviour;
        private readonly double[] hitWindows;

        private double skillToSS;

        protected ManiaProbSkill(Mod[] mods, double overallDifficulty)
            : base(mods)
        {
            classicBehaviour = mods.Any(m => m is ManiaModClassic);

            // Gotta add convert handling.
            hitWindows = classicBehaviour ? HitWindows.GetLegacyHitWindows(mods, false, overallDifficulty) : HitWindows.GetLazerHitWindows(mods, overallDifficulty);
        }

        private LogVal missProbOf(double difficulty, double skill, double hitWindow) => SpecialFunctions.Erfc(skill * hitWindow / (Math.Sqrt(2) * difficulty * acc_multiplier));

        private LogVal[] getJudgementProbsOf(double skill, double difficulty)
        {
            if (skill == 0) return [0.0, 0.0, 0.0, 0.0, 0.0, 1.0];
            if (difficulty == 0) return [1.0, 0.0, 0.0, 0.0, 0.0, 0.0];

            // Use an arbitrary formula to find the probability of mistapping the current note.
            // Mistaps are always assumed to result in misses. Subject to change.
            double mistapProb = 1 - Math.Tanh(skill / (difficulty * mistap_multiplier));

            LogVal prob320 = (1 - missProbOf(difficulty, skill, hitWindows[0])) * (1 - mistapProb);
            LogVal prob300 = (missProbOf(difficulty, skill, hitWindows[0]) - missProbOf(difficulty, skill, hitWindows[1])) * (1 - mistapProb);
            LogVal prob200 = (missProbOf(difficulty, skill, hitWindows[1]) - missProbOf(difficulty, skill, hitWindows[2])) * (1 - mistapProb);
            LogVal prob100 = (missProbOf(difficulty, skill, hitWindows[2]) - missProbOf(difficulty, skill, hitWindows[3])) * (1 - mistapProb);
            LogVal prob50 = (missProbOf(difficulty, skill, hitWindows[3]) - missProbOf(difficulty, skill, hitWindows[4])) * (1 - mistapProb);
            LogVal prob0 = missProbOf(difficulty, skill, hitWindows[4]);

            prob0 = prob0 + mistapProb - prob0 * mistapProb;

            return [prob320, prob300, prob200, prob100, prob50, prob0];
        }

        public override void Process(DifficultyHitObject current)
        {
            difficulties.Add(StrainValueAt(current));

            if (!classicBehaviour)
                difficulties.Add(StrainValueAt(current));
        }

        private double difficultyValueBinned()
        {
            double maxDiff = difficulties.Max();
            if (maxDiff <= 1e-10) return 0;

            var bins = Bin.CreateBins(difficulties);

            const double lower_bound = 0;
            double upperBoundEstimate = 3.0 * maxDiff;

            double skill = RootFinding.FindRootExpand(
                skill => fcProbability(skill) - ss_prob,
                lower_bound,
                upperBoundEstimate,
                accuracy: 1e-4);

            skillToSS = skill;

            return skillToSS;

            double fcProbability(double s)
            {
                if (s <= 0) return 0;

                LogVal fcProb = bins.Aggregate(new LogVal(1), (current, bin) => current * LogVal.Pow(getJudgementProbsOf(s, bin.Difficulty)[0], bin.Count));

                return fcProb.TrueValue;
            }
        }

        private double difficultyValueExact()
        {
            double maxDiff = difficulties.Max();
            if (maxDiff <= 1e-10) return 0;

            const double lower_bound = 0;
            double upperBoundEstimate = 3.0 * maxDiff;

            double skill = RootFinding.FindRootExpand(
                skill => fcProbability(skill) - ss_prob,
                lower_bound,
                upperBoundEstimate,
                accuracy: 1e-4);

            skillToSS = skill;

            return skillToSS;

            double fcProbability(double s)
            {
                if (s <= 0) return 0;

                LogVal fcProb = difficulties.Aggregate(new LogVal(1), (current, d) => current * getJudgementProbsOf(s, d)[0]);

                return fcProb.TrueValue;
            }
        }

        public override double DifficultyValue()
        {
            if (difficulties.Count == 0)
                return 0;

            skillToSS = difficulties.Count < 64 ? difficultyValueExact() : difficultyValueBinned();

            return skillToSS;
        }

        /// <summary>
        /// Find the lowest misscount that a player with the provided <paramref name="skill"/> would have a 2% chance of achieving.
        /// </summary>
        public double GetJudgementCountAtSkill(double skill, int judgementId)
        {
            double maxDiff = difficulties.Max();

            if (maxDiff == 0)
                return 0;

            if (skill <= 0)
            {
                if (judgementId == 5)
                    return difficulties.Count;

                return 0;
            }

            PoissonBinomial poiBin;

            if (difficulties.Count > 64)
            {
                var bins = Bin.CreateBins(difficulties);
                poiBin = new PoissonBinomial(bins, skill, (s, d) => getJudgementProbsOf(s, d)[judgementId]);
            }
            else
            {
                poiBin = new PoissonBinomial(difficulties, skill, (s, d) => getJudgementProbsOf(s, d)[judgementId]);
            }

            double count = Math.Max(0, RootFinding.FindRootExpand(x => poiBin.CDF(x) - ss_prob, -50, 1000, accuracy: 1e-4));

            return count;
        }

        protected abstract double StrainValueAt(DifficultyHitObject current);

        protected double GetSkillToSS() => skillToSS == 0 ? DifficultyValue() : skillToSS;
    }
}
