// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public abstract class ManiaSkillBase : Skill
    {
        // We want to smooth our difficulty using the 1000ms window surrounding it.
        protected double SmoothingWindowSize { get; set; } = 1000;
        private double halfSize => SmoothingWindowSize / 2.0;

        // Used to retroactively apply smoothing to past objects
        protected readonly List<double> ObjectTimes = new List<double>();

        // Used to calculate smoothing for the current object
        protected readonly List<double> UnsmoothedDifficultyPoints = new List<double>();
        protected readonly List<double> UnsmoothedTimePoints = new List<double>();

        private double timePointDifficulty;
        private double currentTimePoint;
        private int timePointNoteCount;

        protected ManiaSkillBase(Mod[] mods)
            : base(mods) { }

        public override double DifficultyValue()
        {
            if (ObjectDifficulties.Count == 0)
                return 0;

            return ObjectDifficulties.Average();
        }

        public override void Process(DifficultyHitObject current)
        {
            if (current.StartTime > currentTimePoint || current.Next(0) is null)
            {
                // Go back and smooth out the previous points.
                double nextDelta = current.StartTime - currentTimePoint;

                if (nextDelta > 0)
                {
                    ApplySmoothingToPreviousDifficulties(timePointDifficulty, currentTimePoint, nextDelta);
                }

                // Get the smoothed difficulty for this point.
                UnsmoothedDifficultyPoints.Add(timePointDifficulty);
                UnsmoothedTimePoints.Add(currentTimePoint);

                // Use the next note's time to let the current point contribute to its own smoothed difficulty.
                double smoothedDifficulty = GetSmoothedDifficultyAt(currentTimePoint, nextDelta);

                for (int i = 0; i < timePointNoteCount; i++)
                {
                    ObjectDifficulties.Add(smoothedDifficulty);
                    ObjectTimes.Add(currentTimePoint);
                }

                currentTimePoint = current.StartTime;
                timePointDifficulty = 0;
                timePointNoteCount = 0;
            }

            double baseDifficulty = ProcessInternal(current);
            timePointDifficulty += baseDifficulty;
            timePointNoteCount++;
        }

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            return BaseDifficulty((ManiaDifficultyHitObject)current);
        }

        protected abstract double BaseDifficulty(ManiaDifficultyHitObject current);

        protected virtual double GetSmoothedDifficultyAt(double time, double nextDelta)
        {
            // Cap difficulty here to half extents, since we only want the difficulty of the current note up to the future value of our smoothing window.
            double scaledDifficulty = UnsmoothedDifficultyPoints.LastOrDefault() * Math.Min(nextDelta, halfSize) / SmoothingWindowSize;
            double timeBackwards = 0;

            for (int difficultiesBack = UnsmoothedDifficultyPoints.Count - 2; difficultiesBack >= 0; difficultiesBack--)
            {
                if (timeBackwards >= halfSize && difficultiesBack > 1)
                {
                    int toRemove = Math.Min(difficultiesBack - 1, UnsmoothedDifficultyPoints.Count);

                    UnsmoothedDifficultyPoints.RemoveRange(0, toRemove);
                    UnsmoothedTimePoints.RemoveRange(0, toRemove);

                    break;
                }

                double previousDifficulty = UnsmoothedDifficultyPoints[difficultiesBack];
                double previousTime = UnsmoothedTimePoints[difficultiesBack];

                double startTime = Math.Max(previousTime, time - halfSize);
                double endTime = time - timeBackwards;

                double deltaTime = Math.Max(0, endTime - startTime);

                scaledDifficulty += previousDifficulty * deltaTime / SmoothingWindowSize;
                timeBackwards += deltaTime;
            }

            return scaledDifficulty;
        }

        protected virtual void ApplySmoothingToPreviousDifficulties(double timePointDifficulty, double currentTimePoint, double nextDelta)
        {
            // Don't cap delta here, we handle this with our overlap distances.
            double scaledDifficulty = timePointDifficulty * nextDelta / SmoothingWindowSize;

            if (scaledDifficulty == 0)
                return;

            for (int i = ObjectDifficulties.Count - 1; i >= 0; i--)
            {
                double prevTime = ObjectTimes[i];

                double overlapStartDistance = Math.Min(halfSize, currentTimePoint - prevTime);
                double overlapEndDistance = Math.Min(halfSize, currentTimePoint + nextDelta - prevTime);

                // If we only overlap a portion of the time point's difficulty window, we reduce the amount we add proportionally.
                double influence = (overlapEndDistance - overlapStartDistance) / nextDelta;

                if (influence == 0)
                    break;

                ObjectDifficulties[i] += scaledDifficulty * influence;
            }
        }
    }
}
