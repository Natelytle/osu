// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.PreviousNoteBonusDicts
{
    public class FourthLastNote
    {
        public static double GetAngleBonus(double? thirdAngle, double? fourthAngle)
        {
            if (thirdAngle is null || fourthAngle is null) return 1.0;

            List<Dictionary<int, double>> bonusesList = new List<Dictionary<int, double>>
            {
                bonuses0,
                bonuses30,
                bonuses60,
                bonuses90,
                bonuses120,
                bonuses150,
                bonuses180
            };

            int lowerBonusIndex = (int)(thirdAngle.Value / 30);
            int upperBonusIndex = Math.Min(lowerBonusIndex + 1, 6);

            double lowerBonusWeight = 30 - thirdAngle.Value % 30;
            double upperBonusWeight = 30 - lowerBonusWeight;

            double lowerBonus = AngleBonus.GetBonusFromDict(bonusesList[lowerBonusIndex], fourthAngle.Value);
            double upperBonus = AngleBonus.GetBonusFromDict(bonusesList[upperBonusIndex], fourthAngle.Value);

            return (lowerBonus * lowerBonusWeight + upperBonus * upperBonusWeight) / (lowerBonusWeight + upperBonusWeight);
        }

        // Bonuses for if the previous note was 0 degrees.
        // int = angle, double = difficulty multiplier.
        private static readonly Dictionary<int, double> bonuses0 = new Dictionary<int, double>
        {
            { 0, 2.00 },
            { 30, 1.50 },
            { 60, 1.25 },
            { 90, 1.20 },
            { 120, 1.10 },
            { 150, 1.05 },
            { 180, 1.00 },
        };

        // Bonuses for if the previous note was 30 degrees.
        // int = angle, double = difficulty multiplier.
        private static readonly Dictionary<int, double> bonuses30 = new Dictionary<int, double>
        {
            { 0, 2.00 },
            { 30, 1.50 },
            { 60, 1.25 },
            { 90, 1.20 },
            { 120, 1.10 },
            { 150, 1.05 },
            { 180, 1.00 },
        };

        // Bonuses for if the previous note was 30 degrees.
        // int = angle, double = difficulty multiplier.
        private static readonly Dictionary<int, double> bonuses60 = new Dictionary<int, double>
        {
            { 0, 2.00 },
            { 30, 1.50 },
            { 60, 1.25 },
            { 90, 1.20 },
            { 120, 1.10 },
            { 150, 1.05 },
            { 180, 1.00 },
        };

        // Bonuses for if the previous note was 30 degrees.
        // int = angle, double = difficulty multiplier.
        private static readonly Dictionary<int, double> bonuses90 = new Dictionary<int, double>
        {
            { 0, 2.00 },
            { 30, 1.50 },
            { 60, 1.25 },
            { 90, 1.20 },
            { 120, 1.10 },
            { 150, 1.05 },
            { 180, 1.00 },
        };

        // Bonuses for if the previous note was 30 degrees.
        // int = angle, double = difficulty multiplier.
        private static readonly Dictionary<int, double> bonuses120 = new Dictionary<int, double>
        {
            { 0, 2.00 },
            { 30, 1.50 },
            { 60, 1.25 },
            { 90, 1.20 },
            { 120, 1.10 },
            { 150, 1.05 },
            { 180, 1.00 },
        };

        // Bonuses for if the previous note was 30 degrees.
        // int = angle, double = difficulty multiplier.
        private static readonly Dictionary<int, double> bonuses150 = new Dictionary<int, double>
        {
            { 0, 2.00 },
            { 30, 1.50 },
            { 60, 1.25 },
            { 90, 1.20 },
            { 120, 1.10 },
            { 150, 1.05 },
            { 180, 1.00 },
        };

        // Bonuses for if the previous note was 30 degrees.
        // int = angle, double = difficulty multiplier.
        private static readonly Dictionary<int, double> bonuses180 = new Dictionary<int, double>
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
