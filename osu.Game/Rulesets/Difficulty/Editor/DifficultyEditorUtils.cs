// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.IO.Serialization;
using osu.Game.IO.Serialization.Converters;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Difficulty.Editor
{
    public static class DifficultyEditorUtils
    {
        /// <summary>
        /// Clones the specified beatmap into a minimal beatmap object that contains all info necessary for difficulty calculation.<br/>
        /// This method thus provides a beatmap decoupled from any UI, drawables etc. and can therefore be used across threads.
        /// </summary>
        /// <param name="beatmap">The beatmap to clone.</param>
        /// <returns>The cloned beatmap.</returns>
        public static IBeatmap CloneBeatmap(IBeatmap beatmap)
        {
            Beatmap clone = new Beatmap
            {
                Difficulty = beatmap.Difficulty,
                ControlPointInfo = beatmap.ControlPointInfo,
                HitObjects = new HitObjects([.. beatmap.HitObjects]).Serialize().Deserialize<HitObjects>().Data
            };

            return clone;
        }

        private class HitObjects(List<HitObject> data)
        {
            [JsonConverter(typeof(TypedListConverter<HitObject>))]
            public List<HitObject> Data { get; private set; } = data;
        }
    }
}
