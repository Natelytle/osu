// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Jack : ManiaSkill
    {
        private readonly ManiaDifficultyConstants constants;

        private readonly IDifficultyProcessor jackProcessor;

        public Jack(Mod[] mods, ManiaDifficultyConstants constants, IDifficultyProcessor jackProcessor, double overallDifficulty)
            : base(mods, overallDifficulty, constants.JackAccuracyScaleFactor, constants.JackAccuracyScaleFactor)
        {
            this.constants = constants;
            this.jackProcessor = jackProcessor;
        }

        protected override NoteAccuracyCurve AccuracyCurveAt(DifficultyHitObject current)
        {
            jackProcessor.ProcessStrainFor(current, constants);

            return new NoteAccuracyCurve { Difficulty = jackProcessor.CurrentStrain, ScaleFactor = this.constants.JackAccuracyScaleFactor };
        }
    }
}
