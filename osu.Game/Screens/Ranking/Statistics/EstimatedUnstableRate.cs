// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Scoring;

namespace osu.Game.Screens.Ranking.Statistics
{
    public partial class EstimatedUnstableRate : SimpleStatisticItem<double?>
    {
        /// <summary>
        /// Creates and computes an <see cref="UnstableRate"/> statistic.
        /// </summary>
        /// <param name="score">The <see cref="ScoreInfo"/> to estimate the UR with.</param>
        /// <param name="playableBeatmap">The <see cref="IBeatmap"/> in which the ScoreInfo is applied to.</param>
        public EstimatedUnstableRate(ScoreInfo score, IBeatmap playableBeatmap)
            : base("Estimated Unstable Rate")
        {
            Ruleset ruleset = score.Ruleset.CreateInstance();

            IWorkingBeatmap workingBeatmap = new FlatWorkingBeatmap(playableBeatmap);

            var calculator = ruleset.CreateDifficultyCalculator(workingBeatmap);
            var attributes = calculator.Calculate(score.Mods);

            var estimator = ruleset.CreateUnstableRateEstimator(attributes);

            Value = estimator.ComputeEstimatedUnstableRate(score, false);
        }

        protected override string DisplayValue(double? value) => value == null ? "(not available)" : value.Value.ToString(@"N2");
    }
}
