// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Simulator
{
    public struct PlayerSimulatorAttributes
    {
        public PlayerSimulatorAttributes(DifficultyHitObject targetObject, Vector2 positionAtPrevNote)
        {
            TargetObject = targetObject;
            PositionAtPrevNote = positionAtPrevNote;
            LastTapTime = null;
            LastTapMethod = null;
            AgilityDrain = 0;
            StaminaDrain = 0;
            RhythmConfusion = 0;
        }

        // The note the player is going for next. You don't always attempt to hit every note in a map, especially in the case of a single really difficult note.
        public DifficultyHitObject TargetObject;

        // Aim specific attributes
        public Vector2 PositionAtPrevNote; // Average location of we're coming from - aim error is a normal distribution.

        // Tap specific attributes
        public double? LastTapTime; // Average time of the last tap - tap time is a normal distribution.
        public TapMethod? LastTapMethod;

        // Player status attributes
        public double AgilityDrain;
        public double StaminaDrain;
        public double RhythmConfusion;
    }
}
