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
        [JsonProperty("SS Star Rating")]
        public double StarRatingSS;

        [JsonProperty("90")]
        public double AccuracyAt90PercentSkill;

        [JsonProperty("80")]
        public double AccuracyAt80PercentSkill;

        [JsonProperty("70")]
        public double AccuracyAt70PercentSkill;

        [JsonProperty("60")]
        public double AccuracyAt60PercentSkill;

        [JsonProperty("50")]
        public double AccuracyAt50PercentSkill;

        public double AccuracyAt40PercentSkill;
        public double AccuracyAt30PercentSkill;
        public double AccuracyAt20PercentSkill;
        public double AccuracyAt10PercentSkill;

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
