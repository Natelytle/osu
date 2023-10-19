// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuDifficultyAttributes : DifficultyAttributes
    {
        /// <summary>
        /// The star rating corresponding to the aim skill.
        /// </summary>
        [JsonProperty("aim_star_rating")]
        public double AimStarRating { get; set; }

        /// <summary>
        /// The difficulty corresponding to the aim skill.
        /// </summary>
        [JsonProperty("aim_difficulty")]
        public double AimDifficulty { get; set; }

        /// <summary>
        /// Something to do with hidden and aim.
        /// </summary>
        [JsonProperty("aim_hidden_factor")]
        public double AimHiddenFactor { get; set; }

        /// <summary>
        /// The difficulty corresponding to the aim skill.
        /// </summary>
        [JsonProperty("combo_throughputs")]
        public double[] ComboThroughputs { get; set; } = Array.Empty<double>();

        /// <summary>
        /// The difficulty corresponding to the aim skill.
        /// </summary>
        [JsonProperty("miss_throughputs")]
        public double[] MissThroughputs { get; set; } = Array.Empty<double>();

        /// <summary>
        /// The difficulty corresponding to the aim skill.
        /// </summary>
        [JsonProperty("miss_counts")]
        public double[] MissCounts { get; set; } = Array.Empty<double>();

        /// <summary>
        /// The difficulty corresponding to the aim skill.
        /// </summary>
        [JsonProperty("cheese_note_count")]
        public double CheeseNoteCount { get; set; }

        /// <summary>
        /// The difficulty corresponding to the aim skill.
        /// </summary>
        [JsonProperty("cheese_levels")]
        public double[] CheeseLevels { get; set; } = Array.Empty<double>();

        /// <summary>
        /// The difficulty corresponding to the aim skill.
        /// </summary>
        [JsonProperty("cheese_factors")]
        public double[] CheeseFactors { get; set; } = Array.Empty<double>();

        /// <summary>
        /// The star rating corresponding to the tap skill.
        /// </summary>
        [JsonProperty("tap_star_rating")]
        public double TapStarRating { get; set; }

        /// <summary>
        /// The difficulty corresponding to the tap skill.
        /// </summary>
        [JsonProperty("tap_difficulty")]
        public double TapDifficulty { get; set; }

        /// <summary>
        /// The number of clickable objects weighted by difficulty.
        /// Related to <see cref="TapDifficulty"/>
        /// </summary>
        [JsonProperty("stream_note_count")]
        public double StreamNoteCount { get; set; }

        /// <summary>
        /// The number of clickable objects weighted by difficulty.
        /// Related to <see cref="TapDifficulty"/>
        /// </summary>
        [JsonProperty("mash_tap_difficulty")]
        public double MashTapDifficulty { get; set; }

        /// <summary>
        /// The star rating corresponding to the tap skill.
        /// </summary>
        [JsonProperty("finger_control_star_rating")]
        public double FingerControlStarRating { get; set; }

        /// <summary>
        /// The difficulty corresponding to the tap skill.
        /// </summary>
        [JsonProperty("finger_control_difficulty")]
        public double FingerControlDifficulty { get; set; }

        /// <summary>
        /// The difficulty corresponding to the flashlight skill.
        /// </summary>
        [JsonProperty("flashlight_difficulty")]
        public double FlashlightDifficulty { get; set; }

        /// <summary>
        /// The length of the map, in seconds.
        /// </summary>
        [JsonProperty("length")]
        public double Length { get; set; }

        /// <summary>
        /// The perceived approach rate inclusive of rate-adjusting mods (DT/HT/etc).
        /// </summary>
        /// <remarks>
        /// Rate-adjusting mods don't directly affect the approach rate difficulty value, but have a perceived effect as a result of adjusting audio timing.
        /// </remarks>
        [JsonProperty("approach_rate")]
        public double ApproachRate { get; set; }

        /// <summary>
        /// The perceived overall difficulty inclusive of rate-adjusting mods (DT/HT/etc).
        /// </summary>
        /// <remarks>
        /// Rate-adjusting mods don't directly affect the overall difficulty value, but have a perceived effect as a result of adjusting audio timing.
        /// </remarks>
        [JsonProperty("overall_difficulty")]
        public double OverallDifficulty { get; set; }

        /// <summary>
        /// The beatmap's drain rate. This doesn't scale with rate-adjusting mods.
        /// </summary>
        public double DrainRate { get; set; }

        /// <summary>
        /// The number of hitcircles in the beatmap.
        /// </summary>
        public int HitCircleCount { get; set; }

        /// <summary>
        /// The number of sliders in the beatmap.
        /// </summary>
        public int SliderCount { get; set; }

        /// <summary>
        /// The number of spinners in the beatmap.
        /// </summary>
        public int SpinnerCount { get; set; }

        public override IEnumerable<(int attributeId, object value)> ToDatabaseAttributes()
        {
            foreach (var v in base.ToDatabaseAttributes())
                yield return v;

            yield return (ATTRIB_ID_AIM, AimDifficulty);
            yield return (ATTRIB_ID_SPEED, TapDifficulty);
            yield return (ATTRIB_ID_OVERALL_DIFFICULTY, OverallDifficulty);
            yield return (ATTRIB_ID_APPROACH_RATE, ApproachRate);
            yield return (ATTRIB_ID_DIFFICULTY, StarRating);

            if (ShouldSerializeFlashlightRating())
                yield return (ATTRIB_ID_FLASHLIGHT, FlashlightDifficulty);

            yield return (ATTRIB_ID_SPEED_NOTE_COUNT, StreamNoteCount);
        }

        public override void FromDatabaseAttributes(IReadOnlyDictionary<int, double> values, IBeatmapOnlineInfo onlineInfo)
        {
            base.FromDatabaseAttributes(values, onlineInfo);

            AimDifficulty = values[ATTRIB_ID_AIM];
            TapDifficulty = values[ATTRIB_ID_SPEED];
            OverallDifficulty = values[ATTRIB_ID_OVERALL_DIFFICULTY];
            ApproachRate = values[ATTRIB_ID_APPROACH_RATE];
            StarRating = values[ATTRIB_ID_DIFFICULTY];
            FlashlightDifficulty = values.GetValueOrDefault(ATTRIB_ID_FLASHLIGHT);
            StreamNoteCount = values[ATTRIB_ID_SPEED_NOTE_COUNT];

            DrainRate = onlineInfo.DrainRate;
            HitCircleCount = onlineInfo.CircleCount;
            SliderCount = onlineInfo.SliderCount;
            SpinnerCount = onlineInfo.SpinnerCount;
        }

        #region Newtonsoft.Json implicit ShouldSerialize() methods

        // The properties in this region are used implicitly by Newtonsoft.Json to not serialise certain fields in some cases.
        // They rely on being named exactly the same as the corresponding fields (casing included) and as such should NOT be renamed
        // unless the fields are also renamed.

        [UsedImplicitly]
        public bool ShouldSerializeFlashlightRating() => Mods.Any(m => m is ModFlashlight);

        #endregion
    }
}
