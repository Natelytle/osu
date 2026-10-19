using System;
using System.Collections.Generic;
using osu.Framework.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class TotalAccuracyCurve
    {
        private const double min_ramp_width = 1e-6;

        private readonly List<(double Skill, double SlopeDelta, int ActiveDelta)> events = new List<(double, double, int)>();

        private double baseAccuracy;
        private double maxAccuracy;

        private double[] skills = Array.Empty<double>();
        private double[] values = Array.Empty<double>();
        private bool dirty;

        public void AddToCurve(AccuracyDifficulties difficulties)
        {
            double[] table = AccuracyValueMultipliers.ACCURACY_VALUES;

            dirty = true;
            maxAccuracy += table[0];

            if (difficulties.BaseDifficulty <= 0)
            {
                baseAccuracy += table[0];
                return;
            }

            baseAccuracy += table[^1];

            for (int i = 0; i < table.Length - 1; i++)
            {
                double upper = difficulties.GetDifficulty(i);
                double lower = Math.Min(difficulties.GetDifficulty(i + 1), upper - min_ramp_width);
                double step = table[i] - table[i + 1];
                double slope = step / (upper - lower);

                events.Add((lower, slope, 1));
                events.Add((upper, -slope, -1));
            }
        }

        public double TotalAccuracyAt(double skill)
        {
            if (dirty) build();

            if (skills.Length == 0 || skill <= skills[0])
                return baseAccuracy;

            if (skill >= skills[^1])
                return values[^1];

            int idx = Array.BinarySearch(skills, skill);

            if (idx >= 0)
                return values[idx];

            int hi = ~idx;
            int lo = hi - 1;

            return Interpolation.Lerp(values[lo], values[hi], (skill - skills[lo]) / (skills[hi] - skills[lo]));
        }

        public double TotalAccuracy() => maxAccuracy;

        private void build()
        {
            dirty = false;
            events.Sort((a, b) => a.Skill.CompareTo(b.Skill));

            var skillList = new List<double>(events.Count);
            var valueList = new List<double>(events.Count);

            double slope = 0;
            double value = baseAccuracy;
            int active = 0;

            for (int i = 0; i < events.Count; i++)
            {
                var (skill, slopeDelta, activeDelta) = events[i];

                if (i > 0)
                    value = Math.Clamp(value + slope * (skill - events[i - 1].Skill), value, maxAccuracy);

                slope += slopeDelta;
                active += activeDelta;

                if (active == 0)
                    slope = 0;

                if (skillList.Count > 0 && skillList[^1] == skill)
                {
                    valueList[^1] = value;
                    continue;
                }

                skillList.Add(skill);
                valueList.Add(value);
            }

            skills = skillList.ToArray();
            values = valueList.ToArray();
        }
    }
}
