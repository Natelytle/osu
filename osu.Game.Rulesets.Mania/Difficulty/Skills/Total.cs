// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Total : ManiaSkill
    {
        private readonly CoordinationProcessor coordinationProcessor;
        private readonly JackProcessor jackProcessor;
        private readonly ReleaseProcessor releaseProcessor;
        private readonly SpeedProcessor speedProcessor;
        private readonly TechnicalProcessor technicalProcessor;

        private readonly List<double> tappingDifficulties = new List<double>();

        public Total(Mod[] mods)
            : base(mods)
        {
            coordinationProcessor = new CoordinationProcessor();
            jackProcessor = new JackProcessor();
            releaseProcessor = new ReleaseProcessor();
            speedProcessor = new SpeedProcessor();
            technicalProcessor = new TechnicalProcessor();
        }

        protected override double DifficultyAt(DifficultyHitObject current)
        {
            double coordinationDifficulty = coordinationProcessor.ProcessStrainFor(current);
            double releaseDifficulty = releaseProcessor.ProcessStrainFor(current);
            double speedDifficulty = speedProcessor.ProcessStrainFor(current);
            double jackDifficulty = jackProcessor.ProcessStrainFor(current);
            double technicalDifficulty = technicalProcessor.ProcessStrainFor(current);

            double tapOnly = combinedDifficulty(coordinationDifficulty, 0.0, speedDifficulty, jackDifficulty, technicalDifficulty);
            if (tapOnly > 0)
                tappingDifficulties.Add(tapOnly);

            return combinedDifficulty(coordinationDifficulty, releaseDifficulty, speedDifficulty, jackDifficulty, technicalDifficulty);
        }

        public double TappingDifficultyValue()
        {
            tappingDifficulties.Sort();
            return AggregateDifficulty(tappingDifficulties, BaseNoteCount);
        }

        private double combinedDifficulty(double coordinationDifficulty, double releaseDifficulty, double speedDifficulty, double jackDifficulty, double technicalDifficulty)
        {
            const int combine_lambda = 2;
            const double speed_weight = 1.02237;
            const double jack_weight = 1.42793;
            const double coordination_weight = 2.49980;
            const double technical_weight = 2.49916;
            const double release_weight = 2.83449;

            double powerSum = speed_weight * DiffUtils.Pow(speedDifficulty, combine_lambda)
                              + jack_weight * DiffUtils.Pow(jackDifficulty, combine_lambda)
                              + coordination_weight * DiffUtils.Pow(coordinationDifficulty, combine_lambda)
                              + technical_weight * DiffUtils.Pow(technicalDifficulty, combine_lambda);

            double tapDifficulty = powerSum > 0 ? DiffUtils.Pow(powerSum, 1.0 / combine_lambda) : 0.0;
            return tapDifficulty + release_weight * releaseDifficulty;
        }
    }
}
