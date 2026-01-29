// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class PressingTest : ManiaStrainDecaySkill
    {
        protected override double StrainDecayBase => 0.15;

        private double currentChordDelta;
        private double chordAccumulator;

        public PressingTest(Mod[] mods)
            : base(mods)
        {
        }

        protected override double BaseDifficulty(ManiaDifficultyHitObject current)
        {
            // We want to divide chord difficulty by the deltaTime to the next note, so we accumulate it until we know what that is.
            if (current.HeadDeltaTime > 0)
            {
                currentChordDelta = current.HeadDeltaTime;
                chordAccumulator = PressingEvaluatorTest.EvaluateDifficultyOf(current);
            }
            else
            {
                chordAccumulator += PressingEvaluator.EvaluateChordDifficultyOf(current) * 1000.0 / currentChordDelta;
            }

            return chordAccumulator;
        }
    }
}
