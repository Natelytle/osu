// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public abstract class ManiaSkillBase : Skill
    {
        protected readonly List<(double, double)> PreviousStrainPointsList = new List<(double, double)>();

        private double currentTimePoint;
        private double timePointStrainCache;
        private int strainPointNoteCount;

        protected ManiaSkillBase(Mod[] mods)
            : base(mods) { }

        public override double DifficultyValue()
        {
            throw new System.NotImplementedException();
        }

        public override void Process(DifficultyHitObject current)
        {
            if (current.StartTime > currentTimePoint || current.Next(0) is null)
            {
                double smoothedDifficulty = ApplySmoothing(currentTimePoint, timePointStrainCache);

                for (int i = 0; i < strainPointNoteCount; i++)
                {
                    ObjectDifficulties.Add(smoothedDifficulty);
                }

                PreviousStrainPointsList.Add((currentTimePoint, timePointStrainCache));

                currentTimePoint = current.StartTime;
                timePointStrainCache = 0;
                strainPointNoteCount = 0;
            }

            double baseDifficulty = ProcessInternal(current);
            timePointStrainCache += baseDifficulty;
            strainPointNoteCount++;
        }

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            return BaseDifficulty((ManiaDifficultyHitObject)current);
        }

        protected abstract double BaseDifficulty(ManiaDifficultyHitObject current);

        protected abstract double ApplySmoothing(double currentTimePoint, double strain);
    }
}
