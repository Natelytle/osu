// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
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

        public readonly double ColumnStrainTime;

        // Chord information
        public readonly List<ManiaChord> Chords;
        public readonly ManiaChord CurrentChord;

        public ManiaDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, List<DifficultyHitObject>[] perColumnObjects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            int totalColumns = perColumnObjects.Length;
            this.perColumnObjects = perColumnObjects;
            Column = BaseObject.Column;
            columnIndex = perColumnObjects[Column].Count;
            PreviousHitObjects = new ManiaDifficultyHitObject[totalColumns];
            ColumnStrainTime = StartTime - PrevInColumn(0)?.StartTime ?? StartTime;

            if (index > 0)
            {
                ManiaDifficultyHitObject prevNote = (ManiaDifficultyHitObject)objects[index - 1];

                for (int i = 0; i < prevNote.PreviousHitObjects.Length; i++)
                    PreviousHitObjects[i] = prevNote.PreviousHitObjects[i];

                // intentionally depends on processing order to match live.
                PreviousHitObjects[prevNote.Column] = prevNote;
            }

            ManiaDifficultyHitObject? prevObject = (ManiaDifficultyHitObject?)objects.LastOrDefault();
            Chords = prevObject?.Chords ?? new List<ManiaChord>();
            ManiaChord? currChord = prevObject?.CurrentChord;
            ManiaChord? prevChord = Chords.LastOrDefault();

            if (currChord is null || !Precision.AlmostEquals(currChord.StartTime, StartTime))
            {
                if (currChord != null)
                {
                    currChord.Finalise(prevChord);
                    Chords.Add(currChord);
                }

                currChord = new ManiaChord(StartTime)
                {
                    Index = Chords.Count
                };
            }

            currChord.AddNote(this);
            CurrentChord = currChord;
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
        /// The previous object in the same column as this <see cref="ManiaDifficultyHitObject"/>, exclusive of Long Note tails.
        /// </summary>
        /// <param name="backwardsIndex">The number of notes to go back.</param>
        /// <returns>The object in this column <paramref name="backwardsIndex"/> notes back, or null if this is the first note in the column.</returns>
        public ManiaChord? PreviousChord(int backwardsIndex)
        {
            int index = CurrentChord.Index - (backwardsIndex + 1);
            return index >= 0 && index < Chords.Count ? Chords[index] : null;
        }

        /// <summary>
        /// The next object in the same column as this <see cref="ManiaDifficultyHitObject"/>, exclusive of Long Note tails.
        /// </summary>
        /// <param name="forwardsIndex">The number of notes to go forward.</param>
        /// <returns>The object in this column <paramref name="forwardsIndex"/> notes forward, or null if this is the last note in the column.</returns>
        public ManiaChord? NextChord(int forwardsIndex)
        {
            int index = CurrentChord.Index + (forwardsIndex + 1);
            return index >= 0 && index < Chords.Count ? Chords[index] : null;
        }
    }
}
