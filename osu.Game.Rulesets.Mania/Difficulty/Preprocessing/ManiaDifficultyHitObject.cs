// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
        public ManiaDifficultyHitObject Head { get; private set; }

        public ManiaDifficultyHitObject? Tail { get; private set; }

        public new readonly double StartTime;
        public new readonly double EndTime;
        public readonly double ActualTime;

        /// <summary>
        /// The time difference to the last processed head note in any other column.
        /// </summary>
        public readonly double HeadDeltaTime;

        /// <summary>
        /// The time difference to the last processed head note in this column, clamped to 25ms.
        /// </summary>
        public readonly double ColumnHeadStrainTime;

        public new ManiaHitObject BaseObject => (ManiaHitObject)base.BaseObject;

        public readonly int Column;
        private readonly int headObjectIndex;
        private readonly int tailObjectIndex;
        private readonly int columnHeadIndex;
        private readonly int columnTailIndex;

        // Lists of head and tail objects to make object-type specific traversal easier.
        private readonly List<ManiaDifficultyHitObject> headObjects;
        private readonly List<ManiaDifficultyHitObject> tailObjects;
        private readonly List<ManiaDifficultyHitObject>[] perColumnHeadObjects;
        private readonly List<ManiaDifficultyHitObject>[] perColumnTailObjects;

        /// <summary>
        /// The hit object earlier in time than this note in each column.
        /// </summary>
        public readonly ManiaDifficultyHitObject?[] PreviousHitObjects;

        public readonly double GreatHitWindow;

        public ManiaDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects,
                                        List<ManiaDifficultyHitObject> headObjects, List<ManiaDifficultyHitObject> tailObjects,
                                        List<ManiaDifficultyHitObject>[] perColumnHeadObjects, List<ManiaDifficultyHitObject>[] perColumnTailObjects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            int totalColumns = perColumnHeadObjects.Length;
            this.headObjects = headObjects;
            this.tailObjects = tailObjects;
            this.perColumnHeadObjects = perColumnHeadObjects;
            this.perColumnTailObjects = perColumnTailObjects;
            Column = BaseObject.Column;
            headObjectIndex = headObjects.Count;
            tailObjectIndex = tailObjects.Count;
            columnHeadIndex = perColumnHeadObjects[Column].Count;
            columnTailIndex = perColumnTailObjects[Column].Count;
            PreviousHitObjects = new ManiaDifficultyHitObject[totalColumns];
            GreatHitWindow = BaseObject.HitWindows.WindowFor(HitResult.Great);

            // Add a reference to the related head/tail for long notes.
            if (BaseObject is TailNote)
            {
                Tail = this;

                // We process forward, so we need to set the tail value for the previous head while we process the tail for it.
                // Can technically be null but setting the head to this in that case should be harmless.
                Head = perColumnHeadObjects[Column].LastOrDefault() ?? this;
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

            HeadDeltaTime = StartTime - PrevHead(0)?.StartTime ?? StartTime;
            ColumnHeadStrainTime = StartTime - PrevHeadInColumn(0)?.StartTime ?? StartTime;

            ManiaDifficultyHitObject? prevHeadObj = PrevHead(0);

            if (prevHeadObj is not null)
            {
                for (int i = 0; i < prevHeadObj.PreviousHitObjects.Length; i++)
                    PreviousHitObjects[i] = prevHeadObj.PreviousHitObjects[i];

                // intentionally depends on processing order to match live.
                PreviousHitObjects[prevHeadObj.Column] = prevHeadObj;
            }
        }

        public ManiaDifficultyHitObject? PrevHead(int backwardsIndex) => getNoteInList(headObjects, headObjectIndex, -(backwardsIndex + 1));
        public ManiaDifficultyHitObject? NextHead(int forwardsIndex) => getNoteInList(headObjects, headObjectIndex, forwardsIndex + 1);

        public ManiaDifficultyHitObject? PrevTail(int backwardsIndex) => getNoteInList(tailObjects, tailObjectIndex, -(backwardsIndex + 1));
        public ManiaDifficultyHitObject? NextTail(int forwardsIndex) => getNoteInList(tailObjects, tailObjectIndex, forwardsIndex + 1);

        public ManiaDifficultyHitObject? PrevHeadInColumn(int backwardsIndex) => getNoteInList(perColumnHeadObjects[Column], columnHeadIndex, -(backwardsIndex + 1));
        public ManiaDifficultyHitObject? NextHeadInColumn(int forwardsIndex) => getNoteInList(perColumnHeadObjects[Column], columnHeadIndex, forwardsIndex + 1);

        public ManiaDifficultyHitObject? PrevTailInColumn(int backwardsIndex) => getNoteInList(perColumnTailObjects[Column], columnTailIndex, -(backwardsIndex + 1));
        public ManiaDifficultyHitObject? NextTailInColumn(int forwardsIndex) => getNoteInList(perColumnTailObjects[Column], columnTailIndex, forwardsIndex + 1);

        private ManiaDifficultyHitObject? getNoteInList(List<ManiaDifficultyHitObject> list, int currentIndex, int offset)
        {
            int targetIndex = currentIndex + offset;
            return (targetIndex >= 0 && targetIndex < list.Count) ? list[targetIndex] : null;
        }
    }
}
