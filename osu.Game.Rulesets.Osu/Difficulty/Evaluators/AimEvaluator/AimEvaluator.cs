// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.AimEvaluator
{
    public static class AimEvaluator
    {
        /// <summary>
        /// Evaluates the difficulty of aiming the current object, based on:
        /// <list type="bullet">
        /// <item><description>cursor velocity to the current object,</description></item>
        /// <item><description>angle difficulty,</description></item>
        /// <item><description>sharp velocity increases,</description></item>
        /// <item><description>and slider difficulty.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.Index <= 1 || current.BaseObject is Spinner || current.Previous(0).BaseObject is Spinner || current.Previous(1).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            // Calculate the velocity to the current hitobject, which starts with a base distance / time assuming the last object is a hitcircle.
            double currVelocity = osuCurrObj.Distance / osuCurrObj.StrainTime;
            double prevVelocity = osuPrevObj.Distance / osuPrevObj.StrainTime;

            double aimStrain = currVelocity; // Start strain with regular velocity.

            if (osuCurrObj.Angle is null || currVelocity == 0) return aimStrain;

            double velocityIndex = Math.Clamp(Math.Pow(2, prevVelocity / currVelocity - 3), 0, 6);
            double angleIndex = 6 / Math.PI * osuCurrObj.Angle.Value;

            // Multiply in the bonus for the previous note position
            aimStrain *= lerpMatrix(AimBonusMatrices.LAST_NOTE_POSITION_BONUS, velocityIndex, angleIndex);

            return aimStrain;
        }

        private static double lerpMatrix(double[,] matrix, double index1, double index2)
        {
            int index1Lower = (int)index1;
            int index1Higher = (int)Math.Ceiling(index1);
            double t1 = index1 - index1Lower;

            int index2Lower = (int)index2;
            int index2Higher = (int)Math.Ceiling(index2);
            double t2 = index2 - index2Lower;

            // Take the weighted average of all 4
            return matrix[index1Lower, index2Lower] * t1 * t2 +
                   matrix[index1Higher, index2Lower] * (1 - t1) * t2 +
                   matrix[index1Lower, index2Higher] * t1 * (1 - t2) +
                   matrix[index1Higher, index2Higher] * (1 - t1) * (1 - t2);
        }
    }
}
