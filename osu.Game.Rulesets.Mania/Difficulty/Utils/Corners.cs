// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
        private double mapEndTime;

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
            // Do not add corner locations before the start of the map.
            double baseCornerStartTime = Math.Max(note.StartTime - 499, 0);
            double aCornerStartTime = Math.Max(note.StartTime - 1000, 0);

            // Same but for the end of the map.
            double[] baseCornerEndTimes = new[] { Math.Min(note.StartTime + 1, mapEndTime), Math.Min(note.StartTime + 500, mapEndTime) };
            double aCornerEndTime = Math.Min(note.StartTime + 1000, mapEndTime);

            BaseCorners.Add(note.StartTime);
            ACorners.Add(note.StartTime);
            AllCorners.Add(note.StartTime);

            BaseCorners.Add(baseCornerStartTime);
            ACorners.Add(aCornerStartTime);
            AllCorners.AddRange([baseCornerStartTime, aCornerStartTime]);

            BaseCorners.AddRange(baseCornerEndTimes);
            ACorners.Add(aCornerEndTime);
            AllCorners.AddRange(baseCornerEndTimes);
            AllCorners.Add(aCornerEndTime);
        }
    }
}
