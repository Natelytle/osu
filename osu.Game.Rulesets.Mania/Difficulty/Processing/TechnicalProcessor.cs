// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Processing
{
    public class TechnicalProcessor : IDifficultyProcessor
    {
        private const double strain_decay_base = 0.06696;

        private static readonly AccuracyValueMultipliers multipliers = new AccuracyValueMultipliers
        (
            multiplierAtSS: 1.46,
            multiplierAt995: 1.38,
            multiplierAt99: 1.25,
            multiplierAt98: 1.10,
            multiplierAt95: 0.88,
            multiplierAt90: 0.7,
            multiplierAt85: 0.55,
            multiplierAt80: 0.25
        );

        public double CurrentStrain { get; private set; }

        private const int rhythm_window = 10;
        private const int variety_window = 8;

        private readonly Queue<double> recentIrregularities = new Queue<double>();
        private double irregularitySum;

        private readonly Queue<(int rhythmClass, int direction)> recentShapes = new Queue<(int, int)>();

        private double previousDeltaTime = -1.0;

        public void ProcessStrainFor(DifficultyHitObject current)
        {
            CurrentStrain *= DiffUtils.Pow(strain_decay_base, current.DeltaTime / 1000);

            var hitObject = (ManiaDifficultyHitObject)current;

            if (hitObject.DeltaTime < ChordUtils.CHORD_TOLERANCE_MS)
                return;

            double rhythmIrregularity = TechnicalEvaluator.EvaluateRhythmIrregularityOf(hitObject, previousDeltaTime);
            previousDeltaTime = hitObject.DeltaTime;

            CurrentStrain += TechnicalEvaluator.EvaluateDifficultyOf(hitObject, rhythmIrregularity, patternVariety(hitObject), windowedIrregularity(rhythmIrregularity));
        }

        private double windowedIrregularity(double rhythmIrregularity)
        {
            recentIrregularities.Enqueue(rhythmIrregularity);
            irregularitySum += rhythmIrregularity;

            while (recentIrregularities.Count > rhythm_window)
                irregularitySum -= recentIrregularities.Dequeue();

            return irregularitySum / recentIrregularities.Count;
        }

        private double patternVariety(ManiaDifficultyHitObject hitObject)
        {
            recentShapes.Enqueue(TechnicalEvaluator.EvaluateShapeOf(hitObject));

            while (recentShapes.Count > variety_window)
                recentShapes.Dequeue();

            return TechnicalEvaluator.EvaluatePatternVarietyOf(recentShapes.Distinct().Count());
        }

        public AccuracyDifficulties TransformStrainToAccuracyDifficulties(double strain) => new AccuracyDifficulties(strain, multipliers);
    }
}
