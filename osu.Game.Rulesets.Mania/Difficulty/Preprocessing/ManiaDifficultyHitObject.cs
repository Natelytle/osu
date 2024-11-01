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

        // The index of the current note when nestedHitObjects are combined.
        public readonly int NoNestedIndex;

        // The hit object earlier in time than this note in each column
        public readonly ManiaDifficultyHitObject?[] PrevHitObjects;
        public ManiaDifficultyHitObject?[] CurrHitObjects { get; set; }

        public int Column;

        public ManiaDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, List<DifficultyHitObject>[] perColumnObjects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            int totalColumns = perColumnObjects.Length;
            this.perColumnObjects = perColumnObjects;
            Column = BaseObject.Column;
            columnIndex = this.perColumnObjects[Column].Count;
            NoNestedIndex = objects.Count(obj => obj.BaseObject is not TailNote);
            PrevHitObjects = new ManiaDifficultyHitObject[totalColumns];
            CurrHitObjects = new ManiaDifficultyHitObject[totalColumns];

            for (int i = 0; i < totalColumns; i++)
            {
                PrevHitObjects[i] = perColumnObjects[i].LastOrDefault()?.StartTime == hitObject.StartTime
                    ? (ManiaDifficultyHitObject?)perColumnObjects[i].LastOrDefault()
                    : (ManiaDifficultyHitObject?)perColumnObjects[i].AsEnumerable().Reverse().Skip(1).FirstOrDefault();
            }
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
