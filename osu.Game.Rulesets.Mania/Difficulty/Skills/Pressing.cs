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

        // We want to smooth our difficulty forward and backward by 500ms.
        private const double smoothing_window_extents = 500;

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

            double chordDifficulty = chordAccumulator * 1000 / Math.Min(1000, next.HeadDeltaTime);

            chordAccumulator = 0;

            double pressingDifficulty = PressingEvaluator.EvaluateDifficultyOf(current);

            return pressingDifficulty + chordDifficulty;
        }

        protected override double GetSmoothedDifficultyAt(double time, double nextDelta)
        {
            double difficulty = UnsmoothedDifficultyPoints.LastOrDefault() * nextDelta / smoothing_window_extents;
            double timeBackwards = 0;

            for (int difficultiesBack = UnsmoothedDifficultyPoints.Count - 2; difficultiesBack >= 0; difficultiesBack--)
            {
                if (timeBackwards >= smoothing_window_extents && difficultiesBack > 1)
                {
                    int toRemove = Math.Min(difficultiesBack - 1, UnsmoothedDifficultyPoints.Count);

                    UnsmoothedDifficultyPoints.RemoveRange(0, toRemove);
                    UnsmoothedTimePoints.RemoveRange(0, toRemove);

                    break;
                }

                double previousDifficulty = UnsmoothedDifficultyPoints[difficultiesBack];
                double previousTime = UnsmoothedTimePoints[difficultiesBack];

                double startTime = Math.Max(previousTime, time - smoothing_window_extents);
                double endTime = time - timeBackwards;

                double deltaTime = Math.Max(0, endTime - startTime);

                difficulty += previousDifficulty * deltaTime / smoothing_window_extents;
                timeBackwards += deltaTime;
            }

            return difficulty;
        }

        protected override void ApplySmoothingToPreviousDifficulties(double timePointDifficulty, double currentTimePoint, double nextDelta)
        {
            double smoothedDifficulty = timePointDifficulty * nextDelta / smoothing_window_extents;

            if (smoothedDifficulty == 0)
                return;

            for (int i = ObjectDifficulties.Count - 1; i >= 0; i--)
            {
                double prevTime = ObjectTimes[i];

                double overlapStartDistance = Math.Min(smoothing_window_extents, currentTimePoint - prevTime);
                double overlapEndDistance = Math.Min(smoothing_window_extents, currentTimePoint + nextDelta - prevTime);

                // If we only overlap a portion of the time point's difficulty window, we reduce the amount we add proportionally.
                double influence = (overlapEndDistance - overlapStartDistance) / nextDelta;

                if (influence == 0)
                    break;

                ObjectDifficulties[i] += smoothedDifficulty * influence;
            }
        }
    }
}
