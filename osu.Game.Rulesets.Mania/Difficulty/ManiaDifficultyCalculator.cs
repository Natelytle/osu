// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Utils.AccuracySimulation;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaDifficultyCalculator : DifficultyCalculator
    {
        private const double star_rating_accuracy = 0.97;

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

            HitWindows hitWindows = new ManiaHitWindows();
            hitWindows.SetDifficulty(beatmap.Difficulty.OverallDifficulty);

            var individualStrainSkill = skills.OfType<IndividualStrain>().Single();
            var overallStrainSkill = skills.OfType<OverallStrain>().Single();

            getCombinedStrainValues(individualStrainSkill, overallStrainSkill, out List<double> noteStrains, out List<double> tailStrains);

            AccuracySimulator accuracySimulator = new AccuracySimulator(mods, beatmap.Difficulty.OverallDifficulty, noteStrains, tailStrains);

            // Get the skill level at star rating's accuracy threshold
            double skillLevel = accuracySimulator.SkillLevelAtAccuracy(star_rating_accuracy);

            // We need a skill level for SS for the accuracy curve because all accuracy values are computed with fractions of SS skill
            double ssSkillLevel = accuracySimulator.SkillLevelAtAccuracy(1);
            double[] accuracyCurve = accuracySimulator.AccuracyCurve(ssSkillLevel);

            ManiaDifficultyAttributes attributes = new ManiaDifficultyAttributes
            {
                StarRating = skillLevel,
                AccuracyCurve = accuracyCurve,
                SSValue = ssSkillLevel,
                Mods = mods,
                MaxCombo = beatmap.HitObjects.Sum(maxComboForObject),
            };

            return attributes;
        }

        private static int maxComboForObject(HitObject hitObject)
        {
            if (hitObject is HoldNote hold)
                return 1 + (int)((hold.EndTime - hold.StartTime) / 100);

            return 1;
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            var sortedObjects = beatmap.HitObjects.OrderBy(a => a.StartTime).ToArray();
            int totalColumns = ((ManiaBeatmap)beatmap).TotalColumns;

            List<DifficultyHitObject> objects = new List<DifficultyHitObject>();
            List<DifficultyHitObject>[] perColumnObjects = new List<DifficultyHitObject>[totalColumns];

            for (int column = 0; column < totalColumns; column++)
                perColumnObjects[column] = new List<DifficultyHitObject>();

            for (int i = 1; i < sortedObjects.Length; i++)
            {
                var currentObject = new ManiaDifficultyHitObject(sortedObjects[i], sortedObjects[i - 1], clockRate, objects, perColumnObjects, objects.Count);
                objects.Add(currentObject);
                perColumnObjects[currentObject.Column].Add(currentObject);
            }

            return objects;
        }

        // Sorting is done in CreateDifficultyHitObjects, since the full list of hitobjects is required.
        protected override IEnumerable<DifficultyHitObject> SortObjects(IEnumerable<DifficultyHitObject> input) => input;

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate) => new Skill[]
        {
            new IndividualStrain(mods, ((ManiaBeatmap)Beatmap).TotalColumns),
            new OverallStrain(mods)
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

                // if we are a convert, we can be played in any key mod.
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

        private void getCombinedStrainValues(IndividualStrain individual, OverallStrain overall, out List<double> noteStrains, out List<double> tailStrains)
        {
            List<NestedObjectDifficultyInfo> individualDifficulties = individual.GetStrainValues();
            List<NestedObjectDifficultyInfo> overallDifficulties = overall.GetStrainValues();

            noteStrains = new List<double>();
            tailStrains = new List<double>();

            for (int i = 0; i < individualDifficulties.Count; i++)
            {
                double combinedDifficulty = DifficultyCalculationUtils.Norm(2, individualDifficulties[i].Difficulty, overallDifficulties[i].Difficulty);

                if (individualDifficulties[i].IsTail)
                    tailStrains.Add(combinedDifficulty);
                else
                    noteStrains.Add(combinedDifficulty);
            }
        }
    }
}
