// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class Rhythm : Skill
    {
        private const double fc_probability = 0.02;

        private double strainDecayBase => 0.3;

        private double currentStrain;

        private double skillMultiplier => 2.5;

        private readonly List<double> rhythmDifficulties = new List<double>();

        public Rhythm(Mod[] mods)
            : base(mods)
        {
        }

        private double fcProbabilityAtSkill(double skill) => Math.Exp(-getAverageMistapOccurencesAtSkill(skill));

        private double getAverageMistapOccurencesAtSkill(double skill)
        {
            if (skill <= 0) return rhythmDifficulties.Count;

            double occurences = 0;

            const double difficulty_exp = 2;

            foreach (double difficulty in rhythmDifficulties)
            {
                // An arbitrary formula to gauge rhythm scaling
                occurences += 1 - Math.Pow(0.9, Math.Pow(difficulty, difficulty_exp) / skill);
            }

            return occurences;
        }

        public override void Process(DifficultyHitObject current)
        {
            rhythmDifficulties.Add(strainValueAt(current));
        }

        public override double DifficultyValue()
        {
            double maxDiff = rhythmDifficulties.Max();

            if (fcProbabilityAtSkill(0) > fc_probability) return 0;

            const double lower_bound = 0;
            double upperBoundEstimate = 3.0 * maxDiff;

            double skill = Chandrupatla.FindRootExpand(
                skill => fcProbabilityAtSkill(skill) - fc_probability,
                lower_bound,
                upperBoundEstimate,
                accuracy: 1e-4);

            return skill;
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        private double strainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(((OsuDifficultyHitObject)current).StrainTime);
            currentStrain += (RhythmEvaluator.EvaluateDifficultyOf(current) - 1) * skillMultiplier;

            return currentStrain;
        }
    }
}
