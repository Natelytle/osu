using System;
using System.Collections.Generic;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class TotalAccuracyCurve
    {
        private const double min_ramp_width = 1e-6;
        private const double min_difficulty_bin = 0.1;
        private const double bin_ratio = 1.05;

        private int size = 16;
        private double[] edges;
        private double[] slopeSum;
        private double[] gain;
        private List<(double Skill, double Delta)>?[] bucketEvents;

        private double baseAccuracy;
        private double maxAccuracy;

        public TotalAccuracyCurve()
        {
            edges = buildEdges(size);
            slopeSum = new double[2 * size];
            gain = new double[2 * size];
            bucketEvents = new List<(double, double)>?[size];
        }

        public void AddNote(AccuracyDifficulties difficulties)
        {
            double[] table = AccuracyValueMultipliers.ACCURACY_VALUES;

            maxAccuracy += table[0];

            if (difficulties.BaseDifficulty <= 0)
            {
                baseAccuracy += table[0];
                return;
            }

            baseAccuracy += table[^1];

            for (int i = 0; i < table.Length - 1; i++)
            {
                double upper = difficulties.GetDifficulty(i);
                double lower = Math.Min(difficulties.GetDifficulty(i + 1), upper - min_ramp_width);
                double slope = (table[i] - table[i + 1]) / (upper - lower);

                insert(lower, slope);
                insert(upper, -slope);
            }
        }

        public double TotalAccuracy() => maxAccuracy;

        public double TotalAccuracyAt(double skill)
        {
            int k = bucketOf(skill);

            // Fold buckets [0, k): accS = slope entering bucket k, accV = value at edge(k).
            // k may equal size (skill beyond every allocated bucket), which folds the whole tree.
            double accS = 0, accV = 0;
            int node = 1, lo = 0, hi = size;

            while (k > lo)
            {
                if (k >= hi)
                {
                    accV += gain[node] + accS * (edges[hi] - edges[lo]);
                    accS += slopeSum[node];
                    break;
                }

                int mid = (lo + hi) >> 1;

                if (k <= mid)
                {
                    node = 2 * node;
                    hi = mid;
                }
                else
                {
                    int l = 2 * node;
                    accV += gain[l] + accS * (edges[mid] - edges[lo]);
                    accS += slopeSum[l];
                    node = l + 1;
                    lo = mid;
                }
            }

            double result = baseAccuracy + accV + accS * (skill - edge(k));

            // Events inside this bucket that have already happened.
            if (k < size && bucketEvents[k] is { } events)
            {
                foreach (var (eventSkill, delta) in events)
                {
                    if (eventSkill <= skill)
                        result += delta * (skill - eventSkill);
                }
            }

            return Math.Clamp(result, baseAccuracy, maxAccuracy);
        }

        // Lower edge of bucket i (== upper edge of bucket i - 1).
        private double computeEdge(int i) => i == 0 ? 0 : min_difficulty_bin * Math.Pow(bin_ratio, i - 1);

        private double edge(int i) => i < edges.Length ? edges[i] : computeEdge(i);

        private double[] buildEdges(int buckets)
        {
            var e = new double[buckets + 1];
            for (int i = 0; i <= buckets; i++)
                e[i] = computeEdge(i);
            return e;
        }

        private int bucketOf(double skill)
        {
            if (!(skill >= min_difficulty_bin)) // also catches NaN and negatives
                return 0;

            double raw = Math.Log(skill / min_difficulty_bin) / Math.Log(bin_ratio);

            int b = 1 + (int)raw;

            // Correct for floating-point error in the log.
            while (b > 1 && skill < edge(b)) b--;
            while (skill >= edge(b + 1)) b++;

            return b;
        }

        private void insert(double skill, double delta)
        {
            int b = bucketOf(skill);

            if (b >= size)
                grow(b);

            (bucketEvents[b] ??= new List<(double, double)>()).Add((skill, delta));

            int leaf = size + b;
            slopeSum[leaf] += delta;
            gain[leaf] += delta * (edges[b + 1] - skill);

            // Walk up: the node covering [lo, lo + w) is (size + lo) / w.
            for (int w = 2; w <= size; w <<= 1)
                pull(b & ~(w - 1), w);
        }

        private void pull(int lo, int w)
        {
            int p = (size + lo) / w;
            int l = 2 * p, r = l + 1;

            slopeSum[p] = slopeSum[l] + slopeSum[r];
            gain[p] = gain[l] + gain[r] + slopeSum[l] * (edges[lo + w] - edges[lo + w / 2]);
        }

        private void grow(int needed)
        {
            int newSize = size;
            while (newSize <= needed) newSize <<= 1;

            double[] newSlope = new double[2 * newSize];
            double[] newGain = new double[2 * newSize];

            // Leaf values depend only on their own bucket's edges, which don't change with size.
            Array.Copy(slopeSum, size, newSlope, newSize, size);
            Array.Copy(gain, size, newGain, newSize, size);

            Array.Resize(ref bucketEvents, newSize);

            size = newSize;
            edges = buildEdges(size);
            slopeSum = newSlope;
            gain = newGain;

            // Rebuild internal nodes bottom-up.
            for (int w = 2; w <= size; w <<= 1)
            {
                for (int lo = 0; lo < size; lo += w)
                    pull(lo, w);
            }
        }
    }
}
