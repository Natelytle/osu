// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class ReleaseStrain : StrainDecaySkill
    {
        private const double individual_decay_base = 0.125;
        private const double overall_decay_base = 0.30;
        private double ln_buff = 1.0;

        private readonly double[] startTimes;
        private readonly double[] endTimes;
        private readonly double[] individualStrains;
        private readonly double[] lnBuffResults;
        private readonly ManiaDifficultyHitObject[] ln_buff_note_cache;
        private readonly int keymode;

        private double individualStrain;
        private double overallStrain;

        protected override double SkillMultiplier => 1.0;

        protected override double StrainDecayBase => 1.0;

        public ReleaseStrain(Mod[] mods, int totalColumns)
            : base(mods)
        {
            startTimes = new double[totalColumns];
            endTimes = new double[totalColumns];
            individualStrains = new double[totalColumns];
            lnBuffResults = new double[totalColumns];
            ln_buff_note_cache = new ManiaDifficultyHitObject[totalColumns];
            overallStrain = 1;
            keymode = totalColumns;
        }

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            //if (current.BaseObject is not HoldNote) return 0;
            var maniaCurrent = (ManiaDifficultyHitObject)current;
            double startTime = maniaCurrent.StartTime;
            double endTime = maniaCurrent.EndTime;
            int column = maniaCurrent.BaseObject.Column;
            double ln_buff_result = coolFunction(maniaCurrent);

            lnBuffResults[column] = ln_buff_result;
            ln_buff_note_cache[column] = maniaCurrent;

            // Decay and increase individualStrains in own column
            individualStrains[column] = applyDecay(individualStrains[column], endTime - endTimes[column], individual_decay_base);
            individualStrains[column] += 2.0;

            // For notes at the same time (in a chord), the individualStrain should be the hardest individualStrain out of those columns
            individualStrain = maniaCurrent.DeltaTime <= 1 ? Math.Max(individualStrain, individualStrains[column]) : individualStrains[column];

            // Decay and increase overallStrain
            overallStrain = applyDecay(overallStrain, current.DeltaTime, overall_decay_base);
            overallStrain++;

            // Update startTimes and endTimes arrays
            startTimes[column] = startTime;
            endTimes[column] = endTime;

            // By subtracting CurrentStrain, this skill effectively only considers the maximum strain of any one hitobject within each strain section.
            return ln_buff_result * (individualStrain + overallStrain) - CurrentStrain;
        }

        protected override double CalculateInitialStrain(double offset, DifficultyHitObject current)
            => applyDecay(individualStrain, offset - current.Previous(0).StartTime, individual_decay_base)
               + applyDecay(overallStrain, offset - current.Previous(0).StartTime, overall_decay_base);

        private double applyDecay(double value, double deltaTime, double decayBase)
            => value * Math.Pow(decayBase, deltaTime / 1000);

        private double coolFunction(ManiaDifficultyHitObject obj)
        {
            double hand = getHand(obj.BaseObject.Column);
            double bonus = 0.0;

            int totalDistance = 0;
            int concurrentNotesOnSameHand = 0;
            int concurrentNotes = 0;

            double buff = ln_buff;

            for (int i = 0; i < ln_buff_note_cache.Length; i++)
            {
                ManiaDifficultyHitObject note = ln_buff_note_cache[i];

                if (note != null && note.BaseObject is HoldNote)
                {
                    concurrentNotes++;

                    double noteHand = getHand(note.BaseObject.Column);

                    if (noteHand == hand || noteHand == 0.5)
                    {
                        concurrentNotesOnSameHand++;
                        totalDistance += Math.Abs(obj.BaseObject.Column - note.BaseObject.Column);
                    }
                    //bonus = coolFunction(note) - lnBuffResults[i];
                }
            }

            double averageDistance = totalDistance / Math.Max(1, (double)concurrentNotesOnSameHand);
            return buff * averageDistance * Math.Min(4, concurrentNotes) + bonus;
        }

        private double getHand(int column)
            => ((column == Math.Ceiling(keymode / 2.0)) && (keymode % 2 == 1)) ? (0.5) : ((column < keymode / 2.0) ? 0 : 1);
    }
}
