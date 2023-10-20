// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class FingerControl : Skill
    {
        private readonly double fingerControlClockRate;

        public FingerControl(Mod[] mods, double clockRate)
            : base(mods)
        {
            fingerControlClockRate = clockRate;
        }

        private readonly List<OsuHitObject> hitObjects = new List<OsuHitObject>();

        /// <summary>
        /// Calculates finger control difficulty of the map
        /// </summary>
        public override double DifficultyValue()
        {
            if (hitObjects.Count == 0)
            {
                return 0;
            }

            double prevTime = hitObjects[0].StartTime / 1000.0;
            double currStrain = 0;
            double prevStrainTime = 0;
            int repeatStrainCount = 1;
            var strainHistory = new List<double> { 0 };

            // calculate strain value for each hit object
            for (int i = 1; i < hitObjects.Count; i++)
            {
                double currTime = hitObjects[i].StartTime / 1000.0;
                double deltaTime = (currTime - prevTime) / fingerControlClockRate;

                double strainTime = Math.Max(deltaTime, 0.046875);
                double strainDecayBase = Math.Pow(0.9, 1 / Math.Min(strainTime, 0.2));

                currStrain *= Math.Pow(strainDecayBase, deltaTime);

                strainHistory.Add(currStrain);

                double strain = 0.1 / strainTime;

                if (Math.Abs(strainTime - prevStrainTime) > 0.004)
                    repeatStrainCount = 1;
                else
                    repeatStrainCount++;

                if (hitObjects[i] is Slider)
                    strain /= 2.0;

                if (repeatStrainCount % 2 == 0)
                    strain = 0;
                else
                    strain /= Math.Pow(1.25, repeatStrainCount);

                currStrain += strain;

                prevTime = currTime;
                prevStrainTime = strainTime;
            }

            // aggregate strain values to compute difficulty
            var strainHistoryArray = strainHistory.ToArray();

            Array.Sort(strainHistoryArray);
            Array.Reverse(strainHistoryArray);

            double diff = 0;

            const double k = 0.95;

            for (int i = 0; i < hitObjects.Count; i++)
            {
                diff += strainHistoryArray[i] * Math.Pow(k, i);
            }

            return diff * (1 - k) * 1.1;
        }

        public override void Process(DifficultyHitObject current)
        {
            hitObjects.Add((OsuHitObject)current.BaseObject);
        }
    }
}
