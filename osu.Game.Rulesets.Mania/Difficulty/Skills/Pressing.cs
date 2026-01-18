// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Pressing : ManiaSkillHeads
    {
        public Pressing(Mod[] mods)
            : base(mods) { }

        // We want to smooth our difficulty using the 1000ms window surrounding it.
        private const double smoothing_window_size = 1000;
        private const double half_size = smoothing_window_size / 2.0;

        private double chordAccumulator;

        protected override double BaseDifficulty(ManiaDifficultyHitObject current)
        {
            var next = current.NextHead(0);

            if (next is null)
                return 0;

            // We want to divide chord difficulty by the deltaTime to the next note, so we accumulate it until we know what that is.
            if (next.HeadDeltaTime == 0)
            {
                chordAccumulator += PressingEvaluator.EvaluateChordDifficultyOf(current);

                return 0;
            }

            // We multiply chord difficulty by the portion of the smoothing window of this chord, so that it becomes a constant value when smoothed.
            double chordDifficulty = chordAccumulator * smoothing_window_size / Math.Min(smoothing_window_size, next.HeadDeltaTime);

            chordAccumulator = 0;

            double pressingDifficulty = PressingEvaluator.EvaluateDifficultyOf(current);

            return pressingDifficulty + chordDifficulty;
        }

        protected override double GetSmoothedDifficultyAt(double time, double nextDelta)
        {
            // Cap difficulty here to half extents, since we only want the difficulty of the current note up to the future value of our smoothing window.
            double scaledDifficulty = UnsmoothedDifficultyPoints.LastOrDefault() * Math.Min(nextDelta, half_size) / smoothing_window_size;
            double timeBackwards = 0;

            for (int difficultiesBack = UnsmoothedDifficultyPoints.Count - 2; difficultiesBack >= 0; difficultiesBack--)
            {
                if (timeBackwards >= half_size && difficultiesBack > 1)
                {
                    int toRemove = Math.Min(difficultiesBack - 1, UnsmoothedDifficultyPoints.Count);

                    UnsmoothedDifficultyPoints.RemoveRange(0, toRemove);
                    UnsmoothedTimePoints.RemoveRange(0, toRemove);

                    break;
                }

                double previousDifficulty = UnsmoothedDifficultyPoints[difficultiesBack];
                double previousTime = UnsmoothedTimePoints[difficultiesBack];

                double startTime = Math.Max(previousTime, time - half_size);
                double endTime = time - timeBackwards;

                double deltaTime = Math.Max(0, endTime - startTime);

                scaledDifficulty += previousDifficulty * deltaTime / smoothing_window_size;
                timeBackwards += deltaTime;
            }

            return scaledDifficulty;
        }

        protected override void ApplySmoothingToPreviousDifficulties(double timePointDifficulty, double currentTimePoint, double nextDelta)
        {
            // Don't cap delta here, we handle this with our overlap distances.
            double scaledDifficulty = timePointDifficulty * nextDelta / smoothing_window_size;

            if (scaledDifficulty == 0)
                return;

            for (int i = ObjectDifficulties.Count - 1; i >= 0; i--)
            {
                double prevTime = ObjectTimes[i];

                double overlapStartDistance = Math.Min(half_size, currentTimePoint - prevTime);
                double overlapEndDistance = Math.Min(half_size, currentTimePoint + nextDelta - prevTime);

                // If we only overlap a portion of the time point's difficulty window, we reduce the amount we add proportionally.
                double influence = (overlapEndDistance - overlapStartDistance) / nextDelta;

                if (influence == 0)
                    break;

                ObjectDifficulties[i] += scaledDifficulty * influence;
            }
        }
    }
}
