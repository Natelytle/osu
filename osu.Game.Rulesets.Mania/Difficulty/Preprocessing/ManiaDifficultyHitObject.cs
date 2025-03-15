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
        private readonly List<DifficultyHitObject>[] perColumnNestedObjects;

        private readonly int columnIndex;
        private readonly int nestedColumnIndex;

        // The number of long notes before this note.
        public readonly int LongNoteIndex;

        public new readonly double StartTime;
        public new double EndTime;

        // The hit object earlier in time than this note in each column
        public readonly ManiaDifficultyHitObject?[] PreviousHitObjects;

        // Every concurrent note, aka chord notes.
        public ManiaDifficultyHitObject?[] ConcurrentHitObjects { get; }

        // PreviousHitObjects and ConcurrentHitObjects combined.
        public ManiaDifficultyHitObject?[] CurrentHitObjects;

        public int Column;

        // Previous and next long notes relative to the current object.
        // Prev can be the current note.
        public readonly ManiaDifficultyHitObject? PrevLongNote;

        public ManiaDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, List<DifficultyHitObject>[] perColumnObjects, List<DifficultyHitObject>[] perColumnNestedObjects, int index, int longNoteIndex)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            int totalColumns = perColumnObjects.Length;
            this.perColumnObjects = perColumnObjects;
            this.perColumnNestedObjects = perColumnNestedObjects;
            Column = BaseObject.Column;
            columnIndex = this.perColumnObjects[Column].Count;
            nestedColumnIndex = this.perColumnNestedObjects[Column].Count;
            LongNoteIndex = longNoteIndex;
            PreviousHitObjects = new ManiaDifficultyHitObject[totalColumns];
            ConcurrentHitObjects = new ManiaDifficultyHitObject[totalColumns];
            CurrentHitObjects = new ManiaDifficultyHitObject[totalColumns];

            StartTime = base.StartTime;
            EndTime = base.EndTime;

            if (BaseObject is TailNote)
            {
                StartTime = PrevInColumn(0)?.StartTime ?? 0;
            }

            if (index > 0)
            {
                var prevNote = (ManiaDifficultyHitObject)objects[index - 1];

                PrevLongNote = BaseObject is HeadNote ? this : prevNote.PrevLongNote;

                PreviousHitObjects = prevNote.PreviousHitObjects.ToArray();

                if (prevNote.StartTime < StartTime)
                {
                    PreviousHitObjects[prevNote.Column] = prevNote;
                }
            }
        }

        // Should only run after perColumnObjects is fully constructed with all details
        public void InitializeNextHitObjects()
        {
            if (BaseObject is HeadNote)
            {
                EndTime = NextInColumn(0)?.EndTime ?? 0;
            }
        }

        /// <summary>
        /// The previous object in the same column as this <see cref="ManiaDifficultyHitObject"/>, exclusive of Long Note tails.
        /// </summary>
        /// <param name="backwardsIndex">The number of notes to go back.</param>
        /// <returns>The object in this column <paramref name="backwardsIndex"/> notes back, or null if this is the first note in the column.</returns>
        public ManiaDifficultyHitObject? PrevInColumn(int backwardsIndex)
        {
            int index = columnIndex - (backwardsIndex + 1);
            return index >= 0 && index < perColumnObjects[Column].Count ? (ManiaDifficultyHitObject)perColumnObjects[Column][index] : null;
        }

        /// <summary>
        /// The next object in the same column as this <see cref="ManiaDifficultyHitObject"/>, exclusive of Long Note tails.
        /// </summary>
        /// <param name="forwardsIndex">The number of notes to go forward.</param>
        /// <returns>The object in this column <paramref name="forwardsIndex"/> notes forward, or null if this is the last note in the column.</returns>
        public ManiaDifficultyHitObject? NextInColumn(int forwardsIndex)
        {
            int index = columnIndex + (forwardsIndex + 1);
            return index >= 0 && index < perColumnObjects[Column].Count ? (ManiaDifficultyHitObject)perColumnObjects[Column][index] : null;
        }

        /// <summary>
        /// The previous object in the same column as this <see cref="ManiaDifficultyHitObject"/>, inclusive of Long Note tails.
        /// </summary>
        /// <param name="backwardsIndex">The number of notes to go back.</param>
        /// <returns>The object in this column <see cref="backwardsIndex"/> notes back, or null if this is the first note in the column.</returns>
        public ManiaDifficultyHitObject? PrevInColumnNested(int backwardsIndex)
        {
            int index = nestedColumnIndex - (backwardsIndex + 1);
            return index >= 0 && index < perColumnNestedObjects[Column].Count ? (ManiaDifficultyHitObject)perColumnNestedObjects[Column][index] : null;
        }

        /// <summary>
        /// The next object in the same column as this <see cref="ManiaDifficultyHitObject"/>, inclusive of Long Note tails.
        /// </summary>
        /// <param name="forwardsIndex">The number of notes to go forward.</param>
        /// <returns>The object in this column <see cref="forwardsIndex"/> notes forward, or null if this is the last note in the column.</returns>
        public ManiaDifficultyHitObject? NextInColumnNested(int forwardsIndex)
        {
            int index = columnIndex + (forwardsIndex + 1);
            return index >= 0 && index < perColumnObjects[Column].Count ? (ManiaDifficultyHitObject)perColumnObjects[Column][index] : null;
        }
    }
}
