// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Difficulty.Utils.AccuracySimulation
{
    public readonly struct JudgementProbabilities
    {
        public JudgementProbabilities(double pMax, double p300, double p200, double p100, double p50)
        {
            this.pMax = pMax;
            this.p300 = p300;
            this.p200 = p200;
            this.p100 = p100;
            this.p50 = p50;
            p0 = 1 - (pMax + p300 + p200 + p100 + p50);
        }

        private readonly double pMax;
        private readonly double p300;
        private readonly double p200;
        private readonly double p100;
        private readonly double p50;
        private readonly double p0;

        public double Score => AccuracySimulator.MAX_JUDGEMENT_WEIGHT * pMax + 300 * p300 + 200 * p200 + 100 * p100 + 50 * p50;

        public double Variance => (AccuracySimulator.MAX_JUDGEMENT_WEIGHT - Score) * (AccuracySimulator.MAX_JUDGEMENT_WEIGHT - Score) * pMax +
                                  (300 - Score) * (300 - Score) * p300 +
                                  (200 - Score) * (200 - Score) * p200 +
                                  (100 - Score) * (100 - Score) * p100 +
                                  (50 - Score) * (50 - Score) * p50 +
                                  (0 - Score) * (0 - Score) * p0;

        // Due to real world factors (such as variance in skill), standard deviation is actually around 2.5x higher than it appears.
        // We account for this by multiplying variance used in the model by 2.5^2.
        public double AdjustedVariance => Variance * 6.25;
    }
}
