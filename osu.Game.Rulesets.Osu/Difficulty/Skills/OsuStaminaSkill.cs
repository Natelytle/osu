// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class OsuStaminaSkill : Skill
    {
        private readonly List<double> difficulties = new List<double>();
        private readonly List<double> deltaTimes = new List<double>();

        // The percentage of the active muscle capacity that should become fatigued per note.
        private const double stamina_decay_per_note = 0.008;

        // The percentage of fatigued muscle capacity that should recover and become resting muscle capacity per second.
        private const double stamina_recovery_per_second = 0.025;

        protected OsuStaminaSkill(Mod[] mods)
            : base(mods)
        {
        }

        public override void Process(DifficultyHitObject current)
        {
            difficulties.Add(StrainValueAt(current));
            deltaTimes.Add(current.DeltaTime);
        }

        protected abstract double StrainValueAt(DifficultyHitObject current);

        public override double DifficultyValue()
        {
            if (difficulties.Count == 0 || difficulties.Max() <= 1e-10)
                return 0;

            double upperBoundEstimate = 3.0 * difficulties.Max();

            double skill = Chandrupatla.FindRootExpand(
                skill => Convert.ToInt32(getSpeedValuesAtSkill(skill).SequenceEqual(difficulties)) - 0.5,
                0,
                upperBoundEstimate,
                accuracy: 1e-4);

            return skill;
        }

        private List<double> getSpeedValuesAtSkill(double skill)
        {
            // The player's total skill is their speed - a player with 40 skill cannot hit a note of difficulty 60 or higher on time.
            double restingStaminaCompartment = skill * 1.5;
            double activeStaminaCompartment = 0;
            double fatiguedStaminaCompartment = 0;

            List<double> speedValues = new List<double>();

            for (int i = 0; i < difficulties.Count; i++)
            {
                double difficulty = difficulties[i];
                double deltaTime = deltaTimes[i];

                double fatiguedCompartmentDecrease = Math.Pow(1 - stamina_recovery_per_second, deltaTime / 1000);
                double fatiguedCompartmentIncrease = activeStaminaCompartment * (1 - Math.Pow(1 - stamina_decay_per_note, deltaTime / 1000));

                activeStaminaCompartment = Math.Min(difficulty, activeStaminaCompartment - fatiguedCompartmentIncrease + restingStaminaCompartment);
                fatiguedStaminaCompartment *= fatiguedCompartmentDecrease;
                fatiguedStaminaCompartment += fatiguedCompartmentIncrease;
                restingStaminaCompartment = skill - activeStaminaCompartment - fatiguedStaminaCompartment;

                speedValues.Add(activeStaminaCompartment);
            }

            return speedValues;
        }
    }
}
