// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;

namespace osu.Game.Rulesets.Mania.Difficulty.Processing
{
    public abstract class DifficultyProcessor : IReadonlyDifficultyProcessor
    {
        protected abstract double ChordStrainDecay { get; }

        public double CurrentStrain { get; private set; }

        public void ProcessRowStrainFor(DifficultyHitObject current)
        {
            var maniaCurrent = (ManiaDifficultyHitObject)current;

            ManiaRow currentRow = maniaCurrent.Row;
            ManiaRow? previousRow = null;

            if (maniaCurrent.Previous() is ManiaDifficultyHitObject maniaPrevious)
                previousRow = maniaPrevious.Row;

            // If we are a different row from the previous note, process ALL the notes
            if (previousRow is null || !currentRow.IsSameRow(previousRow))
            {
                processStrainForRow(currentRow, currentRow.Time - previousRow?.Time);
            }
        }

        private void processStrainForRow(ManiaRow row, double? delta)
        {
            if (delta is not null)
                CurrentStrain *= Math.Pow(ChordStrainDecay, delta.Value / 1000.0);

            foreach (var obj in row.Objects)
            {
                CurrentStrain += CalculateNoteDifficulty(obj);
            }
        }

        protected abstract double CalculateNoteDifficulty(ManiaDifficultyHitObject current);
    }
}
