// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Pressing : ManiaSkill
    {
        public Pressing(Mod[] mods)
            : base(mods) { }

        private double chordAccumulator;

        protected override double BaseDifficulty(ManiaDifficultyHitObject current)
        {
            var next = current.NextHead(0);

            if (next is null)
                return 0;

            // We want to divide chord difficulty by the deltaTime to the next note, so we accumulate it until we know what that is.
            if (next.HeadDeltaTime == 0)
            {
                chordAccumulator += PressingEvaluator.EvaluateChordDifficultyOf(current);

                return 0;
            }

            // We multiply chord difficulty by the portion of the smoothing window of this chord, so that it becomes a constant value when smoothed.
            double chordDifficulty = chordAccumulator * SmoothingWindowSize / Math.Min(SmoothingWindowSize, next.HeadDeltaTime);

            chordAccumulator = 0;

            double pressingDifficulty = PressingEvaluator.EvaluateDifficultyOf(current);

            return pressingDifficulty + chordDifficulty;
        }
    }
}
