// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class HitWindows
    {
        public static double[] GetLazerHitWindows(Mod[] mods, double overallDifficulty)
        {
            double[] lazerHitWindows = new double[5];

            double windowMultiplier = 1;

            if (mods.Any(m => m is ModHardRock))
                windowMultiplier *= 1 / 1.4;
            else if (mods.Any(m => m is ModEasy))
                windowMultiplier *= 1.4;

            if (overallDifficulty < 5)
                lazerHitWindows[0] = (22.4 - 0.6 * overallDifficulty) * windowMultiplier;
            else
                lazerHitWindows[0] = (24.9 - 1.1 * overallDifficulty) * windowMultiplier;
            lazerHitWindows[1] = (64 - 3 * overallDifficulty) * windowMultiplier;
            lazerHitWindows[2] = (97 - 3 * overallDifficulty) * windowMultiplier;
            lazerHitWindows[3] = (127 - 3 * overallDifficulty) * windowMultiplier;
            lazerHitWindows[4] = (151 - 3 * overallDifficulty) * windowMultiplier;

            return lazerHitWindows;
        }

        public static double[] GetLegacyHitWindows(Mod[] mods, bool isConvert, double overallDifficulty)
        {
            double[] legacyHitWindows = new double[5];

            double greatWindowLeniency = 0;
            double goodWindowLeniency = 0;

            // When converting beatmaps to osu!mania in stable, the resulting hit window sizes are dependent on whether the beatmap's OD is above or below 4.
            if (isConvert)
            {
                overallDifficulty = 10;

                if (overallDifficulty <= 4)
                {
                    greatWindowLeniency = 13;
                    goodWindowLeniency = 10;
                }
            }

            double windowMultiplier = 1;

            if (mods.Any(m => m is ModHardRock))
                windowMultiplier *= 1 / 1.4;
            else if (mods.Any(m => m is ModEasy))
                windowMultiplier *= 1.4;

            legacyHitWindows[0] = Math.Floor(16 * windowMultiplier);
            legacyHitWindows[1] = Math.Floor((64 - 3 * overallDifficulty + greatWindowLeniency) * windowMultiplier);
            legacyHitWindows[2] = Math.Floor((97 - 3 * overallDifficulty + goodWindowLeniency) * windowMultiplier);
            legacyHitWindows[3] = Math.Floor((127 - 3 * overallDifficulty) * windowMultiplier);
            legacyHitWindows[4] = Math.Floor((151 - 3 * overallDifficulty) * windowMultiplier);

            return legacyHitWindows;
        }
    }
}
