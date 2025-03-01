// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class SameColumnEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            var maniaCurrObj = (ManiaDifficultyHitObject)current;

            var maniaPrevObj = maniaCurrObj.PrevInColumn(0);

            if (maniaPrevObj?.BaseObject is TailNote)
                maniaPrevObj = maniaPrevObj.PrevInColumn(0);

            if (maniaPrevObj is null)
                return 0;

            double deltaTime = Math.Max(maniaCurrObj.StartTime - maniaPrevObj.StartTime, 25);

            return 1000 / deltaTime;
        }
    }
}
