// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Processing
{
    public interface IReadonlyDifficultyProcessor
    {
        double CurrentStrain { get; }

        AccuracyDifficulties TransformStrainToAccuracyDifficulties(double strain);
    }
}
