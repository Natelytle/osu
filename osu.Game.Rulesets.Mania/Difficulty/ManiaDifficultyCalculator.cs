// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
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
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaDifficultyCalculator : DifficultyCalculator
    {
        private const double difficulty_multiplier = 1.12;

        private readonly bool isForCurrentRuleset;

        public override int Version => 20241007;

        public ManiaDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
            isForCurrentRuleset = beatmap.BeatmapInfo.Ruleset.MatchesOnlineID(ruleset);
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            if (beatmap.HitObjects.Count == 0)
                return new ManiaDifficultyAttributes { Mods = mods };

            var totalSkill = (Strain)skills.First(s => s is Strain);

            double baseDifficulty = totalSkill.SkillAtAccuracy(0.95) * difficulty_multiplier;
            // double sunnyDifficulty = totalSkill.DifficultyValue() * 0.975;

            double ssDifficulty = totalSkill.SkillAtAccuracy(1);
            double a9 = totalSkill.AccuracyAtSkill(ssDifficulty * 0.9);
            double a8 = totalSkill.AccuracyAtSkill(ssDifficulty * 0.8);
            double a7 = totalSkill.AccuracyAtSkill(ssDifficulty * 0.7);
            double a6 = totalSkill.AccuracyAtSkill(ssDifficulty * 0.6);
            double a5 = totalSkill.AccuracyAtSkill(ssDifficulty * 0.5);
            double a4 = totalSkill.AccuracyAtSkill(ssDifficulty * 0.4);
            double a3 = totalSkill.AccuracyAtSkill(ssDifficulty * 0.3);
            double a2 = totalSkill.AccuracyAtSkill(ssDifficulty * 0.2);
            double a1 = totalSkill.AccuracyAtSkill(ssDifficulty * 0.1);

            double weightedNoteCount = totalSkill.GetWeightedNoteCount();
            double shortMapNerf = weightedNoteCount / (weightedNoteCount + 60.0);

            double starRating = baseDifficulty * shortMapNerf;
            double starRatingSS = ssDifficulty * shortMapNerf;

            return new ManiaDifficultyAttributes
            {
                StarRating = starRating,
                StarRatingSS = starRatingSS,
                AccuracyAt90PercentSkill = a9,
                AccuracyAt80PercentSkill = a8,
                AccuracyAt70PercentSkill = a7,
                AccuracyAt60PercentSkill = a6,
                AccuracyAt50PercentSkill = a5,
                AccuracyAt40PercentSkill = a4,
                AccuracyAt30PercentSkill = a3,
                AccuracyAt20PercentSkill = a2,
                AccuracyAt10PercentSkill = a1,
                Mods = mods,
                MaxCombo = beatmap.HitObjects.Sum(maxComboForObject),
            };
        }

        private static int maxComboForObject(HitObject hitObject)
        {
            if (hitObject is HoldNote hold)
                return 1 + (int)((hold.EndTime - hold.StartTime) / 100);

            return 1;
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            var sortedObjects = beatmap.HitObjects.ToArray();
            int totalColumns = ((ManiaBeatmap)beatmap).TotalColumns;

            LegacySortHelper<HitObject>.Sort(sortedObjects,
                Comparer<HitObject>.Create((a, b) => (int)Math.Round(a.StartTime) - (int)Math.Round(b.StartTime)));

            var objects = new List<DifficultyHitObject>();
            List<DifficultyHitObject>[] perColumnObjects = new List<DifficultyHitObject>[totalColumns];

            for (int column = 0; column < totalColumns; column++)
                perColumnObjects[column] = new List<DifficultyHitObject>();

            for (int i = 1; i < sortedObjects.Length; i++)
            {
                var maniaDifficultyHitObject = new ManiaDifficultyHitObject(
                    sortedObjects[i],
                    sortedObjects[i - 1],
                    clockRate,
                    objects,
                    perColumnObjects,
                    objects.Count
                );

                objects.Add(maniaDifficultyHitObject);
                perColumnObjects[maniaDifficultyHitObject.Column].Add(maniaDifficultyHitObject);
            }

            ManiaDifficultyPreprocessor.ProcessAndAssign(objects, beatmap);

            return objects;
        }

        protected override IEnumerable<DifficultyHitObject> SortObjects(IEnumerable<DifficultyHitObject> input) => input;

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate) => new Skill[]
        {
            new Strain(mods),
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
                };

                if (isForCurrentRuleset)
                    return mods;

                return mods.Concat(new Mod[]
                {
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
                }).ToArray();
            }
        }
    }
}
