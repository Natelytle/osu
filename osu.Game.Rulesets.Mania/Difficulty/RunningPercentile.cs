// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class RunningPercentile
    {
        private readonly double percentile;
        private readonly PriorityQueue<double, double> lowerHalf = new PriorityQueue<double, double>();
        private readonly PriorityQueue<double, double> upperHalf = new PriorityQueue<double, double>();
        private int count;

        public RunningPercentile(double percentile)
        {
            this.percentile = percentile;
        }

        public double Value { get; private set; }

        public void Add(double value)
        {
            count++;

            if (lowerHalf.Count == 0 || value <= Value)
                lowerHalf.Enqueue(value, -value);
            else
                upperHalf.Enqueue(value, value);

            int targetSize = Math.Clamp((int)Math.Round((count - 1) * percentile), 0, count - 1) + 1;

            while (lowerHalf.Count > targetSize)
            {
                double moved = lowerHalf.Dequeue();
                upperHalf.Enqueue(moved, moved);
            }

            while (lowerHalf.Count < targetSize)
            {
                double moved = upperHalf.Dequeue();
                lowerHalf.Enqueue(moved, -moved);
            }

            Value = lowerHalf.Peek();
        }
    }
}
