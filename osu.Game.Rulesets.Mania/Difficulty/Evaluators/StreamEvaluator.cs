// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class StreamEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject maniaCurr = (ManiaDifficultyHitObject)current;

            // We keep a collection of our current chord notes (stored as probabilities to allow for non-discrete chords).
            double[] currChordProbabilities = collectChordProbabilities(maniaCurr);

            ManiaDifficultyHitObject? maniaPrev = (ManiaDifficultyHitObject?)maniaCurr.Previous(0);

            double[]? prevChordProbabilities = null;

            // Ugly control flow
            while (maniaPrev is not null)
            {
                double chordProbability = ManiaDifficultyUtils.ChordProbability(maniaCurr, maniaPrev);

                if (chordProbability == 0 || chordProbability < currChordProbabilities[maniaPrev.Column])
                {
                    prevChordProbabilities = collectChordProbabilities(maniaPrev);
                    break;
                }

                maniaPrev = (ManiaDifficultyHitObject?)maniaPrev.Previous(0);
            }

            // These are the same thing but for the linter's sake we check them both
            if (maniaPrev is null || prevChordProbabilities is null)
                return 0;

            double jackDetectionNerf = 1;
            double currChordSum = 0;
            double prevChordSum = 0;

            for (int i = 0; i < currChordProbabilities.Length; i++)
            {
                double currChordProb = currChordProbabilities[i];
                double prevChordProb = prevChordProbabilities[i];

                // If we get any overlap it means we don't hit this like a stream - 0 difficulty.
                jackDetectionNerf *= 1 - currChordProb * prevChordProb;

                currChordSum += currChordProb;
                prevChordSum += prevChordProb;
            }

            return (Math.Sqrt(currChordSum) + Math.Cbrt(prevChordSum) - 1) * timeWeightFunc(maniaCurr.StartTime - maniaPrev.StartTime) * jackDetectionNerf;
        }

        private static double[] collectChordProbabilities(ManiaDifficultyHitObject currentNote)
        {
            double[] chordProbabilities = new double[currentNote.SurroundingNotes.Length];
            int column = 0;

            foreach (List<ManiaDifficultyHitObject> surroundingColumn in currentNote.SurroundingNotes)
            {
                if (column == currentNote.Column)
                {
                    chordProbabilities[currentNote.Column] = 1;
                }
                else
                {
                    foreach (ManiaDifficultyHitObject surroundingNote in surroundingColumn)
                    {
                        double chordProbability = ManiaDifficultyUtils.ChordProbability(currentNote, surroundingNote);

                        // We take the note with the highest probability of being a chord note.
                        chordProbabilities[surroundingNote.Column] = Math.Max(chordProbabilities[surroundingNote.Column], chordProbability);
                    }
                }

                column++;
            }

            return chordProbabilities;
        }

        private static double timeWeightFunc(double columnDeltaTime) => 80 / Math.Pow(columnDeltaTime, 0.88);
    }
}
