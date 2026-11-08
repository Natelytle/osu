// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Coordination : ManiaSkill
    {
        private readonly ManiaDifficultyConstants constants;

        private readonly IDifficultyProcessor coordinationProcessor;

        public Coordination(Mod[] mods, ManiaDifficultyConstants constants, IDifficultyProcessor coordinationProcessor, double overallDifficulty)
            : base(mods, overallDifficulty, constants.CoordinationAccuracyScaleFactor, constants.CoordinationAccuracyScaleFactor)
        {
            this.constants = constants;
            this.coordinationProcessor = coordinationProcessor;
        }

        protected override NoteAccuracyCurve AccuracyCurveAt(DifficultyHitObject current)
        {
            coordinationProcessor.ProcessStrainFor(current, constants);

            return new NoteAccuracyCurve { Difficulty = coordinationProcessor.CurrentStrain, ScaleFactor = this.constants.CoordinationAccuracyScaleFactor };
        }
    }
}
