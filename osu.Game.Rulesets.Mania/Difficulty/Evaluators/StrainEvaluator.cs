// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public class StrainEvaluator
    {
        public static AccuracyDifficulties EvaluateDifficultiesOf(ManiaDifficultyHitObject current)
        {
            AccuracyDifficulties jackDifficulties = new AccuracyDifficulties(JackEvaluator.GetDifficultyOf(current), AccuracyDifficulties.Skillset.Jack);
            AccuracyDifficulties crossColumnDifficulties = new AccuracyDifficulties(CrossColumnEvaluator.GetDifficultyOf(current), AccuracyDifficulties.Skillset.Default);
            AccuracyDifficulties pressingDifficulties = new AccuracyDifficulties(PressingIntensityEvaluator.GetDifficultyOf(current), AccuracyDifficulties.Skillset.Default);
            AccuracyDifficulties releaseDifficulties = new AccuracyDifficulties(ReleaseEvaluator.GetDifficultyOf(current), AccuracyDifficulties.Skillset.Default);

            double unevenness = UnevennessEvaluator.GetValueOf(current);
            double activeKeyCount = AKCEvaluator.GetValueOf(current);
            double localNoteCount = LNCEvaluator.GetValueOf(current);

            // Adjust unevenness impact based on how many keys are active
            double unevennessKeyAdjustment = 1.0;
            if (unevenness > 0.0 && activeKeyCount > 0.0)
                unevennessKeyAdjustment = Math.Pow(unevenness, 3.0 / activeKeyCount);

            // Combine unevenness with same-column difficulty to get our jack difficulty for this note
            jackDifficulties *= unevennessKeyAdjustment;
            jackDifficulties = AccuracyDifficulties.Pow(jackDifficulties, 1.5) * 0.4;

            // Nerf our release difficulties when there's a low number of active columns, then multiply it based on the number of notes around this one.
            releaseDifficulties *= DifficultyCalculationUtils.Smoothstep(activeKeyCount, 0, 4);
            releaseDifficulties *= 35.0 / (localNoteCount + 8.0);

            // Combine unevenness with pressing intensity and release difficulty to get our pressing difficulty for this note
            AccuracyDifficulties pressingComponent = (pressingDifficulties + releaseDifficulties) * Math.Pow(unevenness, 2.0 / 3.0);
            pressingComponent = AccuracyDifficulties.Pow(pressingComponent, 1.5) * 0.6;

            // Main strain difficulty combining both components
            AccuracyDifficulties totalStrainDifficulties = AccuracyDifficulties.Pow(jackDifficulties + pressingComponent, 2.0 / 3.0);

            // Cross-column coordination component
            AccuracyDifficulties twistComponent = (crossColumnDifficulties * unevennessKeyAdjustment) / (crossColumnDifficulties + totalStrainDifficulties + 1.0);
            twistComponent *= AccuracyDifficulties.Pow(twistComponent, 0.5);

            AccuracyDifficulties finalDifficulties = AccuracyDifficulties.Pow(totalStrainDifficulties, 0.5) * twistComponent * 2.7 + totalStrainDifficulties * 0.27;

            return finalDifficulties;
        }
    }
}
