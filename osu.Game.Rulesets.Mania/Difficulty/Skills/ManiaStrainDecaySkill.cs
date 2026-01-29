// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public abstract class ManiaStrainDecaySkill : ManiaSkill
    {
        protected abstract double StrainDecayBase { get; }

        protected double CurrentStrain;
        protected double? CurrentChordDelta;

        protected ManiaStrainDecaySkill(Mod[] mods)
            : base(mods)
        {
        }

        protected override void AddChordDifficulties(double newStartTime)
        {
            double decay = StrainDecay(CurrentChordDelta ?? CurrentChordTime);

            CurrentStrain *= decay;
            CurrentStrain += ChordDifficulty * (1 - decay);

            for (int i = 0; i < ChordNoteCount; i++)
            {
                ObjectDifficulties.Add(CurrentStrain);
            }

            CurrentChordDelta = newStartTime - CurrentChordTime;
        }

        protected double StrainDecay(double ms) => Math.Pow(StrainDecayBase, ms / 1000);
    }
}
