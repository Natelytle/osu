// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Calculators;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class SunnySkill : Skill
    {
        private readonly List<Note> noteSeq;
        private readonly List<List<Note>> noteSeqByColumn;
        private readonly int totalColumns;
        private double spikiness;
        private double switches;
        private readonly double greatHitWindow;
        private readonly Mod[] mods;

        public SunnySkill(Mod[] mods, int totalColumns, int objectCount, double greatHitWindow)
            : base(mods)
        {
            this.totalColumns = totalColumns;

            noteSeq = new List<Note>(objectCount);

            this.greatHitWindow = greatHitWindow;

            noteSeqByColumn = new List<List<Note>>();

            for (int k = 0; k < totalColumns; k++)
            {
                noteSeqByColumn.Add(new List<Note>());
            }

            this.mods = mods;
        }

        // Mania difficulty hit objects are already sorted in the difficulty calculator, we just need to populate the lists.
        public override void Process(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject currObj = (ManiaDifficultyHitObject)current;

            int endTime = currObj.EndTime == currObj.StartTime ? -1 : (int)currObj.EndTime;

            Note note = new Note(currObj.BaseObject.Column, (int)currObj.StartTime, endTime);

            noteSeq.Add(note);

            noteSeqByColumn[note.Column].Add(note);
        }

        public override double DifficultyValue()
        {
            if (noteSeq.Count <= 0)
                return 0;

            double x = 0.3 * Math.Pow(greatHitWindow / 500.0, 0.5);
            x = Math.Min(x, 0.6 * (x - 0.09) + 0.09);

            SrParams srParams = MaCalculator.Calculate(noteSeq, noteSeqByColumn, totalColumns, x, mods.Any(m => m is ModClassic));
            spikiness = srParams.Spikiness;
            switches = srParams.Switches;

            return srParams.Sr;
        }

        public double VarietyValue()
        {
            return MaCalculator.Variety(noteSeq, noteSeqByColumn);
        }

        public double AccScalarValue()
        {
            return 0.5 * spikiness + 0.5 * switches;
        }
    }
}
