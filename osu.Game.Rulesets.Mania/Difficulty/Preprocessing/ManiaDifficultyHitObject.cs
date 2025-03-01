// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing
{
    public class ManiaDifficultyHitObject : DifficultyHitObject
    {
        public new ManiaHitObject BaseObject => (ManiaHitObject)base.BaseObject;

        private readonly List<DifficultyHitObject>[] perColumnObjects;

        private readonly int columnIndex;

        // The number of long notes before this note.
        public readonly int LongNoteIndex;

        public ManiaDifficultyHitObject?[] AllCurrentHitObjects => ConcurrentHitObjects.Concat(PreviousHitObjects).ToArray();

        // The hit object earlier in time than this note in each column
        public readonly ManiaDifficultyHitObject?[] PreviousHitObjects;

        // Every concurrent note, aka chord notes.
        public ManiaDifficultyHitObject?[] ConcurrentHitObjects { get; }

        public int Column;

        // Previous and next long notes relative to the current object.
        // Prev can be the current note.
        public readonly ManiaDifficultyHitObject? PrevLongNote;
        public ManiaDifficultyHitObject? NextLongNote;

        public ManiaDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, List<DifficultyHitObject>[] perColumnObjects, int index, int longNoteIndex)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            int totalColumns = perColumnObjects.Length;
            this.perColumnObjects = perColumnObjects;
            Column = BaseObject.Column;
            columnIndex = this.perColumnObjects[Column].Count;
            LongNoteIndex = longNoteIndex;
            PreviousHitObjects = new ManiaDifficultyHitObject[totalColumns];
            ConcurrentHitObjects = new ManiaDifficultyHitObject[totalColumns];

            if (index > 0)
            {
                var prevNote = (ManiaDifficultyHitObject)objects[index - 1];

                PrevLongNote = BaseObject is HeadNote ? this : prevNote.PrevLongNote;

                PreviousHitObjects = prevNote.PreviousHitObjects;

                if (prevNote.StartTime < StartTime)
                {
                    PreviousHitObjects[prevNote.Column] = prevNote;
                }
            }
        }

        public ManiaDifficultyHitObject? PrevInColumn(int backwardsIndex)
        {
            int index = columnIndex - (backwardsIndex + 1);
            return index >= 0 && index < perColumnObjects[Column].Count ? (ManiaDifficultyHitObject)perColumnObjects[Column][index] : null;
        }

        public ManiaDifficultyHitObject? NextInColumn(int forwardsIndex)
        {
            int index = columnIndex + (forwardsIndex + 1);
            return index >= 0 && index < perColumnObjects[Column].Count ? (ManiaDifficultyHitObject)perColumnObjects[Column][index] : null;
        }
    }
}
