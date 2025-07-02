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
        public new ManiaHitObject BaseObject => (ManiaHitObject)base.BaseObject;

        private readonly List<DifficultyHitObject>[] perColumnObjects;

        private readonly int columnIndex;

        public readonly int Column;

        // The hit object earlier in time than this note in each column
        public readonly ManiaDifficultyHitObject?[] PreviousHitObjects;

        // Chord notes
        public ManiaDifficultyHitObject?[] ConcurrentHitObjects;

        // The current hit object in each column
        public ManiaDifficultyHitObject?[] CurrentHitObjects;

        public readonly double StrainTime;

        public readonly double ColumnDeltaTime;

        public ManiaDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, List<DifficultyHitObject>[] perColumnObjects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            int totalColumns = perColumnObjects.Length;
            this.perColumnObjects = perColumnObjects;
            Column = BaseObject.Column;
            columnIndex = perColumnObjects[Column].Count;
            PreviousHitObjects = new ManiaDifficultyHitObject[totalColumns];
            ConcurrentHitObjects = new ManiaDifficultyHitObject[totalColumns];
            CurrentHitObjects = new ManiaDifficultyHitObject[totalColumns];
            ColumnDeltaTime = StartTime - PrevInColumn(0)?.StartTime ?? StartTime;
            StrainTime = Math.Max(DeltaTime, 2.5);

            CurrentHitObjects = new ManiaDifficultyHitObject[totalColumns];

            for (int i = 0; i < totalColumns; i++)
                CurrentHitObjects[i] = (ManiaDifficultyHitObject?)perColumnObjects[i].LastOrDefault();

            if (index > 0)
            {
                ManiaDifficultyHitObject? prevNote = (ManiaDifficultyHitObject)objects[index - 1];

                PreviousHitObjects = [..prevNote.PreviousHitObjects];

                if (StartTime > prevNote.StartTime)
                {
                    do
                    {
                        PreviousHitObjects[prevNote.Column] = prevNote;
                    }
                    while (prevNote.DeltaTime == 0 && (prevNote = (ManiaDifficultyHitObject?)prevNote.Previous(0)) is not null);
                }
            }

            // Fix some stuff for tail notes
            if (hitObject is TailNote)
            {
                columnIndex -= 1;

                // The first note isnt processed but the first tail is. In this case, the index will be -1 and the per column objects array will have no notes.
                if (columnIndex > -1)
                    Index = perColumnObjects[Column][columnIndex].Index;

                columnIndex = Math.Max(columnIndex, 0);
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
    }
}
