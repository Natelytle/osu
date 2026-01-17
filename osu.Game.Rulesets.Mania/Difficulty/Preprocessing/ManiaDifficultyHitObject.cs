// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing
{
    public class ManiaDifficultyHitObject : DifficultyHitObject
    {
        public ManiaDifficultyHitObject Head { get; private set; }

        public ManiaDifficultyHitObject? Tail { get; private set; }

        public new readonly double StartTime;
        public new readonly double EndTime;
        public readonly double ActualTime;

        /// <summary>
        /// The time difference to the last processed head note in any other column.
        /// </summary>
        public readonly double HeadDeltaTime = double.PositiveInfinity;

        public new ManiaHitObject BaseObject => (ManiaHitObject)base.BaseObject;

        public readonly int Column;
        private readonly int columnHeadIndex;
        private readonly int columnTailIndex;

        private readonly List<ManiaDifficultyHitObject>[] perColumnHeadObjects;
        private readonly List<ManiaDifficultyHitObject>[] perColumnTailObjects;

        /// <summary>
        /// The hit object earlier in time than this note in each column.
        /// </summary>
        public readonly ManiaDifficultyHitObject?[] PreviousHitObjects;

        public readonly double ColumnStrainTime;

        public ManiaDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, List<ManiaDifficultyHitObject>[] perColumnHeadObjects, List<ManiaDifficultyHitObject>[] perColumnTailObjects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            int totalColumns = perColumnHeadObjects.Length;
            this.perColumnHeadObjects = perColumnHeadObjects;
            this.perColumnTailObjects = perColumnTailObjects;
            Column = BaseObject.Column;
            columnHeadIndex = perColumnHeadObjects[Column].Count;
            columnTailIndex = perColumnTailObjects[Column].Count;
            PreviousHitObjects = new ManiaDifficultyHitObject[totalColumns];

            // Add a reference to the related head/tail for long notes.
            if (BaseObject is TailNote)
            {
                Tail = this;

                // We process forward, so we need to set the tail value for the previous head while we process the tail for it.
                Head = perColumnHeadObjects[Column].Last();
                Head.Tail = this;
            }
            else
            {
                Head = this;
            }

            // Actual time is when the nested hit object takes place
            ActualTime = base.StartTime;
            StartTime = Head.ActualTime;
            EndTime = Tail?.ActualTime ?? Head.ActualTime;

            ColumnStrainTime = StartTime - PrevHeadInColumn(0)?.StartTime ?? StartTime;

            foreach (List<ManiaDifficultyHitObject> column in perColumnHeadObjects)
            {
                // Intentionally depends on note processing order, since we want the first processed note in a chord to have a HeadDeltaTime above zero.
                HeadDeltaTime = Math.Min(HeadDeltaTime, column.LastOrDefault()?.StartTime - StartTime ?? StartTime);
            }

            if (index > 0)
            {
                ManiaDifficultyHitObject prevNote = (ManiaDifficultyHitObject)objects[index - 1];

                for (int i = 0; i < prevNote.PreviousHitObjects.Length; i++)
                    PreviousHitObjects[i] = prevNote.PreviousHitObjects[i];

                // intentionally depends on processing order to match live.
                PreviousHitObjects[prevNote.Column] = prevNote;
            }
        }

        public ManiaDifficultyHitObject? PrevHeadInColumn(int backwardsIndex) => getNoteInColumn(perColumnHeadObjects[Column], columnHeadIndex, -(backwardsIndex + 1));
        public ManiaDifficultyHitObject? NextHeadInColumn(int forwardsIndex) => getNoteInColumn(perColumnHeadObjects[Column], columnHeadIndex, forwardsIndex + 1);

        public ManiaDifficultyHitObject? PrevTailInColumn(int backwardsIndex) => getNoteInColumn(perColumnTailObjects[Column], columnTailIndex, -(backwardsIndex + 1));
        public ManiaDifficultyHitObject? NextTailInColumn(int forwardsIndex) => getNoteInColumn(perColumnTailObjects[Column], columnTailIndex, forwardsIndex + 1);

        private ManiaDifficultyHitObject? getNoteInColumn(List<ManiaDifficultyHitObject> list, int currentIndex, int offset)
        {
            int targetIndex = currentIndex + offset;
            return (targetIndex >= 0 && targetIndex < list.Count) ? list[targetIndex] : null;
        }
    }
}
