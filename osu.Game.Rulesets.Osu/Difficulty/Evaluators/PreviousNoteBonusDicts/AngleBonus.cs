// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.PreviousNoteBonusDicts
{
    public class AngleBonus
    {
        internal static double GetBonusFromDict(Dictionary<int, double> angleBonusDict, double angle)
        {
            int lowerBoundLastAngle = 30 * (int)(angle / 30);
            int upperBoundLastAngle = Math.Min(lowerBoundLastAngle + 30, 180);

            double lowerAngleWeight = 30 - angle % 30;
            double upperAngleWeight = 30 - lowerAngleWeight;

            double lowerAngleBonus = angleBonusDict[lowerBoundLastAngle];
            double upperAngleBonus = angleBonusDict[upperBoundLastAngle];

            return (lowerAngleBonus * lowerAngleWeight + upperAngleBonus * upperAngleWeight) / (lowerAngleWeight + upperAngleWeight);
        }
    }
}
