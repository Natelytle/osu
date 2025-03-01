// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class ChordEvaluator
    {
        private const double chord_scale_factor = 0.03478260;
        private const double grace_note_tolerance = 50;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject mCurrent = (ManiaDifficultyHitObject)current;

            int jackCount = 0;

            double chordDelta = mCurrent.StartTime - mCurrent.PreviousHitObjects.ToList().ConvertAll(obj => obj?.StartTime ?? double.PositiveInfinity + grace_note_tolerance).Min();
            if (chordDelta == 0) chordDelta = double.PositiveInfinity;

            int chordSize = mCurrent.ConcurrentHitObjects.Count(obj => obj is not null);

            // Find amount of jacks
            foreach (ManiaDifficultyHitObject? note in mCurrent.ConcurrentHitObjects)
            {
                if (note?.PrevInColumn(0) == null) continue;

                if (note.StartTime - note.PrevInColumn(0)!.StartTime <= chordDelta + grace_note_tolerance)
                    jackCount++;
            }

            double chordBpm = 15000.0 / chordDelta;
            double scaledChordBpmFactor = chordBpmScale(chordBpm);
            double scaledTrillBpmFactor = 0.25 * chordBpmScale(chordBpm);

            // 1 means all jacks =>  no trills
            // 0 means  no jacks => all trills
            double jackDensity = jackCount / (double)chordSize;
            double jackVal = jackDensity * jackCount * scaledChordBpmFactor;
            double trillVal = (1 - jackDensity) * (chordSize - jackCount) * scaledTrillBpmFactor;

            return 0; // return chord_scale_factor * (jackVal + trillVal);
        }

        public static int FindJackCountInChord(ManiaDifficultyHitObject note, double deltaTime, double tolerance)
            => note.ConcurrentHitObjects
                   .Where(obj => obj is not null)
                   .Where(obj => obj!.PrevInColumn(0) is not null)
                   .Count(obj => obj!.StartTime - obj.PrevInColumn(0)!.StartTime <= deltaTime + tolerance);

        private static double chordBpmScale(double bpm) => bpm * Math.Pow(bpm / 240, 0.16);
    }
}
