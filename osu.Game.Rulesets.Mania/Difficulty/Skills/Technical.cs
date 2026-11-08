// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Technical : ManiaSkill
    {
        private readonly ManiaDifficultyConstants constants;

        private readonly IDifficultyProcessor technicalProcessor;

        public Technical(Mod[] mods, ManiaDifficultyConstants constants, IDifficultyProcessor technicalProcessor, double overallDifficulty)
            : base(mods, overallDifficulty, constants.TechnicalAccuracyScaleFactor, constants.TechnicalAccuracyScaleFactor)
        {
            this.constants = constants;
            this.technicalProcessor = technicalProcessor;
        }

        protected override NoteAccuracyCurve AccuracyCurveAt(DifficultyHitObject current)
        {
            technicalProcessor.ProcessStrainFor(current, constants);

            return new NoteAccuracyCurve { Difficulty = technicalProcessor.CurrentStrain, ScaleFactor = constants.TechnicalAccuracyScaleFactor };
        }
    }
}
