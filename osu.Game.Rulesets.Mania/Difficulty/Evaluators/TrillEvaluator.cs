// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class TrillEvaluator
    {
        private const double trill_nerf = 0.62864;
        private const double trill_run_ramp = 4.99947;

        /// <summary>
        /// Whether <paramref name="hitObject"/> alternates back into the column it was in two notes ago (e.g. a 1-2-1 pattern).
        /// </summary>
        public static bool IsTrillStep(ManiaDifficultyHitObject hitObject)
        {
            if (hitObject.Previous(0) is not ManiaDifficultyHitObject previous ||
                hitObject.Previous(1) is not ManiaDifficultyHitObject previous2)
                return false;

            return previous.Column != hitObject.Column && previous2.Column == hitObject.Column;
        }

        /// <summary>
        /// A multiplier that nerfs sustained trill runs, ramping down the longer the trill continues.
        /// </summary>
        public static double TrillFactor(ManiaDifficultyHitObject hitObject)
        {
            if (!IsTrillStep(hitObject))
                return 1.0;

            double ramp = Math.Max(1.0, trill_run_ramp);
            int cap = (int)Math.Ceiling(ramp) + 1;
            int run = 1;
            var current = hitObject;

            while (run < cap)
            {
                if (current.Previous(0) is not ManiaDifficultyHitObject previousNote || !IsTrillStep(previousNote))
                    break;

                run++;
                current = previousNote;
            }

            double t = DifficultyCalculationUtils.ReverseLerp(run - 1, 0.0, ramp);
            return 1.0 - (1.0 - trill_nerf) * t;
        }
    }
}
