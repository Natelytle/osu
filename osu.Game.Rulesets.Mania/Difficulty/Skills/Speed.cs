// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Speed : ManiaSkill
    {
        private const double strain_decay_base = 0.05007;
        private const double tap_rate_offset = 0.030;
        private const double jack_speed_nerf = 0.49996;
        private const double speed_scale = 1.39127;

        public Speed(Mod[] mods, int totalColumns)
            : base(mods, totalColumns)
        {
        }

        protected override double StrainDecayBase => strain_decay_base;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            var hitObject = (ManiaDifficultyHitObject)current;

            if (hitObject.DeltaTime < CHORD_TOLERANCE)
            {
                UpdateColumnState(hitObject);
                return 0.0;
            }

            double tapRate = 1.0 / (hitObject.DeltaTime / 1000.0 + tap_rate_offset);

            bool isJack = hitObject.Previous(0) is ManiaDifficultyHitObject previous && previous.Column == hitObject.Column;

            double patternMultiplier = isJack
                ? jack_speed_nerf
                : TrillFactor(hitObject);

            UpdateColumnState(hitObject);
            return tapRate * patternMultiplier * speed_scale;
        }
    }
}
