// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;


namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing.Rhythm
{
    /// <summary>
    /// Stores rhythm data for a <see cref="OsuDifficultyHitObject"/>.
    /// </summary>
    public class OsuDifficultyHitObjectRhythm
    {
        public EvenHitObjects? EvenHitObjects;
        public EvenPatterns? EvenPatterns;

        /// <summary>
        /// The ratio of current <see cref="Rulesets.Difficulty.Preprocessing.DifficultyHitObject.DeltaTime"/>
        /// to previous <see cref="Rulesets.Difficulty.Preprocessing.DifficultyHitObject.DeltaTime"/> for the rhythm change.
        /// A <see cref="Ratio"/> above 1 indicates a slow-down; a <see cref="Ratio"/> below 1 indicates a speed-up.
        /// </summary>
        public readonly double Ratio;

        /// <summary>
        /// The difficulty multiplier associated with this rhythm change.
        /// </summary>
        public readonly double Difficulty; // Added Difficulty Property

        private static readonly OsuDifficultyHitObjectRhythm[] common_rhythms =
        {
            new OsuDifficultyHitObjectRhythm(1, 1, 0.0),
            new OsuDifficultyHitObjectRhythm(2, 1, 0.3),
            new OsuDifficultyHitObjectRhythm(1, 2, 0.5),
            new OsuDifficultyHitObjectRhythm(3, 1, 0.3),
            new OsuDifficultyHitObjectRhythm(1, 3, 0.35),
            new OsuDifficultyHitObjectRhythm(3, 2, 0.6),
            new OsuDifficultyHitObjectRhythm(2, 3, 0.4),
            new OsuDifficultyHitObjectRhythm(5, 4, 0.5),
            new OsuDifficultyHitObjectRhythm(4, 5, 0.7)
        };

        public OsuDifficultyHitObjectRhythm(OsuDifficultyHitObject current)
        {
            var previous = current.Previous(0);

            if (previous == null)
            {
                Ratio = 1;
                Difficulty = 0.0;
                return;
            }

            OsuDifficultyHitObjectRhythm closestRhythm = getClosestRhythm(current.DeltaTime, previous.DeltaTime);
            Ratio = closestRhythm.Ratio;
            Difficulty = closestRhythm.Difficulty;
        }

        private OsuDifficultyHitObjectRhythm(int numerator, int denominator, double difficulty)
        {
            Ratio = numerator / (double)denominator;
            Difficulty = difficulty;
        }

        private OsuDifficultyHitObjectRhythm getClosestRhythm(double currentDeltaTime, double previousDeltaTime)
        {
            double ratio = currentDeltaTime / previousDeltaTime;
            return common_rhythms.OrderBy(x => Math.Abs(x.Ratio - ratio)).First();
        }
    }
}
