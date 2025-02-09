// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using static osu.Game.Rulesets.Osu.Difficulty.Preprocessing.OsuDifficultyHitObject;
using static osu.Game.Rulesets.Difficulty.Utils.DifficultyCalculationUtils;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class FlowAimEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            // Base snap difficulty is velocity.
            double difficulty = EvaluateDistanceBonus(current) * 100;
            difficulty += EvaluateTappingBonus(current) * 40;
            difficulty += EvaluateAngleBonus(current) * 20;

            return difficulty;
        }

        public static double EvaluateDistanceBonus(DifficultyHitObject current)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;

            // Distance scales harder on flow aim. Technically incorrect, but I just want to see my family again.
            double distanceBonus = Math.Pow(osuCurrObj.Movement.Length / osuCurrObj.StrainTime, 2);

            return distanceBonus;
        }

        public static double EvaluateTappingBonus(DifficultyHitObject current)
        {
            if (!IsValid(current, 2))
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj0 = (OsuDifficultyHitObject)current.Previous(0);

            double currTime = osuCurrObj.StrainTime;
            double prevTime = osuPrevObj0.StrainTime;

            // Tapping bonus of 1 at 330 BPM.
            double tappingBonus = Math.Pow(MillisecondsToBPM(Math.Max(currTime, prevTime)) / 330, 2);

            return tappingBonus;
        }

        public static double EvaluateAngleBonus(DifficultyHitObject current)
        {
            if (!IsValid(current, 3, 1))
                return 1;

            OsuDifficultyHitObject osuCurrObj = (OsuDifficultyHitObject)current;
            OsuDifficultyHitObject osuPrev0Obj = (OsuDifficultyHitObject)current.Previous(0);

            double currAngle = osuCurrObj.Angle!.Value * 180 / Math.PI;

            double prevDistanceRatio = osuPrev0Obj.Movement.Length / osuPrev0Obj.Radius;

            // Provisional angle bonus
            double angleBonus = Smootherstep(currAngle, 0, 180) * (osuCurrObj.Movement.Length / osuCurrObj.StrainTime) * Smootherstep(prevDistanceRatio, 0.5, 1);

            return angleBonus;
        }
    }
}
