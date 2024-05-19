// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.BaseSkills;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class AccuracyNoteProbabilities : NoteProbabilityBaseSkill
    {
        public AccuracyNoteProbabilities(Mod[] mods)
            : base(mods)
        {
        }

        private readonly List<double> h300List = new List<double>();
        private readonly List<double> h50List = new List<double>();

        public override void Process(DifficultyHitObject current)
        {
            Difficulties.Add(DifficultyValueOf(current));
            h300List.Add(current.BaseObject.HitWindows.WindowFor(HitResult.Great));
            h50List.Add(current.BaseObject.HitWindows.WindowFor(HitResult.Meh));
        }

        protected override double DifficultyValueOf(DifficultyHitObject current)
        {
            // Should come from a rhythm skill
            return 0;
        }

        private JudgementProbabilities judgementProbabilities(double skill, double difficulty, double h300, double h50)
        {
            // Arbitrary, should change with balancing.
            double noteDeviationMultiplier = Math.Pow(1.25, difficulty);
            double deviation = 10 * noteDeviationMultiplier / Math.Sqrt(skill);

            double p300 = SpecialFunctions.Erf(h300 / (Math.Sqrt(2) * deviation));
            double p100 = SpecialFunctions.Erf(h50 / (Math.Sqrt(2) * deviation)) - p300;
            double pMiss = 1 - p300 - p100;

            return new JudgementProbabilities(p300, p100, pMiss);
        }

        public override List<JudgementProbabilities> JudgementProbabilitiesList(double skill) => Difficulties.Select((d, i) => judgementProbabilities(skill, d, h300List[i], h50List[i])).ToList();
    }
}
