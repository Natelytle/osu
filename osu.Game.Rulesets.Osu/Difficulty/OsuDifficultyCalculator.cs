// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuDifficultyCalculator : DifficultyCalculator
    {
        private const double aim_multiplier = 0.641;
        private const double tap_multiplier = 0.641;
        private const double finger_control_multiplier = 1.245;

        private const double star_rating_exponent = 0.83;

        public override int Version => 20220902;

        public OsuDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            if (beatmap.HitObjects.Count == 0)
                return new OsuDifficultyAttributes { Mods = mods };

            List<OsuHitObject> hitObjects = beatmap.HitObjects.Cast<OsuHitObject>().ToList();

            double mapLength = beatmap.CalculatePlayableLength() / 1000.0 / clockRate;

            double preemptNoClockRate;
            if (beatmap.BeatmapInfo.Difficulty.ApproachRate > 5)
                preemptNoClockRate = 1200 - 150 * (beatmap.BeatmapInfo.Difficulty.ApproachRate - 5);
            else
                preemptNoClockRate = 1800 - 100 * beatmap.BeatmapInfo.Difficulty.ApproachRate;

            double[] noteDensities = NoteDensity.Calculate(hitObjects, preemptNoClockRate);

            // Tap
            var tapAttributes = ((Tap)skills[0]).CalculateTapAttributes();

            // Aim
            var aimAttributes = ((Aim)skills[1]).CalculateAimAttributes(tapAttributes.StrainHistory, noteDensities);

            // Finger Control
            double fingerControlDiff = skills[2].DifficultyValue();

            double tapStarRating = tap_multiplier * Math.Pow(tapAttributes.TapDifficulty, star_rating_exponent);
            double aimStarRating = aim_multiplier * Math.Pow(aimAttributes.FcProbabilityThroughput, star_rating_exponent);
            double fingerControlStarRating = finger_control_multiplier * Math.Pow(fingerControlDiff, star_rating_exponent);
            double combinedStarRating = PowerMean.Of(new[] { tapStarRating, aimStarRating, fingerControlStarRating }, 7) * 1.131;

            double preempt = IBeatmapDifficultyInfo.DifficultyRange(beatmap.Difficulty.ApproachRate, 1800, 1200, 450) / clockRate;
            int maxCombo = beatmap.GetMaxCombo();

            int hitCirclesCount = beatmap.HitObjects.Count(h => h is HitCircle);
            int sliderCount = beatmap.HitObjects.Count(h => h is Slider);
            int spinnerCount = beatmap.HitObjects.Count(h => h is Spinner);

            HitWindows hitWindows = new OsuHitWindows();
            hitWindows.SetDifficulty(beatmap.Difficulty.OverallDifficulty);

            double hitWindowGreat = hitWindows.WindowFor(HitResult.Great) / clockRate;

            OsuDifficultyAttributes attributes = new OsuDifficultyAttributes
            {
                StarRating = combinedStarRating,
                Mods = mods,
                Length = mapLength,

                TapStarRating = tapStarRating,
                TapDifficulty = tapAttributes.TapDifficulty,
                StreamNoteCount = tapAttributes.StreamNoteCount,
                MashTapDifficulty = tapAttributes.MashedTapDifficulty,

                FingerControlStarRating = fingerControlStarRating,
                FingerControlDifficulty = fingerControlDiff,

                AimStarRating = aimStarRating,
                AimDifficulty = aimAttributes.FcProbabilityThroughput,
                AimHiddenFactor = aimAttributes.HiddenFactor,
                ComboThroughputs = aimAttributes.ComboThroughputs,
                MissThroughputs = aimAttributes.MissThroughputs,
                MissCounts = aimAttributes.MissCounts,
                CheeseNoteCount = aimAttributes.CheeseNoteCount,
                CheeseLevels = aimAttributes.CheeseLevels,
                CheeseFactors = aimAttributes.CheeseFactors,

                ApproachRate = preempt > 1200 ? (1800 - preempt) / 120 : (1200 - preempt) / 150 + 5,
                OverallDifficulty = (80 - hitWindowGreat) / 6,
                MaxCombo = maxCombo,
                HitCircleCount = hitCirclesCount,
                SliderCount = sliderCount,
                SpinnerCount = spinnerCount
            };

            return attributes;
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            List<DifficultyHitObject> objects = new List<DifficultyHitObject>();

            // The first jump is formed by the first two hitobjects of the map.
            // If the map has less than two OsuHitObjects, the enumerator will not return anything.
            for (int i = 1; i < beatmap.HitObjects.Count; i++)
            {
                objects.Add(new OsuDifficultyHitObject(beatmap.HitObjects[i], beatmap.HitObjects[i - 1], clockRate, objects, objects.Count));
            }

            return objects;
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate)
        {
            return new Skill[]
            {
                new Tap(mods, clockRate),
                new Aim(mods, clockRate),
                new FingerControl(mods, clockRate),
            };
        }

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new OsuModTouchDevice(),
            new OsuModDoubleTime(),
            new OsuModHalfTime(),
            new OsuModEasy(),
            new OsuModHardRock(),
            new OsuModFlashlight(),
            new MultiMod(new OsuModFlashlight(), new OsuModHidden())
        };
    }
}
