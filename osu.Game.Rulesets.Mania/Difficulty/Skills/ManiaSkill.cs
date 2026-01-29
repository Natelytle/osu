// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public abstract class ManiaSkill : Skill
    {
        // Used to link tail difficulties up with the LN that corresponds with them.
        public readonly Dictionary<int, int> HeadToDifficultyIndex = new Dictionary<int, int>();
        public readonly Dictionary<int, int> TailToHeadIndex = new Dictionary<int, int>();
        private int tailIndex;

        protected double ChordDifficulty { get; private set; }
        protected double CurrentChordTime { get; private set; }
        protected int ChordNoteCount { get; private set; }

        // Hacky thing used to connect LN difficulties together
        protected int ProcessedNoteCount => ObjectDifficulties.Count + ChordNoteCount;

        public enum LnMode
        {
            Heads,
            Tails,
            Both
        }

        private readonly LnMode lnProcessingMode;

        protected ManiaSkill(Mod[] mods, LnMode lnProcessingMode = LnMode.Heads)
            : base(mods)
        {
            this.lnProcessingMode = lnProcessingMode;
        }

        public override double DifficultyValue()
        {
            if (ObjectDifficulties.Count == 0)
                return 0;

            return ObjectDifficulties.Average();
        }

        public sealed override void Process(DifficultyHitObject current)
        {
            ManiaDifficultyHitObject maniaCurrent = (ManiaDifficultyHitObject)current;

            // Block notes that aren't relevant to the skill at hand, depends on LnMode.
            if (!shouldProcess(current))
            {
                return;
            }

            ManiaDifficultyHitObject? next = lnProcessingMode switch
            {
                LnMode.Heads => maniaCurrent.NextHead(0),
                LnMode.Tails => maniaCurrent.NextTail(0),
                LnMode.Both => (ManiaDifficultyHitObject)maniaCurrent.Next(0),
                _ => (ManiaDifficultyHitObject)maniaCurrent.Next(0)
            };

            double baseDifficulty = ProcessInternal(current);
            ChordDifficulty += baseDifficulty;
            ChordNoteCount++;

            // Add the chord difficulties at the end of the chord
            if (next is null || next.StartTime > CurrentChordTime)
            {
                double newChordStartTime = next?.StartTime ?? current.StartTime;

                AddChordDifficulties(newChordStartTime);
                resetChord(newChordStartTime);
            }

            // And updates the indices.
            if (maniaCurrent.BaseObject is HeadNote)
            {
                if (maniaCurrent.HeadIndex is not null) HeadToDifficultyIndex.Add(maniaCurrent.HeadIndex.Value, ProcessedNoteCount - 1);
            }
            else if (maniaCurrent.BaseObject is TailNote)
            {
                if (maniaCurrent.HeadIndex is not null) TailToHeadIndex.Add(tailIndex, maniaCurrent.HeadIndex.Value);
                tailIndex++;
            }
        }

        private void resetChord(double newChordTime)
        {
            CurrentChordTime = newChordTime;
            ChordDifficulty = 0;
            ChordNoteCount = 0;
        }

        protected abstract void AddChordDifficulties(double newStartTime);

        private bool shouldProcess(DifficultyHitObject current)
        {
            return current.BaseObject switch
            {
                TailNote => lnProcessingMode is LnMode.Tails or LnMode.Both,
                _ => lnProcessingMode is not LnMode.Tails
            };
        }

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            return BaseDifficulty((ManiaDifficultyHitObject)current);
        }

        protected abstract double BaseDifficulty(ManiaDifficultyHitObject current);
    }
}
