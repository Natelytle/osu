// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Total : ManiaSkill
    {
        private readonly ManiaDifficultyConstants tuning;

        private readonly IReadonlyDifficultyProcessor coordinationProcessor;
        private readonly IReadonlyDifficultyProcessor jackProcessor;
        private readonly IReadonlyDifficultyProcessor releaseProcessor;
        private readonly IReadonlyDifficultyProcessor speedProcessor;
        private readonly IReadonlyDifficultyProcessor technicalProcessor;

        public Total(Mod[] mods, ManiaDifficultyConstants tuning,
                     IReadonlyDifficultyProcessor coordinationProcessor,
                     IReadonlyDifficultyProcessor jackProcessor,
                     IReadonlyDifficultyProcessor releaseProcessor,
                     IReadonlyDifficultyProcessor speedProcessor,
                     IReadonlyDifficultyProcessor technicalProcessor,
                     double overallDifficulty)
            : base(mods, overallDifficulty,
                new List<double>
                {
                    tuning.CoordinationAccuracyScaleFactor,
                    tuning.JackAccuracyScaleFactor,
                    tuning.ReleaseAccuracyScaleFactor,
                    tuning.SpeedAccuracyScaleFactor,
                    tuning.TechnicalAccuracyScaleFactor
                }.Min(),
                new List<double>
                {
                    tuning.CoordinationAccuracyScaleFactor,
                    tuning.JackAccuracyScaleFactor,
                    tuning.ReleaseAccuracyScaleFactor,
                    tuning.SpeedAccuracyScaleFactor,
                    tuning.TechnicalAccuracyScaleFactor
                }.Max()
            )
        {
            this.tuning = tuning;

            this.coordinationProcessor = coordinationProcessor;
            this.jackProcessor = jackProcessor;
            this.releaseProcessor = releaseProcessor;
            this.speedProcessor = speedProcessor;
            this.technicalProcessor = technicalProcessor;
        }

        protected override NoteAccuracyCurve AccuracyCurveAt(DifficultyHitObject current)
        {
            const double combine_lambda = 2;

            NoteAccuracyCurve coordination = new NoteAccuracyCurve { Difficulty = coordinationProcessor.CurrentStrain, ScaleFactor = tuning.CoordinationAccuracyScaleFactor };
            NoteAccuracyCurve jack = new NoteAccuracyCurve { Difficulty = jackProcessor.CurrentStrain, ScaleFactor = tuning.JackAccuracyScaleFactor };
            NoteAccuracyCurve release = new NoteAccuracyCurve { Difficulty = releaseProcessor.CurrentStrain, ScaleFactor = tuning.ReleaseAccuracyScaleFactor };
            NoteAccuracyCurve speed = new NoteAccuracyCurve { Difficulty = speedProcessor.CurrentStrain, ScaleFactor = tuning.SpeedAccuracyScaleFactor };
            NoteAccuracyCurve technical = new NoteAccuracyCurve { Difficulty = technicalProcessor.CurrentStrain, ScaleFactor = tuning.TechnicalAccuracyScaleFactor };

            NoteAccuracyCurve tap = NoteAccuracyCurve.PNormSum(combine_lambda, speed, jack, coordination, technical);

            // Release is added linearly (p = 1) on top of the tap difficulty.
            return NoteAccuracyCurve.PNormSum(1, tap, release);
        }
    }
}
