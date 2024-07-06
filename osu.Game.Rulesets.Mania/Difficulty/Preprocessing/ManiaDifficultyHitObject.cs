// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing
{
    public class ManiaDifficultyHitObject : DifficultyHitObject
    {
        private readonly List<DifficultyHitObject>[] perColumnDifficultyHitObjects;

        private int[] ColumnIndices;

        public new ManiaHitObject BaseObject => (ManiaHitObject)base.BaseObject;

        // Integer start and end values are easier to work with.
        public new int StartTime;
        public new int? EndTime;

        public int Column;

        public double GreatHitWindow;

        // Closest in time previous object in either the current or one-over left column.
        // Used for the cross column intensity calculation.
        public ManiaDifficultyHitObject? CrossColumnPreviousObject;

        // Previous and next long notes relative to the current object.
        // Prev can be the current note.
        public ManiaDifficultyHitObject? PrevLongNote;
        public ManiaDifficultyHitObject? NextLongNote;

        public ManiaDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, List<DifficultyHitObject>[] perColumnObjects, int index, int[] columnIndices)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            perColumnDifficultyHitObjects = perColumnObjects;
            ColumnIndices = columnIndices;

            StartTime = (int)BaseObject.StartTime;
            EndTime = BaseObject is HoldNote ? (int)BaseObject.GetEndTime() : null;
            Column = BaseObject.Column;

            GreatHitWindow = BaseObject is HoldNote ? BaseObject.NestedHitObjects[0].HitWindows.WindowFor(HitResult.Great) : BaseObject.HitWindows.WindowFor(HitResult.Great);

            if (index == 0)
                return;

            // The objects list is missing the first object, so we adjust for this.
            int listIndex = index - 1;

            List<DifficultyHitObject> crossColumnObjects = perColumnObjects[Column];

            // Merge this column with the column 1 over, then take the last note that doesn't occur simultaneously.
            if (Column > 0)
                crossColumnObjects = crossColumnObjects.Concat(perColumnObjects[Column - 1]).OrderBy(a => (int)Math.Round(a.StartTime)).ToList();

            CrossColumnPreviousObject = (ManiaDifficultyHitObject?)crossColumnObjects.FindLast(x => ((ManiaDifficultyHitObject)x).StartTime < StartTime);

            if (objects.Count > listIndex)
            {
                PrevLongNote = (ManiaDifficultyHitObject?)objects[..listIndex].LastOrDefault(x => x.BaseObject is HoldNote);
                NextLongNote = (ManiaDifficultyHitObject?)objects[listIndex..].Skip(1).FirstOrDefault(x => x.BaseObject is HoldNote);
            }
        }

        public DifficultyHitObject? PrevInColumn(int backwardsIndex)
        {
            int index = ColumnIndices[Column] - (backwardsIndex + 1);
            return index >= 0 && index < perColumnDifficultyHitObjects[Column].Count ? perColumnDifficultyHitObjects[Column][index] : default;
        }

        public DifficultyHitObject? NextInColumn(int forwardsIndex)
        {
            int index = ColumnIndices[Column] + (forwardsIndex + 1);
            return index >= 0 && index < perColumnDifficultyHitObjects[Column].Count ? perColumnDifficultyHitObjects[Column][index] : default;
        }
    }
}
