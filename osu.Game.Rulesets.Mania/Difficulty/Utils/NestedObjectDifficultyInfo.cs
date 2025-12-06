// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public struct NestedObjectDifficultyInfo
    {
        public double Difficulty;
        public double Time;
        public double ColumnStrainTime;
        public bool IsTail;

        public ManiaDifficultyHitObject Note;

        public NestedObjectDifficultyInfo(double difficulty, ManiaDifficultyHitObject note, bool isTail = false)
        {
            Difficulty = difficulty;
            Time = isTail ? note.EndTime : note.StartTime;
            ColumnStrainTime = isTail ? note.EndTime - note.StartTime : note.ColumnStrainTime;
            IsTail = isTail;

            Note = note;
        }
    }
}
