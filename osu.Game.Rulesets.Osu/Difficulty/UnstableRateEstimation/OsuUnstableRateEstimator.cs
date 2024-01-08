// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.UnstableRateEstimator;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty.UnstableRateEstimation
{
    public class OsuUnstableRateEstimator : UnstableRateEstimator
    {
        public OsuUnstableRateEstimator(IRulesetInfo ruleset, DifficultyAttributes attributes)
            : base(ruleset, attributes)
        {
        }

        public override double? ComputeEstimatedUnstableRate(ScoreInfo score, bool withMisses = true)
        {
            bool isLegacyAccuracy = score.Mods.Any(m => m is OsuModClassic classic && classic.NoSliderHeadAccuracy.Value);

            OsuLegacyUnstableRateEstimator legacyUnstableRateEstimator = new OsuLegacyUnstableRateEstimator(Ruleset, Attributes);
            OsuDefaultUnstableRateEstimator defaultUnstableRateEstimator = new OsuDefaultUnstableRateEstimator(Ruleset, Attributes);

            return isLegacyAccuracy
                ? legacyUnstableRateEstimator.ComputeEstimatedUnstableRate(score, withMisses)
                : defaultUnstableRateEstimator.ComputeEstimatedUnstableRate(score, withMisses);
        }
    }
}
