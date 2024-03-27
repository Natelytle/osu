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

        // Percentage values for the decay and recovery of stamina due to fatigue.
        // Current values - Lose 0.1% of stamina capacity per note, and recover 0.4% of accumulated stamina fatigue per second.
        private const double stamina_decay_per_note = 0.8;
        private const double stamina_recovery_per_second = 15.0;

        // And the same for burst speed.
        private const double burst_decay_per_note = 100.0;
        private const double burst_recovery_per_second = 30.0;

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
            // Stamina skill is slower, but decays slower.
            Compartments stamina = new Compartments(skill, stamina_decay_per_note, stamina_recovery_per_second);

            // Burst skill is much faster, but decays faster.
            Compartments burst = new Compartments(skill * 1.5, burst_decay_per_note, burst_recovery_per_second);

            List<double> speedValues = new List<double>();

            for (int i = 0; i < difficulties.Count; i++)
            {
                double difficulty = difficulties[i];
                double deltaTime = deltaTimes[i];

                double burstCapacity = burst.GetActiveCapacityAt(difficulty, deltaTime);
                double staminaCapacity = stamina.GetActiveCapacityAt(difficulty, deltaTime);

                speedValues.Add(Math.Max(burstCapacity, staminaCapacity));
            }

            return speedValues;
        }

        private struct Compartments
        {
            public Compartments(double skill, double decayPerNote, double recoveryPerSecond)
            {
                this.skill = skill;
                this.decayPerNote = decayPerNote / 100;
                this.recoveryPerSecond = recoveryPerSecond / 100;
                active = 0;
                fatigued = 0;
            }

            // Skill is the "maximum speed" these compartments can reach.
            private readonly double skill;
            private readonly double decayPerNote;
            private readonly double recoveryPerSecond;

            private double resting => skill - active + fatigued;
            private double active;
            private double fatigued;

            public double GetActiveCapacityAt(double difficulty, double deltaTime)
            {
                double fatiguedIncrease = active * decayPerNote;
                double fatiguedDecrease = fatigued * (1 - Math.Pow(1 - recoveryPerSecond, deltaTime / 1000));

                // We check to see if we have enough resting capacity after fatigue to hit the current note.
                active = Math.Min(difficulty, active - fatiguedIncrease + resting);
                fatigued += fatiguedIncrease - fatiguedDecrease;

                return active;
            }
        }
    }
}
