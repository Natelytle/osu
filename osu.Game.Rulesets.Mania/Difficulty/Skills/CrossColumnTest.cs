// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class CrossColumnTest : ManiaStrainDecaySkill
    {
        protected override double StrainDecayBase => 0.2;

        private readonly double[] columnCoordinationStrains;
        private readonly double[] columnSpeedStrains;

        private readonly double[] columnCoordinationDifficulties;
        private readonly double[] columnSpeedDifficulties;

        public CrossColumnTest(Mod[] mods, int columns)
            : base(mods)
        {
            columnCoordinationStrains = new double[columns + 1];
            columnSpeedStrains = new double[columns];

            columnCoordinationDifficulties = new double[columns + 1];
            columnSpeedDifficulties = new double[columns];
        }

        protected override double BaseDifficulty(ManiaDifficultyHitObject current)
        {
            // Get the indices of the left and right column boundaries for this note.
            int leftColumnBoundary = current.Column;
            int rightColumnBoundary = leftColumnBoundary + 1;

            (double leftCrossDifficulty, double rightCrossDifficulty) = CrossColumnEvaluatorTest.EvaluateCrossDifficultiesOf(current);
            columnCoordinationDifficulties[leftColumnBoundary] = leftCrossDifficulty;
            columnCoordinationDifficulties[rightColumnBoundary] = rightCrossDifficulty;

            double speedDifficulty = CrossColumnEvaluatorTest.EvaluateSpeedDifficultyOf(current);
            columnSpeedDifficulties[current.Column] = speedDifficulty;

            double columnSum = columnCoordinationDifficulties.Sum();
            double speedSum = columnSpeedDifficulties.Sum();

            return columnSum + speedSum;
        }

        protected override void AddChordDifficulties(double newStartTime)
        {
            double decay = StrainDecay(CurrentChordDelta ?? CurrentChordTime);

            for (int i = 0; i < columnCoordinationDifficulties.Length; i++)
            {
                columnCoordinationStrains[i] *= decay;
                columnCoordinationStrains[i] += columnCoordinationDifficulties[i] * (1 - decay);
            }

            for (int i = 0; i < columnSpeedDifficulties.Length; i++)
            {
                columnSpeedStrains[i] *= decay;
                columnSpeedDifficulties[i] += columnSpeedDifficulties[i] * (1 - decay);
            }

            double columnSum = columnCoordinationStrains.Sum();
            double speedSum = columnSpeedStrains.Sum();

            double strain = columnSum + speedSum;

            for (int i = 0; i < ChordNoteCount; i++)
            {
                ObjectDifficulties.Add(strain);
            }

            CurrentChordDelta = newStartTime - CurrentChordTime;
        }
    }
}
