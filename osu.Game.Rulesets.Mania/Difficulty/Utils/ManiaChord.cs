// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class ManiaChord
    {
        private readonly List<ManiaDifficultyHitObject> notes = new List<ManiaDifficultyHitObject>();

        public ManiaChord(double startTime)
        {
            StartTime = startTime;
        }

        public double StartTime { get; }
        public double DeltaTime { get; private set; }
        public double QuarterBpm { get; private set; }
        public double HalfBpm { get; private set; }
        public int Index { get; set; }
        public IReadOnlyList<ManiaDifficultyHitObject> Notes => notes;

        public void AddNote(ManiaDifficultyHitObject note)
        {
            notes.Add(note);
        }

        public void Finalise(ManiaChord? previous)
        {
            DeltaTime = StartTime - previous?.StartTime ?? double.PositiveInfinity;

            if (DeltaTime <= 0)
                DeltaTime = 1;

            if (double.IsPositiveInfinity(DeltaTime))
            {
                QuarterBpm = 0;
                HalfBpm = 0;
                return;
            }

            QuarterBpm = 15000 / DeltaTime;
            HalfBpm = 30000 / DeltaTime;
        }
    }
}
