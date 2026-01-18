// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class PressingEvaluator
    {
        private const double stream_bpm_start = 320;
        private const double stream_bpm_end = 720;

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            ManiaDifficultyHitObject? next = current.NextHead(0);

            double leniency = ManiaDifficultyUtils.CalculateHitLeniency(current.GreatHitWindow);

            if (next is null || next.HeadDeltaTime == 0)
            {
                return 0;
            }

            double nextDelta = next.HeadDeltaTime;
            double baseDifficulty = 1000.0 / nextDelta;

            // Multiplier based on how many long notes are currently being held.
            double longNoteBonus = 1.0 + 6 * calculateLnHeldSecondsAmount(current.StartTime, next.StartTime, current.PreviousHitObjects);

            // A bonus for notes with a length between stream_bpm_start and stream_bpm_end.
            double streamBonus = 1.0;
            double streamBpm = DifficultyCalculationUtils.MillisecondsToBPM(nextDelta);

            if (streamBpm > stream_bpm_start && streamBpm < stream_bpm_end)
            {
                double bpmBonusRatio = (streamBpm - stream_bpm_start) / (stream_bpm_end - stream_bpm_start);
                double quadraticBpmNerfRatio = Math.Pow((stream_bpm_end - streamBpm) / (stream_bpm_end - stream_bpm_start), 2);

                streamBonus += bpmBonusRatio * quadraticBpmNerfRatio * 1.35;
            }

            double leniencyPenalty;

            if (nextDelta < (2.0 / 3.0) * leniency)
            {
                double deviation = (nextDelta - leniency / 2.0) / 1000.0;
                leniencyPenalty = Math.Max(0, 1 - 24 * Math.Pow(deviation, 2) / (leniency / 1000));
            }
            else
            {
                leniencyPenalty = Math.Max(0, 1 - (2.0 / 3.0) * (leniency / 1000));
            }

            leniencyPenalty = Math.Pow(leniencyPenalty, 0.25) * Math.Pow(80 / leniency, 0.25);

            return baseDifficulty * Math.Max(streamBonus, longNoteBonus) * leniencyPenalty;
        }

        public static double EvaluateChordDifficultyOf(ManiaDifficultyHitObject current)
        {
            double leniency = ManiaDifficultyUtils.CalculateHitLeniency(current.GreatHitWindow);

            return Math.Pow(0.02 * (4000.0 / leniency - 24.0), 0.25);
        }

        private static double calculateLnHeldSecondsAmount(double startTime, double endTime, ManiaDifficultyHitObject?[] previousHitObjects)
        {
            double duration = endTime - startTime;
            if (duration <= 0) return 0;

            double totalDensityUnits = 0;

            for (int i = 0; i < previousHitObjects.Length; i++)
            {
                var obj = previousHitObjects[i];
                if (obj?.Tail == null) continue;

                double lnStart = obj.StartTime;
                double lnEnd = obj.Tail.ActualTime;

                // Intersection of the LN and the requested time window
                double windowLnStart = Math.Max(lnStart, startTime);
                double windowLnEnd = Math.Min(lnEnd, endTime);

                if (windowLnEnd <= windowLnStart)
                    continue;

                // The first 60ms of the long note don't contribute any LN amount, to nerf short-LN patterns.
                // After 60ms, until 120ms, we provide the seconds spent holding this note.
                // After 120ms, we provide a reduced amount.
                double lnBonusStart = lnStart + 60;
                double bonusReductionPoint = lnStart + 120;

                // Calculate overlap with our full bonus segment [lnBonusStart, bonusReductionPoint]
                double fullBonusOverlap = Math.Max(0, Math.Min(windowLnEnd, bonusReductionPoint) - lnBonusStart);

                // Calculate overlap with the partial bonus segment [bonusReductionPoint, lnEnd]
                double partialBonusOverlap = Math.Max(0, windowLnEnd - Math.Max(windowLnStart, bonusReductionPoint));

                totalDensityUnits += (fullBonusOverlap * 1.0) + (partialBonusOverlap * 0.7);
            }

            totalDensityUnits = Math.Min(totalDensityUnits, (2.5 * duration) + (0.5 * totalDensityUnits));

            return totalDensityUnits / 1000.0;
        }
    }
}
