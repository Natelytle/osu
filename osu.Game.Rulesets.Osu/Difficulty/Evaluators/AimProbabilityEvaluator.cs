// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public class AimProbabilityEvaluator
    {
        public JudgementProbabilities EvaluateProbabilitiesAt(DifficultyHitObject current, double jumpSkill, double flowSkill, double precisionSkill, double agilitySkill)
        {
            return new JudgementProbabilities();
        }
    }
}
