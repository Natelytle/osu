// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class SpeedEvaluator
    {
        private const double speed_factor = 0.16;
        private const double grace_note_tolerance = 6;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject maniaCurrObj = (ManiaDifficultyHitObject)current;

            double noteDelta = maniaCurrObj.StartTime - maniaCurrObj.PrevHitObjects.ToList().ConvertAll(obj => (obj?.StartTime ?? double.PositiveInfinity) + grace_note_tolerance).Min();
            if (noteDelta == 0) noteDelta = double.PositiveInfinity;

            // BPM as in 1/2th Notes because that is what players usually use to refer to speed BPM
            double speedBpm = 15000.0 / noteDelta;

            double streamCount = 0;

            ManiaDifficultyHitObject? prevChord = getPreviousChord(maniaCurrObj);

            // Find amount of notes in stream (allow single jacks)
            while (streamCount < maniaCurrObj.Index)
            {
                if (prevChord is null || ChordEvaluator.FindJackCountInChord(prevChord, noteDelta, grace_note_tolerance) > 1)
                    break;

                prevChord = getPreviousChord(prevChord);
                streamCount++;
            }

            double streamStamina = Math.Min(3000, streamCount + (streamCount * streamCount / 1000.0));

            double staminaBonus = 1 + Math.Pow(streamStamina, 0.12);

            return speed_factor * speedBpmScale(speedBpm) * staminaBonus;
        }

        private static ManiaDifficultyHitObject? getPreviousChord(ManiaDifficultyHitObject note)
        {
            return note.PrevHitObjects.OrderBy(obj => obj?.StartTime).LastOrDefault();
        }

        private static double speedBpmScale(double bpm) => bpm * Math.Pow(bpm / 380, 1.2);
    }
}
