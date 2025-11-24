// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class ManiaSkill : Skill
    {
        private const double skillset_count = 4.0;

        private ChordjackEvaluator? chordjackEvaluator;
        private SpeedjackEvaluator? speedjackEvaluator;
        private SpeedstreamEvaluator? speedstreamEvaluator;
        private ChordstreamEvaluator? chordstreamEvaluator;

        private double chordjackPeak;
        private double speedjackPeak;
        private double speedstreamPeak;
        private double chordstreamPeak;

        public ManiaSkill(Mod[] mods)
            : base(mods)
        {
        }

        public override double DifficultyValue()
        {
            double combined =
                Math.Pow(chordjackPeak, skillset_count) +
                Math.Pow(speedstreamPeak, skillset_count) +
                Math.Pow(chordstreamPeak, skillset_count) +
                Math.Pow(speedjackPeak, skillset_count);

            if (combined <= 0)
                return 0;

            return Math.Pow(combined, 1.0 / skillset_count);
        }

        public override void Process(DifficultyHitObject current)
        {
            var maniaObject = (ManiaDifficultyHitObject)current;

            chordjackEvaluator ??= new ChordjackEvaluator(maniaObject);
            speedjackEvaluator ??= new SpeedjackEvaluator(maniaObject);
            speedstreamEvaluator ??= new SpeedstreamEvaluator(maniaObject);
            chordstreamEvaluator ??= new ChordstreamEvaluator(maniaObject);

            chordjackPeak = Math.Max(chordjackPeak, chordjackEvaluator.EvaluateDifficultyOf(maniaObject));
            speedjackPeak = Math.Max(speedjackPeak, speedjackEvaluator.EvaluateDifficultyOf(maniaObject));
            speedstreamPeak = Math.Max(speedstreamPeak, speedstreamEvaluator.EvaluateDifficultyOf(maniaObject));
            chordstreamPeak = Math.Max(chordstreamPeak, chordstreamEvaluator.EvaluateDifficultyOf(maniaObject));
        }
    }
}
