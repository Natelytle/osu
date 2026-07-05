// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaDifficultyAttributes : DifficultyAttributes
    {
        [JsonProperty("speed_difficulty")]
        public double SpeedDifficulty { get; set; }

        [JsonProperty("technical_difficulty")]
        public double TechnicalDifficulty { get; set; }

        [JsonProperty("jack_difficulty")]
        public double JackDifficulty { get; set; }

        [JsonProperty("coordination_difficulty")]
        public double CoordinationDifficulty { get; set; }

        [JsonProperty("release_difficulty")]
        public double ReleaseDifficulty { get; set; }

        [JsonProperty("variety")]
        public double Variety { get; set; }

        [JsonProperty("overall_difficulty")]
        public double OverallDifficulty { get; set; }

        [JsonProperty("mean_manip")]
        public double MeanManipulation { get; set; }

        [JsonProperty("note_count")]
        public int NoteCount { get; set; }

        [JsonProperty("hold_note_count")]
        public int HoldNoteCount { get; set; }

        /*[JsonProperty("mean_manipulation")]
        public double MeanManipulation { get; set; } = 1.0;*/

        public override IEnumerable<(int attributeId, object value)> ToDatabaseAttributes()
        {
            foreach (var v in base.ToDatabaseAttributes())
                yield return v;

            yield return (ATTRIB_ID_DIFFICULTY, StarRating);
        }

        public override void FromDatabaseAttributes(IReadOnlyDictionary<int, double> values, IBeatmapOnlineInfo onlineInfo)
        {
            base.FromDatabaseAttributes(values, onlineInfo);

            StarRating = values[ATTRIB_ID_DIFFICULTY];
        }
    }
}
