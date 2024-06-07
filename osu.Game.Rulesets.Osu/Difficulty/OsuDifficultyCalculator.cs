// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuDifficultyCalculator : DifficultyCalculator
    {
        public override int Version => 20220902;

        public OsuDifficultyCalculator(IRulesetInfo ruleset)
            : base(ruleset)
        {
        }

        protected override (DifficultyAttributes, PerformanceAttributes?) CreateAttributes(IBeatmap beatmap, Mod[] mods, ScoreInfo? scoreInfo, Skill[] skills, double clockRate)
        {
            scoreInfo ??= simulatePerfectPerformance(beatmap);

            return (new DifficultyAttributes(), new PerformanceAttributes());
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            throw new NotImplementedException();
        }

        // We don't have skills!!!!!
        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate) => [];

        protected override Mod[] DifficultyAdjustmentMods =>
        [
        ];

        private ScoreInfo simulatePerfectPerformance(IBeatmap playableBeatmap)
        {
            ScoreInfo perfectPlay = new ScoreInfo
            {
                Accuracy = 1,
                Passed = true,
                MaxCombo = playableBeatmap.HitObjects.SelectMany(getPerfectHitResults).Count(r => r.AffectsCombo())
            };

            // create statistics assuming all hit objects have perfect hit result
            var statistics = playableBeatmap.HitObjects
                                            .SelectMany(getPerfectHitResults)
                                            .GroupBy(hr => hr, (hr, list) => (hitResult: hr, count: list.Count()))
                                            .ToDictionary(pair => pair.hitResult, pair => pair.count);
            perfectPlay.Statistics = statistics;
            perfectPlay.MaximumStatistics = statistics;

            return perfectPlay;

            IEnumerable<HitResult> getPerfectHitResults(HitObject hitObject)
            {
                foreach (HitObject nested in hitObject.NestedHitObjects)
                    yield return nested.Judgement.MaxResult;

                yield return hitObject.Judgement.MaxResult;
            }
        }
    }
}
