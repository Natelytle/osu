// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class ReleaseFactor
    {
        public static double[] EvaluateReleaseFactor(List<ManiaDifficultyHitObject> noteList, double hitLeniency, double[] baseCorners, double[] allCorners)
        {
            double[] releaseFactor = new double[baseCorners.Length];
            int cornerPointer = 0;

            List<ManiaDifficultyHitObject> longNoteList = noteList.Where(obj => obj.BaseObject is HoldNote).OrderBy(obj => obj.EndTime).ToList();

            ManiaDifficultyHitObject? note = null;
            ManiaDifficultyHitObject? prev = null;

            foreach (ManiaDifficultyHitObject next in longNoteList)
            {
                if (prev is null || note is null || prev.EndTime >= note.EndTime)
                {
                    prev = note;
                    note = next;
                    continue;
                }

                double previousI = 0.001 * Math.Abs(prev.EndTime - prev.StartTime - 80.0) / hitLeniency;
                double currentI = 0.001 * Math.Abs(note.EndTime - note.StartTime - 80.0) / hitLeniency;
                double nextI = 0.001 * Math.Abs(next.StartTime - next.EndTime - 80.0) / hitLeniency;

                double prevHeadSpacingIndex = 2 / (2 + Math.Exp(-5 * (previousI - 0.75)) + Math.Exp(-5 * (currentI - 0.75)));
                double currHeadSpacingIndex = 2 / (2 + Math.Exp(-5 * (currentI - 0.75)) + Math.Exp(-5 * (nextI - 0.75)));

                double deltaR = 0.001 * (note.EndTime - prev.EndTime);

                // find the first corner at the start time of the previous note
                while (cornerPointer < baseCorners.Length && baseCorners[cornerPointer] < prev.StartTime) cornerPointer++;
                int firstCornerIndex = cornerPointer;

                // find the first corner at the start time of the previous note
                while (cornerPointer < baseCorners.Length && baseCorners[cornerPointer] < note.StartTime) cornerPointer++;
                int lastCornerIndex = cornerPointer;

                for (int i = firstCornerIndex; i < lastCornerIndex; i++)
                {
                    releaseFactor[i] = 0.08 * Math.Pow(deltaR, -1.0 / 2.0) * (1.0 / hitLeniency) * (1 + 0.8 * (prevHeadSpacingIndex + currHeadSpacingIndex));
                }

                prev = note;
                note = next;
            }

            releaseFactor = CornerUtils.SmoothCornersWithinWindow(baseCorners, releaseFactor, 500, 0.001);

            releaseFactor = CornerUtils.InterpolateValues(allCorners, baseCorners, releaseFactor);

            return releaseFactor;
        }
    }
}
