// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class OsuStaminaSkill : Skill
    {
        private readonly List<double> bpms = new List<double>();
        private readonly List<double> deltaTimes = new List<double>();

        // Percentage values for the decay and recovery of stamina due to fatigue.
        // Current values - Lose 1.5% of stamina capacity, and recover 9.0% of accumulated stamina fatigue per second.
        private const double stamina_decay_per_second = 1.5;
        private const double stamina_recovery_per_second = 9.0;

        // And the same for burst speed.
        private const double burst_decay_per_second = 25.0;
        private const double burst_recovery_per_second = 40.0;

        protected OsuStaminaSkill(Mod[] mods)
            : base(mods)
        {
        }

        public override void Process(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner) return;

            bpms.Add(StrainValueAt(current));
            deltaTimes.Add(15000 / StrainValueAt(current));
        }

        protected abstract double StrainValueAt(DifficultyHitObject current);

        public override double DifficultyValue()
        {
            if (bpms.Count == 0 || bpms.Max() <= 1e-10)
                return 0;

            double upperBoundEstimate = 1.3 * bpms.Max();

            if (getMaximumDeficitAtSkill(0) <= 40)
                return 0;

            double skill = Chandrupatla.FindRootExpand(
                skill => getMaximumDeficitAtSkill(skill) - 40,
                0,
                upperBoundEstimate,
                accuracy: 1e-4);

            return skill;
        }

        private double getMaximumDeficitAtSkill(double skill)
        {
            if (skill == 0)
                return deltaTimes.Sum();

            // Stamina skill is slower, but decays slower.
            Compartments stamina = new Compartments(skill, stamina_decay_per_second, stamina_recovery_per_second);

            // Burst skill is much faster, but decays faster.
            Compartments burst = new Compartments(skill * 1.4, burst_decay_per_second, burst_recovery_per_second);

            // How many milliseconds late the player is tapping.
            double currentDeficit = 0;
            double maximumDeficit = 0;

            for (int i = 0; i < bpms.Count; i++)
            {
                double difficulty = bpms[i];
                double deltaTime = Math.Max(deltaTimes[i] - currentDeficit, 0);

                double burstCapacity = burst.GetActiveCapacityAt(difficulty, deltaTime);
                double staminaCapacity = stamina.GetActiveCapacityAt(difficulty, deltaTime);

                double playerDeltatime = 15000 / Math.Max(burstCapacity, staminaCapacity);

                currentDeficit = playerDeltatime - deltaTime;
                maximumDeficit = Math.Max(maximumDeficit, currentDeficit);
            }

            return maximumDeficit;
        }

        private struct Compartments
        {
            public Compartments(double skill, double decayPerSecond, double recoveryPerSecond)
            {
                this.skill = skill;
                resting = skill;
                this.decayPerSecond = decayPerSecond / 100;
                this.recoveryPerSecond = recoveryPerSecond / 100;
                active = 0;
                fatigued = 0;
            }

            // Skill is the "maximum speed" these compartments can reach.
            private readonly double skill;
            private readonly double decayPerSecond;
            private readonly double recoveryPerSecond;

            private double resting;
            private double active;
            private double fatigued;

            public double GetActiveCapacityAt(double difficulty, double deltaTime)
            {
                double fatiguedIncrease = active * (1 - Math.Pow(1 - decayPerSecond, deltaTime / 1000));
                double fatiguedDecrease = fatigued * (1 - Math.Pow(1 - recoveryPerSecond, deltaTime / 1000));

                // We check to see if we have enough resting capacity after fatigue to hit the current note.
                active = Math.Min(difficulty, active - fatiguedIncrease + resting);
                fatigued += fatiguedIncrease - fatiguedDecrease;
                resting = skill - active - fatigued;

                return active;
            }
        }
    }
}
