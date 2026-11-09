// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing
{
    public class ManiaDifficultyHitObject : DifficultyHitObject
    {
        public new ManiaHitObject BaseObject => (ManiaHitObject)base.BaseObject;

        public readonly int Column;

        public readonly double ColumnDelta;

        public readonly ManiaDifficultyHitObject?[] PreviousHitObjects;

        public ManiaRow Row = null!;

        // Chords centered around presses and releases respectively.
        public readonly ManiaDifficultyHitObject?[] ChordHitObjects;
        public readonly ManiaDifficultyHitObject?[] TailChordHolds;

        // Head overlapped means the LN body is held through the current head, and tail overlapped means the LN body is held through the current tail.
        public readonly ManiaDifficultyHitObject?[] HeadOverlappedHolds;
        public readonly ManiaDifficultyHitObject?[] TailOverlappedHolds;

        // Last concurrently released means if we have LNs like this
        // Release:   1      2          3                   4
        // [==========] [====] [========] [=================]
        //     [=================================] <--- current
        // 0ms -------------------------------------------- 1000ms
        // We select the LN of the third release, as it is the latest release that overlaps this LN body.
        public readonly ManiaDifficultyHitObject?[] LastConcurrentlyReleasedHolds;

        public double HeldNoteMultiplier = 1.0;

        private readonly ManiaDifficultyHitObject?[] chordPreviousHitObjects;

        private readonly List<DifficultyHitObject>[] perColumnObjects;

        private readonly int columnIndex;

        public ManiaDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, List<DifficultyHitObject>[] perColumnObjects, int index, IReadOnlyList<int> columnLayout)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            int totalColumns = perColumnObjects.Length;
            this.perColumnObjects = perColumnObjects;
            Column = columnLayout[BaseObject.Column];
            columnIndex = perColumnObjects[Column].Count;
            PreviousHitObjects = new ManiaDifficultyHitObject[totalColumns];
            ColumnDelta = (StartTime - PrevInColumn(0)?.StartTime) ?? StartTime;

            if (index > 0)
            {
                ManiaDifficultyHitObject prevNote = (ManiaDifficultyHitObject)objects[index - 1];

                for (int i = 0; i < prevNote.PreviousHitObjects.Length; i++)
                    PreviousHitObjects[i] = prevNote.PreviousHitObjects[i];

                PreviousHitObjects[prevNote.Column] = prevNote;
            }

            TailOverlappedHolds = new ManiaDifficultyHitObject[totalColumns];
            TailChordHolds = new ManiaDifficultyHitObject[totalColumns];

            if (index == 0)
            {
                chordPreviousHitObjects = new ManiaDifficultyHitObject[totalColumns];
                ChordHitObjects = new ManiaDifficultyHitObject[totalColumns];
                HeadOverlappedHolds = new ManiaDifficultyHitObject[totalColumns];
            }
            else
            {
                var prevNote = (ManiaDifficultyHitObject)objects[index - 1];

                bool sameChord = prevNote.StartTime == StartTime;

                // Pass by reference if we're in the same chord, so that this note will update previously processed notes in the chord.
                chordPreviousHitObjects = sameChord ? prevNote.chordPreviousHitObjects : prevNote.chordPreviousHitObjects.ToArray();
                ChordHitObjects = sameChord ? prevNote.ChordHitObjects : new ManiaDifficultyHitObject[totalColumns];
                HeadOverlappedHolds = sameChord ? prevNote.HeadOverlappedHolds : new ManiaDifficultyHitObject[totalColumns];

                // If this is a new chord, update relational info for this chord *and* the previous chord.
                if (!sameChord)
                    updateChordRelationsOf(prevNote);
            }

            // Seed the last concurrently released holds with LNs that overlap the head of the LN, but are released before the tail.
            LastConcurrentlyReleasedHolds = HeadOverlappedHolds.Select(o => o?.EndTime > EndTime ? null : o).ToArray();

            // Need to do a lil something extra to ensure completely overlapped notes register the tail overlap.
            foreach (var prevHitObj in chordPreviousHitObjects)
            {
                if (prevHitObj is null || prevHitObj.EndTime <= EndTime)
                    continue;

                TailOverlappedHolds[prevHitObj.Column] = prevHitObj;

                prevHitObj.LastConcurrentlyReleasedHolds[Column] = this; // Any engulfed LN is a later hold that we therefore need to update.
            }

            ChordHitObjects[Column] = this;
        }

        private void updateChordRelationsOf(ManiaDifficultyHitObject prevNote)
        {
            // Update previous hit objects for the new chord with the previous chord's notes.
            for (int i = 0; i < prevNote.ChordHitObjects.Length; i++)
            {
                if (prevNote.ChordHitObjects[i] is not null)
                    chordPreviousHitObjects[i] = prevNote.ChordHitObjects[i];
            }

            // These arrays are shared between all chord notes by reference, so updating them updates every note in the chord.
            foreach (var prevObj in chordPreviousHitObjects)
            {
                if (prevObj is null)
                    continue;

                // Checking directly with this note is fine, since all notes in the chord would be overlapped.
                if (prevObj.StartTime < StartTime && prevObj.EndTime > StartTime)
                    HeadOverlappedHolds[prevObj.Column] = prevObj;

                // Look back and update tail info.
                if (prevObj.EndTime == EndTime)
                {
                    prevObj.TailChordHolds[Column] = this;
                    TailChordHolds[prevObj.Column] = prevObj;
                }
                else if (prevObj.EndTime > StartTime && prevObj.EndTime < EndTime)
                {
                    prevObj.TailOverlappedHolds[Column] = this;
                }
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

        public double LastStartTimeInColumn(int column) => PreviousHitObjects[column]?.StartTime ?? double.NegativeInfinity;
    }
}
