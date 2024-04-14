// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators.PreviousNoteBonusDicts;
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
            var osuLastObj = (OsuDifficultyHitObject)current.Previous(0);

            // Calculate the velocity to the current hitobject, which starts with a base distance / time assuming the last object is a hitcircle.
            double currVelocity = osuCurrObj.Distance / osuCurrObj.StrainTime;

            double aimStrain = currVelocity; // Start strain with regular velocity.

            // Third last note bonus
            aimStrain *= ThirdLastNote.GetAngleBonus(osuCurrObj.Angle);

            aimStrain *= FourthLastNote.GetAngleBonus(osuCurrObj.Angle, osuLastObj.Angle);

            return aimStrain;
        }
    }
}
