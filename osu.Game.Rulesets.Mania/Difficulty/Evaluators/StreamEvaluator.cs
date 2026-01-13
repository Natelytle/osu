// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class StreamEvaluator
    {
        private const double other_hand_penalty = 0.5;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject maniaCurr = (ManiaDifficultyHitObject)current;

            // Find the previous chord
            ManiaDifficultyHitObject? maniaPrev = findPreviousChord(maniaCurr);

            if (maniaPrev is null)
                return 0;

            // Collect chord information
            double[] currChordProbabilities = collectChordProbabilities(maniaCurr);
            double[] prevChordProbabilities = collectChordProbabilities(maniaPrev);

            // Calculate per-hand difficulty
            double leftHandDifficulty = calculateHandDifficulty(Hand.Left, maniaCurr, maniaPrev, currChordProbabilities, prevChordProbabilities);
            double rightHandDifficulty = calculateHandDifficulty(Hand.Right, maniaCurr, maniaPrev, currChordProbabilities, prevChordProbabilities);

            // Nerf the opposite hand to the one we're using to hit this note.
            if (maniaCurr.NoteHandedness == Hand.Left)
            {
                rightHandDifficulty *= other_hand_penalty;
            }
            else if (maniaCurr.NoteHandedness == Hand.Right)
            {
                leftHandDifficulty *= other_hand_penalty;
            }
            else
            {
                // If the hand is ambiguous, apply a half-nerf to both hands.
                leftHandDifficulty *= Math.Sqrt(other_hand_penalty);
                rightHandDifficulty *= Math.Sqrt(other_hand_penalty);
            }

            return (leftHandDifficulty + rightHandDifficulty) * Strain.StreamMultiplier;
        }

        private static double calculateHandDifficulty(Hand hand, ManiaDifficultyHitObject currNote, ManiaDifficultyHitObject prevNote, double[] currChordProbs, double[] prevChordProbs)
        {
            double jackDetectionNerf = 1;
            double chordSize = 0;

            for (int i = 0; i < currChordProbs.Length; i++)
            {
                Hand columnHand = Handedness.GetHandednessOf(i, currChordProbs.Length);
                double handFactor = Handedness.GetHandednessFactorOf(hand, columnHand);

                if (handFactor == 0)
                    continue;

                double currProb = currChordProbs[i] * handFactor;
                double prevProb = prevChordProbs[i] * handFactor;

                // Jack detection: if this hand hits the same column twice in a row, it's a jack
                jackDetectionNerf *= 1 - currProb * prevProb;

                chordSize += currProb;
            }

            double timingDifficulty = Math.Min(timeWeightFunc(currNote.StartTime - prevNote.StartTime), CalculateSpeedCap(currNote));

            return Math.Cbrt(chordSize) * timingDifficulty * jackDetectionNerf;
        }

        public static double CalculateSpeedCap(ManiaDifficultyHitObject currentNote)
        {
            const int history_look_back = 10;
            const double history_divisor = 12.0;
            const double min_speedcap_bpm = 150;

            // Walk back through history to find the Nth previous chord
            ManiaDifficultyHitObject? historicalNote = currentNote;
            int chordsFound = 0;

            while (historicalNote is not null && chordsFound < history_look_back)
            {
                historicalNote = findPreviousChord(historicalNote);
                if (historicalNote is not null)
                    chordsFound++;
            }

            double minSpeedCap = timeWeightFunc(DifficultyCalculationUtils.BPMToMilliseconds(min_speedcap_bpm));

            if (historicalNote is null || chordsFound < history_look_back)
                return minSpeedCap; // Not enough history, use the minimum

            double historicalDelta = (currentNote.StartTime - historicalNote.StartTime) / history_divisor;

            double speedCapValue = timeWeightFunc(historicalDelta);

            return Math.Max(speedCapValue, minSpeedCap);
        }

        private static ManiaDifficultyHitObject? findPreviousChord(ManiaDifficultyHitObject currentNote)
        {
            double[] currChordProbabilities = collectChordProbabilities(currentNote);
            ManiaDifficultyHitObject? candidate = (ManiaDifficultyHitObject?)currentNote.Previous(0);

            while (candidate is not null)
            {
                double chordProbability = ManiaDifficultyUtils.ChordProbability(currentNote, candidate);

                if (chordProbability == 0 || chordProbability < currChordProbabilities[candidate.Column])
                    return candidate;

                candidate = (ManiaDifficultyHitObject?)candidate.Previous(0);
            }

            return null;
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
                        chordProbabilities[surroundingNote.Column] = Math.Max(chordProbabilities[surroundingNote.Column], chordProbability);
                    }
                }

                column++;
            }

            return chordProbabilities;
        }

        private static double timeWeightFunc(double deltaTime) => 80 / Math.Pow(deltaTime, 0.88);
    }
}
