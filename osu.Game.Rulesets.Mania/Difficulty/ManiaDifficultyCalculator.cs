﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.MathUtils;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaDifficultyCalculator : DifficultyCalculator
    {
        private const double star_scaling_factor = 0.018;

        private readonly IRulesetInfo ruleset;

        public override int Version => 20230817;

        public ManiaDifficultyCalculator(IRulesetInfo ruleset)
            : base(ruleset)
        {
            this.ruleset = ruleset;
        }

        private static int maxComboForObject(HitObject hitObject)
        {
            if (hitObject is HoldNote hold)
                return 1 + (int)((hold.EndTime - hold.StartTime) / 100);

            return 1;
        }

        protected override (DifficultyAttributes, PerformanceAttributes?) CreateAttributes(IBeatmap beatmap, Mod[] mods, ScoreInfo? scoreInfo, Skill[] skills, double clockRate)
        {
            DifficultyAttributes difficultyAttributes = createDifficultyAttributes(beatmap, mods, skills, clockRate);

            if (scoreInfo is null)
                return (difficultyAttributes, null);

            ManiaPerformanceCalculator performanceCalculator = new ManiaPerformanceCalculator();
            PerformanceAttributes performanceAttributes = performanceCalculator.CreatePerformanceAttributes(scoreInfo, difficultyAttributes);

            return (difficultyAttributes, performanceAttributes);
        }

        private DifficultyAttributes createDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            if (beatmap.HitObjects.Count == 0)
                return new ManiaDifficultyAttributes { Mods = mods };

            HitWindows hitWindows = new ManiaHitWindows();
            hitWindows.SetDifficulty(beatmap.Difficulty.OverallDifficulty);

            ManiaDifficultyAttributes attributes = new ManiaDifficultyAttributes
            {
                StarRating = skills[0].DifficultyValue() * star_scaling_factor,
                Mods = mods,
                // In osu-stable mania, rate-adjustment mods don't affect the hit window.
                // This is done the way it is to introduce fractional differences in order to match osu-stable for the time being.
                GreatHitWindow = Math.Ceiling((int)(getHitWindow300(mods, beatmap) * clockRate) / clockRate),
                MaxCombo = beatmap.HitObjects.Sum(maxComboForObject),
            };

            return attributes;
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            var sortedObjects = beatmap.HitObjects.ToArray();

            LegacySortHelper<HitObject>.Sort(sortedObjects, Comparer<HitObject>.Create((a, b) => (int)Math.Round(a.StartTime) - (int)Math.Round(b.StartTime)));

            List<DifficultyHitObject> objects = new List<DifficultyHitObject>();

            for (int i = 1; i < sortedObjects.Length; i++)
                objects.Add(new ManiaDifficultyHitObject(sortedObjects[i], sortedObjects[i - 1], clockRate, objects, objects.Count));

            return objects;
        }

        // Sorting is done in CreateDifficultyHitObjects, since the full list of hitobjects is required.
        protected override IEnumerable<DifficultyHitObject> SortObjects(IEnumerable<DifficultyHitObject> input) => input;

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate) => new Skill[]
        {
            new Strain(mods, ((ManiaBeatmap)Beatmap).TotalColumns)
        };

        protected override Mod[] DifficultyAdjustmentMods
        {
            get
            {
                var mods = new Mod[]
                {
                    new ManiaModDoubleTime(),
                    new ManiaModHalfTime(),
                    new ManiaModEasy(),
                    new ManiaModHardRock(),

                    // These are only for converts, but putting the beatmap in the constructor complicates things, so always leave them in.
                    new ManiaModKey1(),
                    new ManiaModKey2(),
                    new ManiaModKey3(),
                    new ManiaModKey4(),
                    new ManiaModKey5(),
                    new MultiMod(new ManiaModKey5(), new ManiaModDualStages()),
                    new ManiaModKey6(),
                    new MultiMod(new ManiaModKey6(), new ManiaModDualStages()),
                    new ManiaModKey7(),
                    new MultiMod(new ManiaModKey7(), new ManiaModDualStages()),
                    new ManiaModKey8(),
                    new MultiMod(new ManiaModKey8(), new ManiaModDualStages()),
                    new ManiaModKey9(),
                    new MultiMod(new ManiaModKey9(), new ManiaModDualStages()),
                };

                return mods;
            }
        }

        private double getHitWindow300(Mod[] mods, IBeatmap beatmap)
        {
            bool isForCurrentRuleset = beatmap.BeatmapInfo.Ruleset.MatchesOnlineID(ruleset);

            double originalOverallDifficulty = beatmap.BeatmapInfo.Difficulty.OverallDifficulty;

            if (isForCurrentRuleset)
            {
                double od = Math.Min(10.0, Math.Max(0, 10.0 - originalOverallDifficulty));
                return applyModAdjustments(34 + 3 * od, mods);
            }

            if (Math.Round(originalOverallDifficulty) > 4)
                return applyModAdjustments(34, mods);

            return applyModAdjustments(47, mods);

            static double applyModAdjustments(double value, Mod[] mods)
            {
                if (mods.Any(m => m is ManiaModHardRock))
                    value /= 1.4;
                else if (mods.Any(m => m is ManiaModEasy))
                    value *= 1.4;

                return value;
            }
        }
    }
}
