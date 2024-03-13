// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class OsuStaminaSkill : Skill
    {
        protected OsuStaminaSkill(Mod[] mods)
            : base(mods)
        {
        }

        // How fast speed should decay with note count.
        private const double stamina_factor = 2.5;

        private readonly List<double> difficulties = new List<double>();
        private readonly List<double> strainTimes = new List<double>();

        protected abstract double StrainValueAt(DifficultyHitObject current);

        public override void Process(DifficultyHitObject current)
        {
            difficulties.Add(StrainValueAt(current));
            strainTimes.Add(((OsuDifficultyHitObject)current).StrainTime);
        }

        // If the difference is positive, skill is too high for the map.
        // If the difference is negative, skill is too low and the player is not fast enough.
        private double getLowestDifference(double skill)
        {
            if (skill == 0)
                return double.NegativeInfinity;

            double accumulatedDifficulty = 0;
            double lowestDifference = double.PositiveInfinity;

            for (int i = 0; i < difficulties.Count; i++)
            {
                // accumulatedDifficulty *= Math.Pow(0.96, strainTimes[i] / 1000);

                accumulatedDifficulty += difficulties[i] / skill;

                double staminaDecayFactor = Math.Min(1, Math.Pow(accumulatedDifficulty, -1 / (10 * stamina_factor)));

                lowestDifference = Math.Min(lowestDifference, skill * staminaDecayFactor - difficulties[i]);
            }

            return lowestDifference;
        }

        public override double DifficultyValue()
        {
            if (difficulties.Max() == 0)
                return 0;

            const double guess_upper_bound = 1;

            return 8 * Chandrupatla.FindRootExpand(getLowestDifference, 0, guess_upper_bound);
        }
    }
}
