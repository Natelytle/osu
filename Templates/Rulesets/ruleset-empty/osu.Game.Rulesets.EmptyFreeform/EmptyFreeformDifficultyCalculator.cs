// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.EmptyFreeform
{
    public class EmptyFreeformDifficultyCalculator : DifficultyCalculator
    {
        public EmptyFreeformDifficultyCalculator(IRulesetInfo ruleset)
            : base(ruleset)
        {
        }

        protected override (DifficultyAttributes, PerformanceAttributes) CreateAttributes(IBeatmap beatmap, Mod[] mods, ScoreInfo scoreInfo, Skill[] skills, double clockRate)
        {
            return (new DifficultyAttributes(mods, 0), null);
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate) => Enumerable.Empty<DifficultyHitObject>();

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate) => Array.Empty<Skill>();
    }
}
