// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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

        protected abstract double GetSmoothedDifficultyAt(double time, double nextDelta);
        protected abstract void ApplySmoothingToPreviousDifficulties(double timePointDifficulty, double currentTimePoint, double nextDelta);
    }
}
