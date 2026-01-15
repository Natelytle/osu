// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaDifficultyAttributes : DifficultyAttributes
    {
        public double StarRatingSS;

        public double AccuracyAt90PercentSkill;
        public double AccuracyAt80PercentSkill;
        public double AccuracyAt70PercentSkill;
        public double AccuracyAt60PercentSkill;
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
