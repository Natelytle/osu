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
            // difficulty += EvaluateVelocityChangeBonus(current);

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
            if (!IsValid(current, 3, 1))
                return 1;

            OsuDifficultyHitObject osuCurrObj = (OsuDifficultyHitObject)current;
            OsuDifficultyHitObject osuPrev0Obj = (OsuDifficultyHitObject)current.Previous(0);
            OsuDifficultyHitObject osuPrev1Obj = (OsuDifficultyHitObject)current.Previous(1);
            OsuDifficultyHitObject osuNextObj = (OsuDifficultyHitObject)current.Next(0);

            double currAngle = osuCurrObj.Angle!.Value * 180 / Math.PI;
            double prev0Angle = osuPrev0Obj.Angle!.Value * 180 / Math.PI;
            double nextAngle = osuNextObj.Angle!.Value * 180 / Math.PI;

            double currVelocity = osuCurrObj.Movement.Length / osuCurrObj.StrainTime;
            double prev0Velocity = osuPrev0Obj.Movement.Length / osuPrev0Obj.StrainTime;
            double prev1Velocity = osuPrev1Obj.Movement.Length / osuPrev1Obj.StrainTime;
            double nextVelocity = osuNextObj.Movement.Length / osuNextObj.StrainTime;

            const double curr_multiplier = 1.0;
            const double prev_multiplier = 0.5;
            const double next_multiplier = 0.3;
            const double curr_prev_multiplier = 0.5;
            const double curr_next_multiplier = 0.0;

            double currAngleBonus = CurveBuilder.BuildLerp(currAngle, (35, 0), (45, 0.015), (60, 0.3), (90, 0.8), (105, 0.975), (115, 1)) * Math.Min(currVelocity, prev0Velocity);
            double prevAngleBonus = CurveBuilder.BuildLerp(prev0Angle, (35, 0), (45, 0.015), (60, 0.3), (90, 0.8), (105, 0.975), (115, 1)) * Math.Min(prev0Velocity, prev1Velocity);
            double nextAngleBonus = CurveBuilder.BuildLerp(nextAngle, (35, 0), (45, 0.015), (60, 0.2), (90, 0.8), (105, 0.975), (115, 1)) * Math.Min(currVelocity, nextVelocity);
            double currPrevAngleBonus = CurveBuilder.BuildLerp(currAngle + prev0Angle, (70, 0), (120, 0.3), (150, 0.5), (240, 1)) * Math.Min(Math.Min(currVelocity, prev0Velocity), prev1Velocity);
            double currNextAngleBonus = CurveBuilder.BuildLerp(nextAngle + currAngle, (35, 0), (45, 0.015), (60, 0.2), (90, 0.8), (105, 0.975), (115, 1)) * Math.Min(Math.Min(currVelocity, prev0Velocity), nextVelocity);

            double totalAngleBonus = currAngleBonus * curr_multiplier
                                     + prevAngleBonus * prev_multiplier
                                     + nextAngleBonus * next_multiplier
                                     + currPrevAngleBonus * curr_prev_multiplier
                                     + currNextAngleBonus * curr_next_multiplier;

            return totalAngleBonus * multiplier;
        }

        public static double EvaluateVelocityChangeBonus(DifficultyHitObject current)
        {
            if (!IsValid(current, 3))
                return 0;

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
