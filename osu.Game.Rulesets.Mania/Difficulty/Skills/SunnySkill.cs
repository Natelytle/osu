// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class SunnySkill : Skill
    {
        private readonly int totalColumns;
        private readonly double hitLeniency;

        private readonly Corners corners;
        private readonly List<ManiaDifficultyHitObject> noteList = new List<ManiaDifficultyHitObject>();
        private readonly List<ManiaDifficultyHitObject>[] perColumnNoteList;

        public SunnySkill(Mod[] mods, int totalColumns, double od, double mapEndTime)
            : base(mods)
        {
            hitLeniency = 0.3 * Math.Pow((64.5 - Math.Ceiling(od * 3.0)) / 500.0, 0.5);
            this.totalColumns = totalColumns;
            perColumnNoteList = new List<ManiaDifficultyHitObject>[totalColumns];

            for (int i = 0; i < totalColumns; i++)
                perColumnNoteList[i] = new List<ManiaDifficultyHitObject>();

            corners = new Corners(mapEndTime);
        }

        // Mania difficulty hit objects are already sorted in the difficulty calculator, we just need to populate the lists.
        public override void Process(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject currObj = (ManiaDifficultyHitObject)current;

            noteList.Add(currObj);
            perColumnNoteList[currObj.Column].Add(currObj);
            corners.AddCornersForNote(currObj);
        }

        public override double DifficultyValue()
        {
            if (noteList.Count <= 0)
                return 0;

            double[] baseCorners = corners.BaseCorners.ToArray();
            double[] aCorners = corners.ACorners.ToArray();
            double[] allCorners = corners.AllCorners.ToArray();

            int length = allCorners.Length;

            double[] x = CrossColumnPressure.EvaluateCrossColumnPressure(perColumnNoteList, totalColumns, hitLeniency, baseCorners, allCorners);
            double[] j = SameColumnPressure.EvaluateSameColumnPressure(perColumnNoteList, totalColumns, hitLeniency, baseCorners, allCorners);
            double[] p = PressingIntensity.EvaluatePressingIntensity(noteList, perColumnNoteList, hitLeniency, baseCorners, allCorners);
            double[] r = ReleaseFactor.EvaluateReleaseFactor(noteList, hitLeniency, baseCorners, allCorners);
            double[] a = Unevenness.EvaluateUnevenness(perColumnNoteList, totalColumns, aCorners, allCorners);

            double xMax = x.Max();
            double xMin = x.Min();
            double jMax = j.Max();
            double jMin = j.Min();
            double pMax = p.Max();
            double pMin = p.Min();
            double rMax = r.Max();
            double rMin = r.Min();
            double aMax = a.Max();
            double aMin = a.Min();

            double[] c = new double[length];

            int start = 0;
            int end = 0;

            for (int i = 0; i < allCorners.Length; i++)
            {
                while (start < noteList.Count && noteList[start].StartTime < allCorners[i] - 500)
                    start += 1;

                while (end < noteList.Count && noteList[end].StartTime < allCorners[i] + 500)
                    end += 1;

                c[i] = end - start;
            }

            double[] ks = KeyUsage.GetKeyUsages(perColumnNoteList, allCorners);

            // Final star rating calculations.
            double[] s = new double[length];
            double[] t = new double[length];
            double[] d = new double[length];

            for (int i = 0; i < length; i++)
            {
                double term1 = Math.Pow(Math.Pow(a[i], 3.0 / ks[i]) * Math.Min(j[i], 8 + 0.85 * j[i]), 1.5);
                double term2 = Math.Pow(Math.Pow(a[i], 2.0 / 3.0) * (0.8 * p[i] + r[i] * 35.0 / (c[i] + 8)), 1.5);
                double sVal = Math.Pow(0.4 * term1 + (1 - 0.4) * term2, 2.0 / 3.0);
                double tVal = Math.Pow(a[i], 3.0 / ks[i]) * x[i] / (x[i] + sVal + 1);

                s[i] = sVal;
                t[i] = tVal;
                d[i] = 2.7 * Math.Pow(sVal, 0.5) * Math.Pow(tVal, 1.5) + sVal * 0.27;
            }

            double[] gaps = new double[length];

            if (length == 1)
                gaps[0] = 0;
            else
            {
                gaps[0] = (allCorners[1] - allCorners[0]) / 2.0;
                gaps[^1] = (allCorners[^1] - allCorners[^2]) / 2.0;

                for (int i = 1; i < length - 1; i++)
                    gaps[i] = (allCorners[i + 1] - allCorners[i - 1]) / 2.0;
            }

            double[] effectiveWeights = new double[length];

            for (int i = 0; i < length; i++)
            {
                effectiveWeights[i] = c[i] * gaps[i];
            }

            List<CornerData> cornerDataList = new List<CornerData>();

            for (int i = 0; i < length; i++)
            {
                cornerDataList.Add(new CornerData
                {
                    Time = allCorners[i],
                    J = j[i],
                    X = x[i],
                    P = p[i],
                    A = a[i],
                    R = r[i],
                    C = c[i],
                    Ks = ks[i],
                    D = d[i],
                    Weight = effectiveWeights[i]
                });
            }

            var sortedList = cornerDataList.OrderBy(cd => cd.D).ToList();
            double[] cumWeights = new double[sortedList.Count];

            double sumW = 0.0;

            for (int i = 0; i < sortedList.Count; i++)
            {
                sumW += sortedList[i].Weight;
                cumWeights[i] = sumW;
            }

            double totalWeight = sumW;
            double[] normCumWeights = cumWeights.Select(cw => cw / totalWeight).ToArray();

            double[] targetPercentiles = new[] { 0.945, 0.935, 0.925, 0.915, 0.845, 0.835, 0.825, 0.815 };

            List<int> indices = new List<int>();

            foreach (double tp in targetPercentiles)
            {
                int idx = Array.FindIndex(normCumWeights, cw => cw >= tp);
                if (idx < 0)
                    idx = sortedList.Count - 1;
                indices.Add(idx);
            }

            double percentile93, percentile83;

            if (indices.Count >= 8)
            {
                double sum93 = 0.0;
                for (int i = 0; i < 4; i++)
                    sum93 += sortedList[indices[i]].D;
                percentile93 = sum93 / 4.0;
                double sum83 = 0.0;
                for (int i = 4; i < 8; i++)
                    sum83 += sortedList[indices[i]].D;
                percentile83 = sum83 / 4.0;
            }
            else
            {
                percentile93 = sortedList.Average(cd => cd.D);
                percentile83 = percentile93;
            }

            double numWeighted = 0.0;
            double denWeighted = 0.0;

            for (int i = 0; i < sortedList.Count; i++)
            {
                numWeighted += Math.Pow(sortedList[i].D, 5) * sortedList[i].Weight;
                denWeighted += sortedList[i].Weight;
            }

            double weightedMean = Math.Pow(numWeighted / denWeighted, 1.0 / 5);

            double sr = (0.88 * percentile93) * 0.25 + (0.94 * percentile83) * 0.2 + weightedMean * 0.55;

            int noteCount = noteList.Count;

            // Each LN is weighted as 1 note per 200 milliseconds, with a max of 5 notes per LN.
            double lnCount = noteList.Where(obj => obj.BaseObject is HoldNote).Sum(obj => Math.Min(obj.EndTime - obj.StartTime, 1000)) / 200.0;

            // length weighting
            double totalNotes = noteCount + 0.5 * lnCount;
            sr *= totalNotes / (totalNotes + 60);

            if (sr > 9)
                sr += (sr - 9) * (1.0 / 1.2);

            sr *= 0.975;

            return sr;
        }

        /// <summary>
        /// Used to store various computed values at a corner (time point).
        /// </summary>
        public struct CornerData
        {
            public double Time;
            public double J;
            public double X;
            public double P;
            public double A;
            public double R;
            public double C;
            public double Ks;
            public double D;
            public double Weight;
        }
    }
}
