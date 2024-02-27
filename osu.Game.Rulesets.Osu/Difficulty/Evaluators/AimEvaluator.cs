// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
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
        public static double EvaluateSnapDifficultyOf(DifficultyHitObject current, bool withSliderTravelDistance = false, double strainDecayBase = 0)
        {
            if (current.Index <= 2 ||
                current.BaseObject is Spinner ||
                current.Previous(0).BaseObject is Spinner ||
                current.Previous(1).BaseObject is Spinner ||
                current.Previous(2).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLastObj0 = (OsuDifficultyHitObject)current.Previous(0);

            //////////////////////// CIRCLE SIZE /////////////////////////
            double linearDifficulty = 32.0 / osuCurrObj.Radius;

            var currMovement = osuCurrObj.Movement;
            var prevMovement = osuLastObj0.Movement;
            double currTime = osuCurrObj.MovementTime;

            if (!withSliderTravelDistance)
            {
                currMovement = osuCurrObj.SliderlessMovement;
                prevMovement = osuLastObj0.SliderlessMovement;
                currTime = osuCurrObj.StrainTime;
            }

            // Base snap difficulty is distance / time.
            double snapDifficulty = linearDifficulty * currMovement.Length / currTime;

            // Add a bonus for agility.
            snapDifficulty += 4000 / (Math.Max(25, Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime) - 25) * Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime));

            // Begin angle and weird rewards.
            double currVelocity = currMovement.Length / osuCurrObj.StrainTime;
            double prevVelocity = prevMovement.Length / osuLastObj0.StrainTime;

            double snapAngle = 0;

            if (osuCurrObj.Angle != null)
            {
                double currAngle = osuCurrObj.Angle.Value;

                // We give a bonus to the width of the angle.
                snapAngle = linearDifficulty * calculateAngleSpline(Math.Abs(currAngle), false) * Math.Min(Math.Min(currVelocity, prevVelocity), (currMovement + prevMovement).Length / Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime));
            }

            double snapVelChange = linearDifficulty * Math.Max(0, Math.Min(Math.Abs(prevVelocity - currVelocity) - Math.Min(currVelocity, prevVelocity), Math.Max(osuCurrObj.Radius / Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime), Math.Min(currVelocity, prevVelocity))));

            snapDifficulty += snapVelChange + snapAngle;

            // Slider stuff.
            // double sustainedSliderStrain = 0.0;

            // if (osuCurrObj.SliderSubObjects.Count != 0)
            //     sustainedSliderStrain = calculateSustainedSliderStrain(osuCurrObj, strainDecayBase, withSliderTravelDistance);

            // Apply slider strain with constant adjustment
            // snapDifficulty += 2.0 * sustainedSliderStrain;

            return snapDifficulty;
        }

        /// <summary>
        /// Evaluates the difficulty of aiming the current object, based on:
        /// <list type="bullet">
        /// <item><description>cursor velocity to the current object,</description></item>
        /// <item><description>angle difficulty,</description></item>
        /// <item><description>sharp velocity increases,</description></item>
        /// <item><description>and slider difficulty.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateFlowDifficultyOf(DifficultyHitObject current, bool withSliderTravelDistance = false, double strainDecayBase = 0)
        {
            if (current.Index <= 2 ||
                current.BaseObject is Spinner ||
                current.Previous(0).BaseObject is Spinner ||
                current.Previous(1).BaseObject is Spinner ||
                current.Previous(2).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuLastObj0 = (OsuDifficultyHitObject)current.Previous(0);

            //////////////////////// CIRCLE SIZE /////////////////////////
            double linearDifficulty = 32.0 / osuCurrObj.Radius;

            var currMovement = osuCurrObj.Movement;
            var prevMovement = osuLastObj0.Movement;
            double currTime = osuCurrObj.MovementTime;

            if (!withSliderTravelDistance)
            {
                currMovement = osuCurrObj.SliderlessMovement;
                prevMovement = osuLastObj0.SliderlessMovement;
                currTime = osuCurrObj.StrainTime;
            }

            // Base flow difficulty is distance / time.
            double flowDifficulty = linearDifficulty * currMovement.Length / (currTime - 12.5);

            // Begin angle and weird rewards.
            double currVelocity = currMovement.Length / osuCurrObj.StrainTime;
            double prevVelocity = prevMovement.Length / osuLastObj0.StrainTime;

            double flowAngle = 0;

            if (osuCurrObj.Angle != null && osuLastObj0.Angle != null)
            {
                double currAngle = osuCurrObj.Angle.Value;
                double lastAngle = osuLastObj0.Angle.Value;

                // We reward for angle changes or the acuteness of the angle, whichever is higher. Possibly a case out there to reward both.
                flowAngle = linearDifficulty * Math.Max(Math.Pow(Math.Sin((currAngle - lastAngle) / 2), 2) * Math.Min(currVelocity, prevVelocity),
                    calculateAngleSpline(Math.Abs(currAngle), true) * Math.Min(Math.Min(currVelocity, prevVelocity), (currMovement - prevMovement).Length / Math.Max(osuCurrObj.StrainTime, osuLastObj0.StrainTime)));
            }

            double flowVelChange = linearDifficulty * Math.Abs(prevVelocity - currVelocity);

            flowDifficulty += 0.65 * (flowVelChange + flowAngle);

            // Slider stuff.
            // double sustainedSliderStrain = 0.0;

            // if (osuCurrObj.SliderSubObjects.Count != 0)
            //     sustainedSliderStrain = calculateSustainedSliderStrain(osuCurrObj, strainDecayBase, withSliderTravelDistance);

            // Apply slider strain with constant adjustment
            // flowDifficulty += 2.0 * sustainedSliderStrain;
            // snapDifficulty += 2.0 * sustainedSliderStrain;

            return flowDifficulty;
        }

        private static double calculateSustainedSliderStrain(OsuDifficultyHitObject osuCurrObj, double strainDecayBase, bool withSliderTravelDistance)
        {
            int index = 1;

            double sliderRadius = 2.4 * osuCurrObj.Radius;
            double linearDifficulty = 32.0 / osuCurrObj.Radius;

            var previousHistoryVector = new Vector2(0,0);
            var historyVector = new Vector2(0,0);
            var priorMinimalPos = new Vector2(0,0);
            double historyTime = 0;
            double historyDistance = 0;

            double peakStrain = 0;
            double currentStrain = 0;

            foreach (var subObject in osuCurrObj.SliderSubObjects)
            {
                if (index == osuCurrObj.SliderSubObjects.Count && !withSliderTravelDistance)
                    break;

                double noteStrain = 0;

                // if (index == 0 && osuCurrObj.SliderSubObjects.Count > 1)
                //     noteStrain = Math.Max(0, linearDifficulty * subObject.Movement.Length - 2 * osuCurrObj.Radius) / subObject.StrainTime;

                historyVector += subObject.Movement;
                historyTime += subObject.StrainTime;
                historyDistance += subObject.Movement.Length;

                if ((historyVector - priorMinimalPos).Length > sliderRadius)
                {
                    double angleBonus = Math.Min(Math.Min(previousHistoryVector.Length, historyVector.Length), Math.Min((previousHistoryVector - historyVector).Length, (previousHistoryVector + historyVector).Length));

                    noteStrain += linearDifficulty * (historyDistance + angleBonus - sliderRadius) / historyTime;

                    previousHistoryVector = historyVector;
                    priorMinimalPos = Vector2.Multiply(historyVector, (float) - sliderRadius / historyVector.Length);
                    historyVector = new Vector2(0,0);
                    historyTime = 0;
                    historyDistance = 0;
                }

                currentStrain *= Math.Pow(strainDecayBase, subObject.StrainTime / 1000.0); // TODO bug here using strainTime.
                currentStrain += noteStrain;
                peakStrain = Math.Max(peakStrain, currentStrain);

                index += 1;
            }

            if (historyTime > 0 && withSliderTravelDistance)
                currentStrain += Math.Max(0, linearDifficulty * Math.Max(0, historyVector.Length - 2 * osuCurrObj.Radius) / historyTime);

            return Math.Max(currentStrain, peakStrain);
        }

        private static double calculateAngleSpline(double angle, bool reversed)
        {
            if (reversed)
                return 1 - Math.Pow(Math.Sin(Math.Clamp(1.2 * angle - 5.4 * Math.PI / 12.0, 0, Math.PI / 2)), 2);

            return Math.Pow(Math.Sin(Math.Clamp(1.2 * angle - Math.PI / 4.0, 0, Math.PI / 2)), 2);
        }
    }
}
