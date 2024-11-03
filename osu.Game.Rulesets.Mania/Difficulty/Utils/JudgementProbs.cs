// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public readonly struct JudgementProbs
    {
        public JudgementProbs(double pMax, double p300, double p200, double p100, double p50)
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

        public double Score => 320 * pMax + 300 * p300 + 200 * p200 + 100 * p100 + 50 * p50;

        public double Variance => (320 - Score) * (320 - Score) * pMax +
                                  (300 - Score) * (300 - Score) * p300 +
                                  (200 - Score) * (200 - Score) * p200 +
                                  (100 - Score) * (100 - Score) * p100 +
                                  (50 - Score) * (50 - Score) * p50 +
                                  -Score * -Score * p0;
    }
}
