// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AimEvaluator
    {
        private static double snapMultiplier => 65;
        private static double flowMultiplier => 150;

        public static double EvaluateSnapDifficultyOf(DifficultyHitObject current)
        {
            if (current.Index <= 2 ||
                current.BaseObject is Spinner ||
                current.Previous(0).BaseObject is Spinner ||
                current.Previous(1).BaseObject is Spinner ||
                current.Previous(2).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj0 = (OsuDifficultyHitObject)current.Previous(0);
            var osuPrevObj1 = (OsuDifficultyHitObject)current.Previous(1);

            double currDistance = osuCurrObj.LazyJumpDistance;
            double prevDistance = osuPrevObj0.LazyJumpDistance;

            double currTime = osuCurrObj.StrainTime;
            double prevTime = osuPrevObj0.StrainTime;

            double currVelocity = currDistance / currTime;
            double prevVelocity = prevDistance / prevTime;
            double minVelocity = Math.Min(currVelocity, prevVelocity);

            // Base snap difficulty is distance / time.
            double difficulty = currDistance / currTime;

            // Add a bonus for agility.
            difficulty += 6000 / (Math.Max(25, Math.Max(osuCurrObj.StrainTime, osuPrevObj0.StrainTime) - 50) * Math.Max(osuCurrObj.StrainTime, osuPrevObj0.StrainTime));

            double angleBonus = 0;

            if (osuCurrObj.Angle != null && osuPrevObj0.Angle != null && osuPrevObj1.Angle != null)
            {
                double currAngle = osuCurrObj.Angle.Value;
                double lastAngle = osuPrevObj0.Angle.Value;
                double lastLastAngle = osuPrevObj1.Angle.Value;

                // We give a bonus to the width of the angle.
                angleBonus = calculateAngleSpline(Math.Abs(currAngle), false) * Math.Min(minVelocity, threeNoteDistanceAdd(currDistance, prevDistance, currAngle) / Math.Max(currTime, prevTime));

                // Slightly nerf the wide angle buff if there is a sharp angle in between 2 wide angles.
                angleBonus *= 1 - 0.25 * calculateAngleSpline(Math.Abs(lastAngle), true) * calculateAngleSpline(Math.Abs(lastLastAngle), false);
            }

            double velChangeBonus = Math.Max(0, Math.Min(Math.Abs(prevDistance / prevTime - currDistance / currTime) - minVelocity, Math.Max(50 / Math.Max(osuCurrObj.StrainTime, osuPrevObj0.StrainTime), minVelocity)));

            difficulty += velChangeBonus + angleBonus;

            return difficulty * snapMultiplier;
        }

        public static double EvaluateFlowDifficultyOf(DifficultyHitObject current, bool withSliderTravelDistance = false, double strainDecayBase = 0)
        {
            if (current.Index <= 2 ||
                current.BaseObject is Spinner ||
                current.Previous(0).BaseObject is Spinner ||
                current.Previous(1).BaseObject is Spinner ||
                current.Previous(2).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            double currDistance = osuCurrObj.LazyJumpDistance;
            double prevDistance = osuPrevObj.LazyJumpDistance;

            double currTime = osuCurrObj.StrainTime;

            // Base flow difficulty is distance / time, with a bit of time subtracted to buff speed flow.
            double difficulty = currDistance / (currTime - 12.5);

            double currVelocity = currDistance / osuCurrObj.StrainTime;
            double prevVelocity = prevDistance / osuPrevObj.StrainTime;
            double minVelocity = Math.Min(currVelocity, prevVelocity);

            double angleBonus = 0;

            if (osuCurrObj.Angle != null && osuPrevObj.Angle != null)
            {
                double currAngle = osuCurrObj.Angle.Value;
                double lastAngle = osuPrevObj.Angle.Value;

                double threeNoteVelocitySub = threeNoteDistanceSub(currDistance, prevDistance, currAngle) / Math.Max(osuCurrObj.StrainTime, osuPrevObj.StrainTime);

                // We reward for angle changes or the acuteness of the angle, whichever is higher. Possibly a case out there to reward both.
                angleBonus = Math.Max(
                    Math.Pow(Math.Sin((currAngle - lastAngle) / 2), 2) * minVelocity,
                    calculateAngleSpline(Math.Abs(currAngle), true) * Math.Min(minVelocity, threeNoteVelocitySub));
            }

            double velChangeBonus = Math.Abs(prevVelocity - currVelocity);

            difficulty += 0.65 * (velChangeBonus + angleBonus);

            return difficulty * flowMultiplier;
        }

        // Adds the vectors of the 2 notes movements.
        private static double threeNoteDistanceAdd(double a, double b, double angle) => Math.Sqrt(a * a + b * b - 2 * a * b * Math.Cos(Math.PI - angle));

        // Subtracts the vectors of the 2 notes movements.
        private static double threeNoteDistanceSub(double a, double b, double angle) => Math.Sqrt(a * a + b * b - 2 * a * b * Math.Cos(angle));

        private static double calculateAngleSpline(double angle, bool reversed)
        {
            if (reversed)
                return 1 - Math.Pow(Math.Sin(Math.Clamp(1.2 * angle - 5.4 * Math.PI / 12.0, 0, Math.PI / 2)), 2);

            return Math.Pow(Math.Sin(Math.Clamp(1.2 * angle - Math.PI / 4.0, 0, Math.PI / 2)), 2);
        }
    }
}
