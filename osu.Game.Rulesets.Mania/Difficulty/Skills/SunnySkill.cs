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

            int noteCount = noteList.Count;
            int lnCount = noteList.Count(obj => obj.BaseObject is HoldNote);

            double[] baseCorners = corners.BaseCorners.ToArray();
            double[] aCorners = corners.ACorners.ToArray();
            double[] allCorners = corners.AllCorners.ToArray();

            double[] x = CrossColumnPressure.EvaluateCrossColumnPressure(perColumnNoteList, totalColumns, hitLeniency, baseCorners, allCorners);
            double[] j = SameColumnPressure.EvaluateSameColumnPressure(perColumnNoteList, totalColumns, hitLeniency, baseCorners, allCorners);
            double[] p = PressingIntensity.EvaluatePressingIntensity(noteList, perColumnNoteList, hitLeniency, baseCorners, allCorners);
            double[] r = ReleaseFactor.EvaluateReleaseFactor(noteList, hitLeniency, baseCorners, allCorners);
            double[] a = Unevenness.EvaluateUnevenness(perColumnNoteList, totalColumns, aCorners, allCorners);

            // --- Everything below this comment is all old and wrong ---

            double sum1 = 0;
            double sum2 = 0;

            int start = 0;
            int end = 0;

            for (int i = 0; i < allCorners.Length; i++)
            {
                // Clamp each pressure value to [0-inf]
                x[i] = Math.Max(0, x[i]);
                j[i] = Math.Max(0, j[i]);
                p[i] = Math.Max(0, p[i]);
                a[i] = Math.Max(0, a[i]);
                r[i] = Math.Max(0, r[i]);

                while (start < noteList.Count && noteList[start].StartTime < allCorners[i] - 500)
                {
                    start += 1;
                }

                while (end < noteList.Count && noteList[end].StartTime < allCorners[i] + 500)
                {
                    end += 1;
                }

                int c = end - start;

                double strain = Math.Pow(0.37 * Math.Pow(Math.Pow(a[i], 1.0 / 2.0) * j[i], 1.5) + (1 - 0.37) * Math.Pow(Math.Pow(a[i], 2.0 / 3.0) * (p[i] + r[i]), 1.5), 2.0 / 3.0);
                double twist = x[i] / (x[i] + strain + 1);

                double deez = 2.7 * Math.Pow(strain, 1.0 / 2.0) * Math.Pow(twist, 1.5) + strain * 0.27;

                sum1 += Math.Pow(deez, 4.0) * c;
                sum2 += c;
            }

            double starRating = Math.Pow(sum1 / sum2, 1.0 / 4.0);
            starRating = Math.Pow(starRating, 1.2) / Math.Pow(8, 1.2) * 8;

            // Nerf short maps
            starRating = starRating * (noteCount + 0.5 * lnCount) / (noteCount + 0.5 * lnCount + 60);

            // Buff high column counts
            return starRating * (0.88 + 0.03 * totalColumns);
        }
    }
}
