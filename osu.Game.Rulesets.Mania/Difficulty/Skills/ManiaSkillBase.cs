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

        // Used to calculate smoothing for the current chord
        protected readonly List<double> UnsmoothedChordDifficulties = new List<double>();
        protected readonly List<double> UnsmoothedChordTime = new List<double>();

        private double chordDifficulty;
        private double currentChordTime;
        private int chordNoteCount;

        // Hacky thing used to connect LN difficulties together
        protected int ProcessedNoteCount => ObjectDifficulties.Count + chordNoteCount;

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
            if (current.StartTime > currentChordTime)
            {
                AddChordDifficulties(current.StartTime);
            }

            double baseDifficulty = ProcessInternal(current);
            chordDifficulty += baseDifficulty;
            chordNoteCount++;

            // Add the final chord difficulties
            if (current.Next(0) is null)
            {
                AddChordDifficulties(current.StartTime);
            }
        }

        // Flush the current difficulties and set up the next chord
        protected void AddChordDifficulties(double newStartTime)
        {
            // Go back and smooth out the previous points.
            double nextDelta = newStartTime - currentChordTime;

            if (nextDelta > 0)
            {
                ApplySmoothingToPreviousDifficulties(chordDifficulty, currentChordTime, nextDelta);
            }

            UnsmoothedChordDifficulties.Add(chordDifficulty);
            UnsmoothedChordTime.Add(currentChordTime);

            // Use the next note's time to let the current chord contribute to its own smoothed difficulty.
            double smoothedDifficulty = GetSmoothedDifficultyAt(currentChordTime, nextDelta);

            for (int i = 0; i < chordNoteCount; i++)
            {
                ObjectDifficulties.Add(smoothedDifficulty);
                ObjectTimes.Add(currentChordTime);
            }

            currentChordTime = newStartTime;
            chordDifficulty = 0;
            chordNoteCount = 0;
        }

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            return BaseDifficulty((ManiaDifficultyHitObject)current);
        }

        protected abstract double BaseDifficulty(ManiaDifficultyHitObject current);

        protected virtual double GetSmoothedDifficultyAt(double time, double nextDelta)
        {
            // Cap difficulty here to half extents, since we only want the difficulty of the current note up to the future value of our smoothing window.
            double scaledDifficulty = UnsmoothedChordDifficulties.LastOrDefault() * Math.Min(nextDelta, halfSize) / SmoothingWindowSize;
            double timeBackwards = 0;

            for (int difficultiesBack = UnsmoothedChordDifficulties.Count - 2; difficultiesBack >= 0; difficultiesBack--)
            {
                if (timeBackwards >= halfSize && difficultiesBack > 1)
                {
                    int toRemove = Math.Min(difficultiesBack - 1, UnsmoothedChordDifficulties.Count);

                    UnsmoothedChordDifficulties.RemoveRange(0, toRemove);
                    UnsmoothedChordTime.RemoveRange(0, toRemove);

                    break;
                }

                double previousDifficulty = UnsmoothedChordDifficulties[difficultiesBack];
                double previousTime = UnsmoothedChordTime[difficultiesBack];

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
