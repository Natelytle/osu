// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    /// <summary>
    /// This skill is processed last, to ensure that the rest of the skills are able to process the current note in each <see cref="IReadonlyDifficultyProcessor"/>.
    /// </summary>
    public class Total : ManiaSkill
    {
        private readonly IReadonlyDifficultyProcessor coordinationProcessor;
        private readonly IReadonlyDifficultyProcessor jackProcessor;
        private readonly IReadonlyDifficultyProcessor releaseProcessor;
        private readonly IReadonlyDifficultyProcessor speedProcessor;
        private readonly IReadonlyDifficultyProcessor technicalProcessor;

        private readonly bool includeReleases;

        public Total(Mod[] mods, bool includeReleases,
                     IReadonlyDifficultyProcessor coordinationProcessor,
                     IReadonlyDifficultyProcessor jackProcessor,
                     IReadonlyDifficultyProcessor releaseProcessor,
                     IReadonlyDifficultyProcessor speedProcessor,
                     IReadonlyDifficultyProcessor technicalProcessor)
            : base(mods)
        {
            this.includeReleases = includeReleases;
            this.coordinationProcessor = coordinationProcessor;
            this.jackProcessor = jackProcessor;
            this.releaseProcessor = releaseProcessor;
            this.speedProcessor = speedProcessor;
            this.technicalProcessor = technicalProcessor;
        }

        protected override double GetNoteWeight(DifficultyHitObject current)
        {
            return includeReleases ? base.GetNoteWeight(current) : 1.0;
        }

        protected override double DifficultyAt(DifficultyHitObject current)
        {
            double coordinationDifficulty = coordinationProcessor.CurrentStrain;
            double releaseDifficulty = includeReleases ? releaseProcessor.CurrentStrain : 0;
            double speedDifficulty = speedProcessor.CurrentStrain;
            double jackDifficulty = jackProcessor.CurrentStrain;
            double technicalDifficulty = technicalProcessor.CurrentStrain;

            return combinedDifficulty(coordinationDifficulty, releaseDifficulty, speedDifficulty, jackDifficulty, technicalDifficulty);
        }

        private double combinedDifficulty(double coordinationDifficulty, double releaseDifficulty, double speedDifficulty, double jackDifficulty, double technicalDifficulty)
        {
            const int combine_lambda = 2;
            const double speed_weight = 1.02237;
            const double jack_weight = 1.42793;
            const double coordination_weight = 3.30000;
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
