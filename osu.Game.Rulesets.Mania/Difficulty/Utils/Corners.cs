// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Extensions;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    /// <summary>
    /// The locations in time where linearly interpolated difficulty values are obtained from.
    /// Storing a difficulty value at every point in time is memory-intensive, so we only store the time values where relevant changes in difficulty occur.
    /// </summary>
    public struct Corners
    {
        private readonly double mapEndTime;

        /// <summary>
        /// Time values at, 1ms after, 501ms after, and 499ms before each note.
        /// </summary>
        public SortedSet<double> BaseCorners;

        /// <summary>
        /// Time values at, 1000ms after, and 1000ms before each note.
        /// </summary>
        public SortedSet<double> ACorners;

        /// <summary>
        /// A combination of BaseCorners and ACorners. Used for interpolating difficulty values.
        /// </summary>
        public SortedSet<double> AllCorners;

        public Corners(double mapEndTime)
        {
            this.mapEndTime = mapEndTime;

            BaseCorners = new SortedSet<double>();
            ACorners = new SortedSet<double>();
            AllCorners = new SortedSet<double>();
        }

        public readonly void AddCornersForNote(ManiaDifficultyHitObject note)
        {
            BaseCorners.AddRange([0, mapEndTime]);
            ACorners.AddRange([0, mapEndTime]);
            AllCorners.AddRange([0, mapEndTime]);

            BaseCorners.AddRange([note.StartTime, note.EndTime]);
            ACorners.AddRange([note.StartTime, note.EndTime]);
            AllCorners.AddRange([note.StartTime, note.EndTime]);

            if (note.StartTime <= mapEndTime - 1)
            {
                BaseCorners.Add(note.StartTime + 1);
                AllCorners.Add(note.StartTime + 1);
            }

            if (note.EndTime <= mapEndTime - 1)
            {
                BaseCorners.Add(note.EndTime + 1);
                AllCorners.Add(note.EndTime + 1);
            }
        }
    }
}
