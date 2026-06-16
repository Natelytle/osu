// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    /// <summary>
    /// Cross-column coordination and chord pressure.
    /// </summary>
    /// <remarks>
    /// Three live signals are computed per note:
    /// <list type="bullet">
    /// <item>cross-column - a quick transition to a different column, scaled by how far apart the columns
    /// are (wide jumps demand more hand movement).</item>
    /// <item>chord load - the cost of pressing several columns at once, growing with chord size; chordjacks
    /// are nerfed to avoid double-counting with <see cref="Jack"/>.</item>
    /// <item>long-note coordination - pressing a note while other columns are still held, scaled by speed
    /// so fast hybrid patterns are credited more than slow ones.</item>
    /// </list>
    /// </remarks>
    public class Coordination : ManiaSkill
    {
        private const double strain_decay_base = 0.52909;

        private const double cross_column_rate_offset = 0.045;
        private const double coordination_scale = 1.19990;

        private const double chord_load_per_extra_column = 0.9;
        private const double chordjack_nerf = 0.45397;

        private const double held_long_note_weight = 0.01003;
        private const double held_speed_factor_offset = 0.08;

        public Coordination(Mod[] mods, int totalColumns)
            : base(mods, totalColumns)
        {
        }

        protected override double StrainDecayBase => strain_decay_base;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            var hitObject = (ManiaDifficultyHitObject)current;

            double crossColumnStrain = 0.0;

            if (hitObject.Previous(0) is ManiaDifficultyHitObject previous
                && previous.Column != hitObject.Column
                && hitObject.DeltaTime >= CHORD_TOLERANCE)
            {
                double coefficient = CrossColumnCoefficientSum(previous.Column, hitObject.Column);
                crossColumnStrain = coefficient / (hitObject.DeltaTime / 1000.0 + cross_column_rate_offset);
                crossColumnStrain *= TrillFactor(hitObject);
            }

            int chordSize = ChordSize(hitObject);

            double lastStartTime = LastStartTimeInColumn(hitObject.Column);
            double columnDelta = double.IsNegativeInfinity(lastStartTime) ? double.PositiveInfinity : hitObject.StartTime - lastStartTime;
            bool isChordjack = chordSize >= 2 && columnDelta <= JACK_WINDOW_MS;
            double chordLoad = chordSize >= 2 ? chord_load_per_extra_column * (chordSize - 1) * (isChordjack ? chordjack_nerf : 1.0) : 0.0;

            int heldColumns = ConcurrentlyHeldColumns(hitObject.Column, hitObject.StartTime);
            double heldSpeedFactor = hitObject.DeltaTime >= CHORD_TOLERANCE ? 1.0 / (hitObject.DeltaTime / 1000.0 + held_speed_factor_offset) : 1.0;
            double heldNoteLoad = held_long_note_weight * Math.Sqrt(heldColumns) * heldSpeedFactor;

            UpdateColumnState(hitObject);
            return crossColumnStrain * coordination_scale + chordLoad + heldNoteLoad;
        }
    }
}
