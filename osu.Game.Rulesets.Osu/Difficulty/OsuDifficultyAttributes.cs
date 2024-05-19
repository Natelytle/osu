// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuDifficultyAttributes : DifficultyAttributes
    {
        public override IEnumerable<(int attributeId, object value)> ToDatabaseAttributes()
        {
            foreach (var v in base.ToDatabaseAttributes())
                yield return v;
        }

        public override void FromDatabaseAttributes(IReadOnlyDictionary<int, double> values, IBeatmapOnlineInfo onlineInfo)
        {
            base.FromDatabaseAttributes(values, onlineInfo);

            StarRating = values[ATTRIB_ID_DIFFICULTY];
        }

        #region Newtonsoft.Json implicit ShouldSerialize() methods

        // The properties in this region are used implicitly by Newtonsoft.Json to not serialise certain fields in some cases.
        // They rely on being named exactly the same as the corresponding fields (casing included) and as such should NOT be renamed
        // unless the fields are also renamed.

        [UsedImplicitly]
        public bool ShouldSerializeFlashlightDifficulty() => Mods.Any(m => m is ModFlashlight);

        #endregion
    }
}
