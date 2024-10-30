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

        // The current hit object in each column
        public readonly ManiaDifficultyHitObject?[] CurrentHitObjects;

        public readonly int NotesInCurrentChord;

        public int Column;

        public ManiaDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, List<DifficultyHitObject>[] perColumnObjects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            int totalColumns = perColumnObjects.Length;
            this.perColumnObjects = perColumnObjects;
            Column = BaseObject.Column;
            columnIndex = this.perColumnObjects[Column].Count;
            CurrentHitObjects = new ManiaDifficultyHitObject[totalColumns];

            // Note: We're iterating through objects from left to right, so when calculating chord difficulty we want to do so on the rightmost note, or else mirror mod will different values.
            for (int i = 0; i < totalColumns; i++)
                CurrentHitObjects[i] = (ManiaDifficultyHitObject?)perColumnObjects[i].LastOrDefault();

            NotesInCurrentChord = CurrentHitObjects.Count(obj => obj?.StartTime == hitObject.StartTime);
        }

        public DifficultyHitObject? PrevInColumn(int backwardsIndex)
        {
            int index = columnIndex - (backwardsIndex + 1);
            return index >= 0 && index < perColumnObjects[Column].Count ? perColumnObjects[Column][index] : default;
        }

        public DifficultyHitObject? NextInColumn(int forwardsIndex)
        {
            int index = columnIndex + (forwardsIndex + 1);
            return index >= 0 && index < perColumnObjects[Column].Count ? perColumnObjects[Column][index] : default;
        }
    }
}
