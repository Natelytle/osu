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
            double longNoteBonus = 1.0 + 6 * lnHeldSecondsBetween(current.StartTime, next.StartTime, current.PreviousHitObjects);

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

        private static double lnHeldSecondsBetween(double startTime, double endTime, ManiaDifficultyHitObject?[] previousHitObjects)
        {
            if (endTime - startTime <= 0)
                return 0;

            double totalDensityUnits = 0;

            for (int i = 0; i < previousHitObjects.Length; i++)
            {
                var obj = previousHitObjects[i];

                if (obj?.Tail == null)
                    continue;

                double lnStart = obj.StartTime;
                double lnEnd = obj.Tail.ActualTime;

                if (lnEnd <= startTime || lnStart >= endTime)
                    continue;

                double windowStart = Math.Max(lnStart, startTime);
                double windowEnd = Math.Min(lnEnd, endTime);

                // The first 60ms of the long note don't contribute any LN amount, to nerf short-LN patterns.
                // We treat the first 60ms as if they don't exist, to nerf patterns that abuse short LNs.
                // We apply a bonus between 60ms and 120ms to make up for this.
                double heldStartPoint = lnStart + 60;
                double bonusReductionPoint = lnStart + 120;

                // Calculate overlap with our full bonus segment [lnBonusStart, bonusReductionPoint]
                double fullBonusOverlap = Math.Max(0, Math.Min(bonusReductionPoint, windowEnd) - Math.Max(heldStartPoint, windowStart));

                // Calculate overlap with the partial bonus segment [bonusReductionPoint, lnEnd]
                double partialBonusOverlap = Math.Max(0, Math.Min(lnEnd, windowEnd) - Math.Max(bonusReductionPoint, windowStart));

                totalDensityUnits += (fullBonusOverlap * 1.3) + (partialBonusOverlap * 1.0);
            }

            return totalDensityUnits / 1000.0;
        }
    }
}
