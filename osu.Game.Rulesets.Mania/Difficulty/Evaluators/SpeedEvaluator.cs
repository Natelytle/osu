// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class SpeedEvaluator
    {
        private const double tap_rate_offset = 0.030;
        private const double jack_speed_nerf = 0.49996;
        private const double speed_scale = 1.39127;

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject hitObject)
        {
            if (hitObject.DeltaTime < ChordEvaluator.CHORD_TOLERANCE_MS)
                return 0.0;

            double tapRate = 1.0 / (hitObject.DeltaTime / 1000.0 + tap_rate_offset);

            bool isJack = hitObject.Previous(0) is ManiaDifficultyHitObject previous && previous.Column == hitObject.Column;

            double patternMultiplier = isJack
                ? jack_speed_nerf
                : TrillEvaluator.TrillFactor(hitObject);

            return tapRate * patternMultiplier * speed_scale * hitObject.ManipulationFactor * hitObject.StaminaFactor;
        }
    }
}
