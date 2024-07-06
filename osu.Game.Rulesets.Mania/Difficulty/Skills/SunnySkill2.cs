// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class SunnySkill2 : Skill
    {
        public SunnySkill2(Mod[] mods, int totalColumns)
            : base(mods)
        {
            currObjects = new ManiaDifficultyHitObject[totalColumns];
        }

        private readonly ManiaDifficultyHitObject?[] currObjects;
        private readonly List<double> difficulties = new List<double>();

        public override void Process(DifficultyHitObject current)
        {
            difficulties.Add(strainValueAt(current));
        }

        private double strainValueAt(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject currObj = (ManiaDifficultyHitObject)current;
            currObjects[currObj.Column] = currObj;

            double sameColumnIntensity = SunnyEvaluator.EvaluateSameColumnIntensityAt(currObjects);
            double crossColumnIntensity = SunnyEvaluator.EvaluateCrossColumnIntensityAt(currObjects);
            double pressingIntensity = SunnyEvaluator.EvaluatePressingIntensityAt(currObj, currObjects);
            double unevennessIntensity = SunnyEvaluator.EvaluateUnevennessIntensityAt(currObjects);
            double releaseIntensity = SunnyEvaluator.EvaluateReleaseFactorAt(currObj);

            return (sameColumnIntensity + crossColumnIntensity + pressingIntensity + unevennessIntensity + releaseIntensity);
        }

        public override double DifficultyValue()
        {
            return difficulties.Max();
        }
    }
}
