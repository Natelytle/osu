// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class CrossColumn : ManiaSkillHeads
    {
        private readonly double[] columnCoordinationDifficulties;
        private readonly double[] columnSpeedDifficulties;

        public CrossColumn(Mod[] mods, int columns)
            : base(mods)
        {
            columnCoordinationDifficulties = new double[columns + 1];
            columnSpeedDifficulties = new double[columns];
        }

        protected override double BaseDifficulty(ManiaDifficultyHitObject current)
        {
            double speedDifficulty = CrossColumnEvaluator.EvaluateSpeedDifficultyOf(current);
            columnSpeedDifficulties[current.Column] = speedDifficulty;

            // Get the indices of the left and right column boundaries for this note.
            int leftColumnBoundary = current.Column;
            int rightColumnBoundary = leftColumnBoundary + 1;

            (double leftCrossDifficulty, double rightCrossDifficulty) = CrossColumnEvaluator.EvaluateCrossDifficultiesOf(current);
            columnCoordinationDifficulties[leftColumnBoundary] = leftCrossDifficulty;
            columnCoordinationDifficulties[rightColumnBoundary] = rightCrossDifficulty;

            // Wait until the whole chord is processed before returning.
            if (current.NextHead(0) is not null && current.NextHead(0)!.HeadDeltaTime == 0)
            {
                return 0;
            }

            double columnSum = columnCoordinationDifficulties.Sum();
            double speedSum = columnSpeedDifficulties.Sum();

            return columnSum + speedSum;
        }
    }
}
