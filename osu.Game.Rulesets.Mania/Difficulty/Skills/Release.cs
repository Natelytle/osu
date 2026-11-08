// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Release : ManiaSkill
    {
        private readonly ManiaDifficultyConstants constants;

        private readonly IDifficultyProcessor releaseProcessor;

        public Release(Mod[] mods, ManiaDifficultyConstants constants, IDifficultyProcessor releaseProcessor, double overallDifficulty)
            : base(mods, overallDifficulty, constants.ReleaseAccuracyScaleFactor, constants.ReleaseAccuracyScaleFactor)
        {
            this.constants = constants;
            this.releaseProcessor = releaseProcessor;
        }

        protected override NoteAccuracyCurve AccuracyCurveAt(DifficultyHitObject current)
        {
            releaseProcessor.ProcessStrainFor(current, constants);

            return new NoteAccuracyCurve { Difficulty = releaseProcessor.CurrentStrain, ScaleFactor = constants.ReleaseAccuracyScaleFactor };
        }
    }
}
