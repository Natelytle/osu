// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.PreviousNoteBonusDicts
{
    public class ThirdLastNote
    {
        public static double GetAngleBonus(double? angle) => angle is not null ? AngleBonus.GetBonusFromDict(Bonuses, angle.Value) : 1.0;

        // int = angle, double = difficulty multiplier.
        internal static Dictionary<int, double> Bonuses = new Dictionary<int, double>
        {
            { 0, 2.00 },
            { 30, 1.50 },
            { 60, 1.25 },
            { 90, 1.20 },
            { 120, 1.10 },
            { 150, 1.05 },
            { 180, 1.00 },
        };
    }
}
