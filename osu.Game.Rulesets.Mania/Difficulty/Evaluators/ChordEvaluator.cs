// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class ChordEvaluator
    {
        /// <summary>
        /// The time window within which two notes are considered to start simultaneously.
        /// </summary>
        public const double CHORD_TOLERANCE_MS = 8.0;

        /// <summary>
        /// Counts the number of notes starting within <see cref="CHORD_TOLERANCE_MS"/> of <paramref name="current"/>, including itself.
        /// </summary>
        public static int Size(DifficultyHitObject current)
        {
            int chordSize = 1;

            for (int i = 0; current.Previous(i) is { } previous && Math.Abs(previous.StartTime - current.StartTime) <= CHORD_TOLERANCE_MS; i++)
                chordSize++;

            return chordSize;
        }
    }
}
