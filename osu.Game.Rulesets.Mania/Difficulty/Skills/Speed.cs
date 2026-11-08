// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Speed : ManiaSkill
    {
        private readonly ManiaDifficultyConstants constants;

        private readonly IDifficultyProcessor speedProcessor;

        public Speed(Mod[] mods, ManiaDifficultyConstants constants, IDifficultyProcessor speedProcessor, double overallDifficulty)
            : base(mods, overallDifficulty, constants.SpeedAccuracyScaleFactor, constants.SpeedAccuracyScaleFactor)
        {
            this.constants = constants;
            this.speedProcessor = speedProcessor;
        }

        protected override NoteAccuracyCurve AccuracyCurveAt(DifficultyHitObject current)
        {
            speedProcessor.ProcessStrainFor(current, constants);

            return new NoteAccuracyCurve { Difficulty = speedProcessor.CurrentStrain, ScaleFactor = this.constants.SpeedAccuracyScaleFactor };
        }
    }
}
