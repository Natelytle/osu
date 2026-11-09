// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning
{
    public sealed record RowLoad(double[] FingerRates, HandLoad Left, HandLoad Right, double RowRate)
    {
        public HandLoad Of(ManiaHand hand) => hand == ManiaHand.Left ? Left : Right;
    }
}
