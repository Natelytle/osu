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
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            // Base snap difficulty is velocity.
            double difficulty = EvaluateDistanceBonus(current) * 62;
            difficulty += EvaluateAgilityBonus(current) * 65;
            difficulty += EvaluateAngleBonus(current) * 65;
            // difficulty += EvaluateVelocityChangeBonus(current) * 65;

            return difficulty;
        }

        public static double EvaluateDistanceBonus(DifficultyHitObject current)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;

            // Base snap difficulty is velocity.
            double distanceBonus = osuCurrObj.Movement.Length / osuCurrObj.StrainTime;

            return distanceBonus;
        }

        public static double EvaluateAgilityBonus(DifficultyHitObject current)
        {
            if (!IsValid(current, 2))
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj0 = (OsuDifficultyHitObject)current.Previous(0);

            double currTime = osuCurrObj.StrainTime;
            double prevTime = osuPrevObj0.StrainTime;

            double currentAngle = osuCurrObj.Angle!.Value * 180 / Math.PI;
            double currDistanceRatio = osuCurrObj.Movement.Length / osuCurrObj.Radius;

            // We reward high bpm more for wider angles, but only when distance is over 0.5 radii.
            double baseBpm = 320.0 / (1 + 0.45 * Smootherstep(currentAngle, 0, 120) * Smootherstep(currDistanceRatio, 0.5, 1));

            // Agility bonus of 1 at base BPM.
            double agilityBonus = Math.Pow(MillisecondsToBPM(Math.Max(currTime, prevTime), 2) / baseBpm, 2.5);

            return agilityBonus;
        }

        public static double EvaluateAngleBonus(DifficultyHitObject current)
        {
            if (!IsValid(current, 3, 1))
                return 1;

            OsuDifficultyHitObject osuCurrObj = (OsuDifficultyHitObject)current;
            OsuDifficultyHitObject osuPrev0Obj = (OsuDifficultyHitObject)current.Previous(0);

            double currAngle = osuCurrObj.Angle!.Value * 180 / Math.PI;

            double currVelocity = osuCurrObj.Movement.Length / osuCurrObj.StrainTime;
            double prevDistanceRatio = osuPrev0Obj.Movement.Length / osuPrev0Obj.Radius;

            // Provisional angle bonus
            double angleBonus = Smootherstep(currAngle, 0, 180) * currVelocity * Smootherstep(prevDistanceRatio, 0.5, 1);

            return angleBonus;
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

            return velChangeBonus;
        }
    }
}
