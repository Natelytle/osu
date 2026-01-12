// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class ManiaDifficultyUtils
    {
        public const double GRACE_TOLERANCE = 50;

        /// <summary>
        /// How likely the player is to play this surrounding note as part of a chord with the current note.
        /// </summary>
        /// <param name="currentNote">The note to compare against.</param>
        /// <param name="surroundingNote">The surrounding note.</param>
        /// <returns>A probability value between zero and one of the player playing this note as a chord with the current.</returns>
        public static double ChordProbability(ManiaDifficultyHitObject currentNote, ManiaDifficultyHitObject surroundingNote)
        {
            ManiaDifficultyHitObject? surrNext = surroundingNote.NextInColumn(0);

            // If there's a note in between this note and the note we think we're in a chord with, we ain't in a chord.
            if (surrNext is not null && surrNext.StartTime <= currentNote.StartTime)
                return 0;

            // If not, we weight it by how close our notes are in time. Eventually, I want to weight it by how close the next closest note is as well.
            return DifficultyCalculationUtils.SmoothstepBellCurve(currentNote.StartTime, surroundingNote.StartTime, GRACE_TOLERANCE);
        }
    }
}
