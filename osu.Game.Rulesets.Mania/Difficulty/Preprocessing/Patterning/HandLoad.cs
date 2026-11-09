// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning
{
    public readonly record struct HandLoad(double Throughput, double SecondMoment, double RowRate, double HeldFingers, double FingerCapacity)
    {
        public double EffectiveRate => Throughput > 0 ? SecondMoment / Throughput : 0.0;

        public double ChordSize => RowRate > 0 ? Throughput / RowRate : 0.0;

        public double Occupancy => RowRate > 0 ? EffectiveRate / RowRate : 0.0;

        public double Fill => FingerCapacity > 1.0 ? ChordSize / FingerCapacity : 0.0;

        public double HeldShare => FingerCapacity > 0 ? HeldFingers / FingerCapacity : 0.0;

        public double Reshaping(double repetition) => Math.Clamp(1.0 - Occupancy, 0.0, 1.0) * (1.0 - repetition);
    }
}
