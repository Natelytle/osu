// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
        private const double difficulty_multiplier = 1.0;

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

            var strain = (Strain)skills[0];

            double starRating = strain.DifficultyValue();
            double[] accuracySkillLevels = strain.AccuracyCurve();

            ManiaDifficultyAttributes attributes = new ManiaDifficultyAttributes
            {
                StarRating = starRating,
                AccuracySkillLevels = accuracySkillLevels,
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
            // We want to split LNs into their heads and tails.
            List<HitObject> nestedHitObjects = new List<HitObject>();

            foreach (var obj in beatmap.HitObjects)
            {
                if (obj.NestedHitObjects.Count == 0)
                    nestedHitObjects.Add(obj);
                else
                {
                    nestedHitObjects.Add(obj.NestedHitObjects.First());
                    nestedHitObjects.Add(obj.NestedHitObjects.Last());
                }
            }

            // Order notes by start time, then by column left to right.
            var sortedObjects = nestedHitObjects.OrderBy(obj => obj.StartTime).ThenBy(obj => ((ManiaHitObject)obj).Column).ToList();

            int columns = ((ManiaBeatmap)beatmap).TotalColumns;

            List<DifficultyHitObject> objects = new List<DifficultyHitObject>();
            List<DifficultyHitObject>[] perColumnObjects = new List<DifficultyHitObject>[columns];

            for (int column = 0; column < columns; column++)
                perColumnObjects[column] = new List<DifficultyHitObject>();

            // Since we can only view previous objects, we need to temporarily store objects when we want to edit a property to be a next object.
            List<ManiaDifficultyHitObject> currentTimeObjects = new List<ManiaDifficultyHitObject>();
            // List<ManiaDifficultyHitObject> notesSinceLastLongNote = new List<ManiaDifficultyHitObject>();

            int longNoteIndex = 0;

            for (int i = 1; i < sortedObjects.Count; i++)
            {
                var currentObject = new ManiaDifficultyHitObject(sortedObjects[i], sortedObjects[i - 1], clockRate, objects, perColumnObjects, objects.Count, longNoteIndex);
                objects.Add(currentObject);
                currentTimeObjects.Add(currentObject);
                // notesSinceLastLongNote.Add(currentObject);
                perColumnObjects[currentObject.Column].Add(currentObject);

                if (currentObject.BaseObject is HeadNote)
                {
                    longNoteIndex += 1;

                    /* foreach (ManiaDifficultyHitObject previousNote in notesSinceLastLongNote)
                    {
                        previousNote.NextLongNote = currentObject;
                    }

                    notesSinceLastLongNote.Clear();*/
                }

                // Update the current objects of every note once we've processed every note in this chord.
                if (i + 1 == sortedObjects.Count || sortedObjects[i].StartTime != sortedObjects[i + 1].StartTime)
                {
                    foreach (ManiaDifficultyHitObject previousNote in currentTimeObjects)
                    {
                        foreach (ManiaDifficultyHitObject concurrentObj in currentTimeObjects)
                            previousNote.ConcurrentHitObjects[concurrentObj.Column] = concurrentObj;
                    }

                    currentTimeObjects.Clear();
                }
            }

            return objects;
        }

        // Sorting is done in CreateDifficultyHitObjects, since the full list of hitobjects is required.
        protected override IEnumerable<DifficultyHitObject> SortObjects(IEnumerable<DifficultyHitObject> input) => input;

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate) => new Skill[]
        {
            new Strain(mods, beatmap.Difficulty.OverallDifficulty)
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
    }
}
