// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
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

        public readonly double ColumnStrainTime;

        public List<ManiaDifficultyHitObject>[] SurroundingNotes;

        public ManiaDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, List<DifficultyHitObject>[] perColumnObjects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            int totalColumns = perColumnObjects.Length;
            this.perColumnObjects = perColumnObjects;
            Column = BaseObject.Column;
            columnIndex = perColumnObjects[Column].Count;
            PreviousHitObjects = new ManiaDifficultyHitObject[totalColumns];
            ColumnStrainTime = StartTime - PrevInColumn(0)?.StartTime ?? StartTime;
            SurroundingNotes = new List<ManiaDifficultyHitObject>[totalColumns];

            for (int column = 0; column < totalColumns; column++)
            {
                SurroundingNotes[column] = new List<ManiaDifficultyHitObject>();
            }

            if (index > 0)
            {
                ManiaDifficultyHitObject? prev = (ManiaDifficultyHitObject?)objects[index - 1];

                for (int i = 0; i < prev!.PreviousHitObjects.Length; i++)
                    PreviousHitObjects[i] = prev.PreviousHitObjects[i];

                // intentionally depends on processing order to match live.
                PreviousHitObjects[prev.Column] = prev;

                const double note_position_history_max = 50;

                // Collect all previous note positions up to one second ago.
                while (prev is not null && StartTime - prev.StartTime < note_position_history_max)
                {
                    SurroundingNotes[prev.Column].Add(prev);
                    prev.SurroundingNotes[Column].Add(this);

                    prev = (ManiaDifficultyHitObject?)prev.Previous(0);
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
    }
}
