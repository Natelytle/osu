// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class ReleaseFactor
    {
        public static double[] EvaluateReleaseFactor(List<ManiaDifficultyHitObject> noteList, int totalColumns, int mapLength, double hitLeniency)
        {
            double[] releaseFactor = new double[mapLength];

            List<ManiaDifficultyHitObject> longNoteList = noteList.Where(obj => obj.BaseObject is HoldNote).OrderBy(obj => obj.EndTime).ToList();

            ManiaDifficultyHitObject? curr = null;
            ManiaDifficultyHitObject? prev = null;

            foreach (ManiaDifficultyHitObject next in longNoteList)
            {
                if (prev is null || curr is null || prev.EndTime >= curr.EndTime)
                    continue;

                double previousI = 0.001 * Math.Abs(prev.EndTime - prev.StartTime - 80.0) / hitLeniency;
                double currentI = 0.001 * Math.Abs(curr.EndTime - curr.StartTime - 80.0) / hitLeniency;
                double nextI = 0.001 * Math.Abs(next.StartTime - next.EndTime - 80.0) / hitLeniency;

                double prevHeadSpacingIndex = 2 / (2 + Math.Exp(-5 * (previousI - 0.75)) + Math.Exp(-5 * (currentI - 0.75)));
                double currHeadSpacingIndex = 2 / (2 + Math.Exp(-5 * (currentI - 0.75)) + Math.Exp(-5 * (nextI - 0.75)));

                double deltaR = 0.001 * (curr.EndTime - prev.EndTime);

                for (int t = (int)prev.EndTime; t < curr.EndTime; t++)
                {
                    releaseFactor[t] = 0.08 * Math.Pow(deltaR, -1.0 / 2.0) * (1.0 / hitLeniency) * (1 + SunnySkill.LAMBDA_4 * (prevHeadSpacingIndex + currHeadSpacingIndex));
                }

                prev = curr;
                curr = next;
            }

            releaseFactor = ListUtils.ApplySymmetricMovingAverage(releaseFactor, 500);

            return releaseFactor;
        }
    }
}
