// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public class AccuracyProbabilityEvaluator
    {
        public JudgementProbabilities EvaluateProbabilitiesAt(DifficultyHitObject current, double skill)
        {
            return new JudgementProbabilities();
        }
    }
}
