// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;
using static osu.Game.Rulesets.Difficulty.Utils.DifficultyCalculationUtils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class TotalEvaluator
    {
        // Closer to 1 means this skill contributes more of its difficulty to the sum. Higher means it contributes less.
        private const double column = 1.5;
        private const double speed = 1.6;
        private const double chord = 1.1;
        private const double hold = 1.2;

        public static double EvaluateTotalDifficultyOf(DifficultyHitObject current)
        {
            switch (current.BaseObject)
            {
                case not (HeadNote or TailNote) or HeadNote:
                    return evaluateTotalDifficultyNote(current);

                case TailNote:
                    return evaluateTotalDifficultyTail(current);
            }
        }

        private static double evaluateTotalDifficultyNote(DifficultyHitObject current)
        {
            double sameColumnDifficulty = SameColumnEvaluator.EvaluateDifficultyOf(current);
            double crossColumnDifficulty = CrossColumnEvaluator.EvaluateDifficultyOf(current);
            double chordDifficulty = ChordEvaluator.EvaluateDifficultyOf(current);
            double speedDifficulty = SpeedEvaluator.EvaluateDifficultyOf(current);

            double totalDifficulty = LpNorm(column, sameColumnDifficulty, crossColumnDifficulty);
            totalDifficulty = LpNorm(speed, totalDifficulty, speedDifficulty);
            totalDifficulty = LpNorm(chord, totalDifficulty, chordDifficulty);

            return totalDifficulty;
        }

        private static double evaluateTotalDifficultyTail(DifficultyHitObject current)
        {
            double holdingDifficulty = HoldingEvaluator.EvaluateDifficultyOf(current);
            double releaseDifficulty = ReleaseEvaluator.EvaluateDifficultyOf(current);

            double totalDifficulty = LpNorm(hold, holdingDifficulty, releaseDifficulty);

            return totalDifficulty;
        }
    }
}
