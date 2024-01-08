// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.UnstableRateEstimator;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Difficulty.UnstableRateEstimation
{
    public class ManiaUnstableRateEstimator : UnstableRateEstimator
    {
        protected const double TAIL_DEVIATION_MULTIPLIER = 1.8;

        public ManiaUnstableRateEstimator(IRulesetInfo ruleset, DifficultyAttributes attributes)
            : base(ruleset, attributes)
        {
        }

        public override double? ComputeEstimatedUnstableRate(ScoreInfo score, bool withMisses = true)
        {
            SetStatistics(score);

            ManiaDifficultyAttributes maniaAttributes = (ManiaDifficultyAttributes)Attributes;
            bool isLegacyScore = score.Mods.Any(m => m is ManiaModClassic) && !Precision.DefinitelyBigger(TotalHits, maniaAttributes.NoteCount + maniaAttributes.HoldNoteCount);

            ManiaLegacyUnstableRateEstimator legacyUnstableRateEstimator = new ManiaLegacyUnstableRateEstimator(Ruleset, Attributes);
            ManiaDefaultUnstableRateEstimator defaultUnstableRateEstimator = new ManiaDefaultUnstableRateEstimator(Ruleset, Attributes);

            return isLegacyScore
                ? legacyUnstableRateEstimator.ComputeEstimatedUnstableRate(score, withMisses)
                : defaultUnstableRateEstimator.ComputeEstimatedUnstableRate(score, withMisses);
        }
    }
}
