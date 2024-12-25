// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osuTK;
using static osu.Game.Rulesets.Difficulty.Utils.DifficultyCalculationUtils;
using static osu.Game.Rulesets.Osu.Difficulty.Preprocessing.OsuDifficultyHitObject;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class SnapAimEvaluator
    {
        private static double multiplier => 61.0;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            // Base snap difficulty is velocity.
            double difficulty = EvaluateDistanceBonus(current);

            difficulty += EvaluateAgilityBonus(current);
            difficulty += EvaluateAngleBonus(current);
            difficulty += EvaluateVelocityChangeBonus(current);

            return difficulty;
        }

        public static double EvaluateDistanceBonus(DifficultyHitObject current)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;

            // Base snap difficulty is velocity.
            double distanceBonus = osuCurrObj.Movement.Length / osuCurrObj.StrainTime;

            return distanceBonus * multiplier;
        }

        public static double EvaluateAgilityBonus(DifficultyHitObject current)
        {
            if (!IsValid(current, 1))
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj0 = (OsuDifficultyHitObject)current.Previous(0);

            double currTime = osuCurrObj.StrainTime;
            double prevTime = osuPrevObj0.StrainTime;

            double agilityBonus = Math.Pow(MillisecondsToBPM(Math.Max(currTime, prevTime), 2) / 285, 3);

            return agilityBonus * multiplier;
        }

        public static double EvaluateAngleBonus(DifficultyHitObject current)
        {
            if (!IsValid(current, 4))
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj0 = (OsuDifficultyHitObject)current.Previous(0);
            var osuPrevObj1 = (OsuDifficultyHitObject)current.Previous(1);

            double currTime = osuCurrObj.StrainTime;
            double prevTime = osuPrevObj0.StrainTime;

            double currAngle = osuCurrObj.Angle!.Value;
            double lastAngle = osuPrevObj0.Angle!.Value;
            double lastLastAngle = osuPrevObj1.Angle!.Value;

            var currMovement = osuCurrObj.Movement;
            var prevMovement = osuPrevObj0.Movement;

            double currVelocity = currMovement.Length / currTime;
            double prevVelocity = prevMovement.Length / prevTime;
            double minVelocity = Math.Min(currVelocity, prevVelocity);

            // We give a bonus to the width of the angle.
            double angleBonus = calculateAngleSpline(Math.Abs(currAngle), false) * Math.Min(minVelocity, (currMovement + prevMovement).Length / Math.Max(currTime, prevTime));

            // Slightly nerf the wide angle buff if there is a sharp angle in between 2 wide angles.
            angleBonus *= 1 - 0.25 * calculateAngleSpline(Math.Abs(lastAngle), true) * calculateAngleSpline(Math.Abs(lastLastAngle), false);

            return angleBonus * multiplier;
        }

        public static double EvaluateVelocityChangeBonus(DifficultyHitObject current)
        {
            if (!IsValid(current, 3, 1))
                return 0;

            var osuNextObj = (OsuDifficultyHitObject)current.Next(0);
            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            Vector2 prevMovement = osuPrevObj.Movement;
            Vector2 currMovement = osuCurrObj.Movement;

            double currTime = osuCurrObj.StrainTime;
            double prevTime = osuPrevObj.StrainTime;

            double baseVelocityChange = Math.Max(0, Math.Abs(prevMovement.Length / prevTime - currMovement.Length / currTime) - currMovement.Length / currTime);

            double prevAngleBonus = CurveBuilder.BuildSmootherStep(osuPrevObj.Angle!.Value, (0, 0.4), (1.04, 0), (2.62, 1));
            double currAngleBonus = CurveBuilder.BuildSmootherStep(osuCurrObj.Angle!.Value, (0.52, 1), (2.09, 0), (3.14, 0.4));

            double angleBonus = 0.3 * prevAngleBonus + 0.7 * currAngleBonus;

            double overlapNerf = Math.Pow(Math.Clamp((osuCurrObj.RawMovement.Length - osuPrevObj.Radius / 1.5) / osuPrevObj.Radius, 0, 1), 2);

            double velChangeBonus = baseVelocityChange * ((1.3 + angleBonus) * overlapNerf);

            return velChangeBonus * multiplier;
        }

        private static double calculateAngleSpline(double angle, bool reversed)
        {
            if (reversed)
                return 1 - Math.Pow(Math.Sin(Math.Clamp(1.2 * angle - 5.4 * Math.PI / 12.0, 0, Math.PI / 2)), 2);

            return Math.Pow(Math.Sin(Math.Clamp(1.2 * angle - Math.PI / 4.0, 0, Math.PI / 2)), 2);
        }
    }
}
