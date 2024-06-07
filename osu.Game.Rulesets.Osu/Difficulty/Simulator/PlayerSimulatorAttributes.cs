// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Simulator
{
    public struct PlayerSimulatorAttributes
    {
        // The note the player is going for next, and the position on the note.
        // You don't always play maps sequentially, especially if there is a single really difficult note.
        public DifficultyHitObject TargetObject;

        // Aim specific attributes
        public Vector2 TargetObjectPosition; // Where on the note to aim for.
        public Vector2 PositionAtLastTargetNote; // Where we're coming from.

        // Tap specific attributes
        public double LastTapTime; // Mean time of the last tap - tap time is a normal distribution.
        public Finger LastTapFinger;

        // Player status attributes
        public double AgilityDrain;
        public double StaminaDrain;
        public double RhythmConfusion;
    }
}
