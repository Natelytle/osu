// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing
{
    /// <summary>
    /// Works out, once per note, how much the long notes being held at that moment are worth. Doing it here means
    /// every evaluator reads <see cref="ManiaDifficultyHitObject.HeldNoteMultiplier"/> instead of each re-deriving
    /// it, so a note that is held through is priced the same everywhere it matters.
    /// </summary>
    public static class HeldNoteMultiplier
    {
        /// <summary>
        /// How long either side of a hold's head or tail it takes to reach full strength. A hold contributes
        /// little right after it is pressed and right before it is let go, and its full amount once it has been
        /// held this long with at least this long still to go.
        /// </summary>
        private const double hold_ramp_ms = 800.0;

        /// <summary>
        /// How long before a hold's tail it takes to reach full strength. Kept short so a hold is only worth less
        /// right as it is let go.
        /// </summary>
        private const double hold_end_ramp_ms = 180.0;

        /// <summary>
        /// The least a held note contributes, so a hold whose head or tail lands right on a note still counts a
        /// little rather than switching on from nothing.
        /// </summary>
        private const double hold_edge_factor = 0.4;

        /// <summary>
        /// Holds shorter than this barely work the hand more than the tap that made them, so a hold fades out
        /// below it. Matches <c>ReleaseEvaluator</c> so a short hold is priced the same everywhere it is
        /// read rather than only being discounted on release.
        /// </summary>
        private const double short_hold_ms = 110.90068;

        /// <summary>
        /// How sharply a hold fades out below <see cref="short_hold_ms"/>; matches the release gate.
        /// </summary>
        private const double short_hold_steepness = 0.07;

        /// <summary>
        /// How much a fully held note adds to the multiplier.
        /// </summary>
        private const double hold_weight = 1.0;

        /// <summary>
        /// Pressure below this adds straight to the multiplier. Past it the total bends over logarithmically, so a
        /// wall of simultaneous holds (an inverse or very LN-dense chart) soft caps instead of running away.
        /// </summary>
        private const double hold_pressure_cap = 1.0;

        /// <summary>
        /// How quickly the soft cap bends over past <see cref="hold_pressure_cap"/>; smaller is a harder cap.
        /// </summary>
        private const double hold_pressure_softness = 0.3;

        public static void ProcessAndAssign(ManiaMapData mapData)
        {
            foreach (var row in mapData.Rows)
            {
                foreach (var note in row.Objects)
                    note.HeldNoteMultiplier = evaluate(note);
            }
        }

        /// <summary>
        /// A multiplier (at least 1) for the long notes being held while <paramref name="current"/> is hit. Every
        /// hold still down at that moment ramps in over the first <see cref="hold_ramp_ms"/> after it started and
        /// out over the last <see cref="hold_end_ramp_ms"/> before it ends, so a long note that has been held a
        /// while and still has time to run is worth the most while one whose head or tail is right on this note is
        /// worth a little. The holds add up and the total soft caps, so a wall of them prices higher than a single
        /// one without running away.
        /// </summary>
        private static double evaluate(ManiaDifficultyHitObject current)
        {
            double pressure = 0.0;

            foreach (var hold in current.HeadOverlappedHolds)
            {
                if (hold is null)
                    continue;

                pressure += heldNoteFactor(hold.StartTime, hold.EndTime, current.StartTime);
            }

            // A hold in this very row has only just been pressed, so it counts as the smallest amount.
            if (current.BaseObject is HoldNote)
                pressure += heldNoteFactor(current.StartTime, current.EndTime, current.StartTime);

            return 1.0 + hold_weight * softCap(pressure);
        }

        /// <summary>
        /// Grows linearly up to <see cref="hold_pressure_cap"/>, then bends over logarithmically so the total keeps
        /// rising but ever more slowly.
        /// </summary>
        private static double softCap(double pressure)
        {
            if (pressure <= hold_pressure_cap)
                return pressure;

            return hold_pressure_cap + hold_pressure_softness * Math.Log(1.0 + (pressure - hold_pressure_cap) / hold_pressure_softness);
        }

        /// <summary>
        /// What a single long note contributes while it is held at <paramref name="time"/>, ramping in from its
        /// start and out towards its end over <see cref="hold_ramp_ms"/> either side, and never below
        /// <see cref="hold_edge_factor"/>. Short holds that barely outlast a tap fade out here the same way
        /// <c>ReleaseEvaluator</c> fades them, so a hold costs according to how much it really holds.
        /// </summary>
        private static double heldNoteFactor(double startTime, double endTime, double time)
        {
            double heldFor = time - startTime;
            double stillToGo = endTime - time;

            if (heldFor < 0.0 || stillToGo < 0.0)
                return 0.0;

            double rampIn = DiffUtils.Smoothstep(heldFor, 0.0, hold_ramp_ms);
            double rampOut = DiffUtils.Smoothstep(stillToGo, 0.0, hold_end_ramp_ms);

            double shortHoldGate = DiffUtils.Logistic(endTime - startTime, short_hold_ms, short_hold_steepness);

            return (hold_edge_factor + (1.0 - hold_edge_factor) * rampIn * rampOut) * shortHoldGate;
        }
    }
}
