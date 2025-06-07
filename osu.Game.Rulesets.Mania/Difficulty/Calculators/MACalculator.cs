// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Mania.Difficulty.Calculators
{
    public class Note
    {
        public int Column; // key/column index
        public int Head; // head (hit) time
        public int Tail; // tail time (or -1 if not LN)
        public int ColumnIndex;

        public Note(int column, int head, int tail)
        {
            Column = column;
            Head = head;
            Tail = tail;
            ColumnIndex = 0;
        }
    }

    /// <summary>
    /// Used to store various computed values at a corner (time point).
    /// </summary>
    public class CornerData
    {
        public double Time;
        public double Jbar;
        public double Xbar;
        public double Pbar;
        public double Abar;
        public double Rbar;
        public double C;
        public double Ks;
        public double D;
        public double Weight;
    }

    public struct SrParams
    {
        public double Sr;
        public double Spikiness;
        public double Switches;
    }

    /// <summary>
    /// MACalculator computes the difficulty rating from noteSeq (a sequence of notes).
    /// All methods are static.
    /// </summary>
    public static class MaCalculator
    {
        /// <param name="noteSeq">List of Note objects.</param>
        /// <param name="noteSeqByColumn">Per column lists of Note objects.</param>
        /// <param name="keyCount">Number of keys (columns).</param>
        /// <param name="hitWindowLeniency">A leniency value derived from the hit window.</param>
        /// <param name="containsCl">Whether the CL mod is activated.</param>
        /// <returns>The computed difficulty (Level) as an int.</returns>
        public static SrParams Calculate(List<Note> noteSeq, List<List<Note>> noteSeqByColumn, int keyCount, double hitWindowLeniency, bool containsCl)
        {
            // Fixed tuning constants.
            const double lambda_n = 5;
            const double lambda_1 = 0.11;
            const double lambda_3 = 24.0;
            const double lambda2 = 6.0;
            const double lambda_4 = 0.8;
            const double w0 = 0.4;
            const double w1 = 2.7;
            const double p1 = 1.5;
            const double w2 = 0.27;
            const double p0 = 1.0;

            // --- Sort notes by head time, then by column ---
            noteSeq.Sort((a, b) =>
            {
                int cmp = a.Head.CompareTo(b.Head);
                return cmp == 0 ? a.Column.CompareTo(b.Column) : cmp;
            });

            // --- Group notes by column ---
            Dictionary<int, List<Note>> noteDict = new Dictionary<int, List<Note>>();

            for (int i = 0; i < keyCount; i++)
            {
                noteDict[i] = new List<Note>();
            }

            foreach (var note in noteSeq)
            {
                noteDict[note.Column].Add(note);
            }

            foreach (var list in noteDict.Values)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var note = list[i];
                    note.ColumnIndex = i;
                }
            }

            // --- Long notes ---
            List<Note> lnSeq = noteSeq.Where(n => n.Tail >= 0).ToList();
            List<Note> tailSeq = lnSeq.OrderBy(n => n.Tail).ToList();

            Dictionary<int, List<Note>> lnDict = new Dictionary<int, List<Note>>();

            foreach (var note in lnSeq)
            {
                if (!lnDict.ContainsKey(note.Column))
                    lnDict[note.Column] = new List<Note>();
                lnDict[note.Column].Add(note);
            }

            int maxHead = noteSeq.Max(n => n.Head);
            int maxTail = noteSeq.Max(n => n.Tail);
            int maxT = Math.Max(maxHead, maxTail) + 1;

            // --- Determine Corner Times for base variables and for A ---
            HashSet<int> cornersBase = new HashSet<int>();

            foreach (var note in noteSeq)
            {
                cornersBase.Add(note.Head);
                if (note.Tail >= 0)
                    cornersBase.Add(note.Tail);
            }

            foreach (int s in cornersBase.ToList())
            {
                cornersBase.Add(s + 501);
                cornersBase.Add(s - 499);
                cornersBase.Add(s + 1);
            }

            cornersBase.Add(0);
            cornersBase.Add(maxT);
            List<int> cornersBaseList = cornersBase.Where(s => s >= 0 && s <= maxT).ToList();
            cornersBaseList.Sort();

            HashSet<int> cornersA = new HashSet<int>();

            foreach (var note in noteSeq)
            {
                cornersA.Add(note.Head);
                if (note.Tail >= 0)
                    cornersA.Add(note.Tail);
            }

            foreach (int s in cornersA.ToList())
            {
                cornersA.Add(s + 1000);
                cornersA.Add(s - 1000);
            }

            cornersA.Add(0);
            cornersA.Add(maxT);
            List<int> cornersAList = cornersA.Where(s => s >= 0 && s <= maxT).ToList();
            cornersAList.Sort();

            HashSet<int> allCornersSet = new HashSet<int>(cornersBaseList);
            allCornersSet.UnionWith(cornersAList);
            List<int> allCornersList = allCornersSet.ToList();
            allCornersList.Sort();

            double[] allCorners = allCornersList.Select(val => (double)val).ToArray();
            double[] baseCorners = cornersBaseList.Select(val => (double)val).ToArray();
            double[] aCorners = cornersAList.Select(val => (double)val).ToArray();

            // Calculate KU
            // Allocate a boolean active–flag array per key over baseCorners.
            // (New bool arrays are false by default; no need to loop and set to false.)
            Dictionary<int, bool[]> keyUsage = new Dictionary<int, bool[]>();

            for (int k = 0; k < keyCount; k++)
            {
                keyUsage[k] = new bool[baseCorners.Length];
            }

            // For each key, mark baseCorners that lie within the “active” interval for each note.
            // The active interval for a note is [max(note.Head - 150, 0), (note.Tail < 0 ? note.Head + 150 : min(note.Tail + 150, T - 1))).
            for (int k = 0; k < keyCount; k++)
            {
                // Get the note sequence for key k.
                List<Note> notes = noteSeqByColumn[k];

                foreach (var note in notes)
                {
                    int activeStart = Math.Max(note.Head - 150, 0);
                    int activeEnd = note.Tail < 0 ? note.Head + 150 : Math.Min(note.Tail + 150, maxT - 1);
                    // Use binary search to find the first baseCorner >= activeStart.
                    int startIdx = Array.BinarySearch(baseCorners, activeStart);
                    if (startIdx < 0)
                        startIdx = ~startIdx;
                    // Advance pointer until the base corner is no longer less than activeEnd.
                    int idx = startIdx;

                    while (idx < baseCorners.Length && baseCorners[idx] < activeEnd)
                    {
                        keyUsage[k][idx] = true;
                        idx++;
                    }
                }
            }

            // For each baseCorner, build a list of active keys.
            List<List<int>> kuSCols = new List<List<int>>(baseCorners.Length);

            for (int i = 0; i < baseCorners.Length; i++)
            {
                List<int> activeCols = new List<int>();

                for (int k = 0; k < keyCount; k++)
                {
                    if (keyUsage[k][i])
                        activeCols.Add(k);
                }

                kuSCols.Add(activeCols);
            }

            Dictionary<int, double[]> keyUsage400 = new Dictionary<int, double[]>();

            for (int k = 0; k < keyCount; k++)
            {
                keyUsage400[k] = new double[baseCorners.Length];
            }

            for (int k = 0; k < keyCount; k++)
            {
                // Get the note sequence for key k.
                List<Note> notes = noteSeqByColumn[k];

                foreach (var note in notes)
                {
                    int activeStart = Math.Max(note.Head, 0);
                    int activeEnd = note.Tail < 0 ? note.Head : Math.Min(note.Tail, maxT - 1);
                    // Use binary search to find the first baseCorner >= activeStart.
                    int start400Idx = Array.BinarySearch(baseCorners, (double)activeStart - 400);
                    int startIdx = Array.BinarySearch(baseCorners, activeStart);
                    int end400Idx = Array.BinarySearch(baseCorners, (double)activeEnd + 400);
                    int endIdx = Array.BinarySearch(baseCorners, activeEnd);

                    if (start400Idx < 0)
                        start400Idx = ~start400Idx;
                    if (startIdx < 0)
                        startIdx = ~startIdx;
                    if (end400Idx < 0)
                        end400Idx = ~end400Idx;
                    if (endIdx < 0)
                        endIdx = ~endIdx;

                    for (int i = startIdx; i < endIdx; i++)
                        keyUsage400[k][i] += 3.75 + Math.Min(activeEnd - activeStart, 1500) / 150.0;

                    for (int i = start400Idx; i < startIdx; i++)
                        keyUsage400[k][i] += 3.75 - 3.75 / Math.Pow(400, 2) * Math.Pow(baseCorners[i] - activeStart, 2);

                    for (int i = endIdx; i < end400Idx; i++)
                        keyUsage400[k][i] += 3.75 - 3.75 / Math.Pow(400, 2) * Math.Pow(Math.Abs(baseCorners[i] - activeEnd), 2);
                }
            }

            double[] anchor = new double[baseCorners.Length];

            for (int i = 0; i < baseCorners.Length; i++)
            {
                double[] counts = new double[keyCount];

                for (int k = 0; k < keyCount; k++)
                {
                    counts[k] = keyUsage400[k][i];
                }

                Array.Sort(counts);
                Array.Reverse(counts);

                double[] nonZeroCounts = counts.Where(c => c > 0).ToArray();
                int countLength = nonZeroCounts.Length;

                if (countLength > 1)
                {
                    double walk = Enumerable.Range(0, countLength - 1)
                                            .Select(j => nonZeroCounts[j] * (1 - 4 * Math.Pow(0.5 - nonZeroCounts[j + 1] / nonZeroCounts[j], 2)))
                                            .Sum();

                    double maxWalk = Enumerable.Range(0, countLength - 1)
                                               .Select(j => nonZeroCounts[j])
                                               .Sum();

                    anchor[i] = walk / maxWalk;
                }
                else
                {
                    anchor[i] = 0;
                }
            }

            for (int i = 0; i < anchor.Length; i++)
            {
                anchor[i] = 1 + Math.Min(anchor[i] - 0.18, 5 * Math.Pow(anchor[i] - 0.22, 3));
            }

            // --- Section 2.3: Compute Jbar ---
            // Console.WriteLine("2.3");
            Func<double, double> jackNerfer = delta => 1 - 7e-5 * Math.Pow(0.15 + Math.Abs(delta - 0.08), -4);

            // Allocate arrays for each column. For each column k,
            // J_ks[k] will store the unsmoothed J values on baseCorners,
            // and delta_ks[k] will store the corresponding delta values.
            Dictionary<int, double[]> jKs = new Dictionary<int, double[]>();
            Dictionary<int, double[]> deltaKs = new Dictionary<int, double[]>();

            for (int k = 0; k < keyCount; k++)
            {
                jKs[k] = new double[baseCorners.Length];
                deltaKs[k] = new double[baseCorners.Length];

                // Initialize delta_ks to a large value.
                for (int j = 0; j < baseCorners.Length; j++)
                {
                    deltaKs[k][j] = 1e9;
                }
            }

            // For each column, compute unsmoothed J using a linear sweep over baseCorners.
            for (int k = 0; k < keyCount; k++)
            {
                List<Note> notes = noteSeqByColumn[k];
                int pointer = 0; // pointer over the baseCorners array

                // For each adjacent note pair in the column.
                for (int i = 0; i < notes.Count - 1; i++)
                {
                    int start = notes[i].Head;
                    int end = notes[i + 1].Head;
                    double delta = 0.001 * (end - start);
                    double val = 1.0 / delta * (1.0 / (delta + lambda_1 * Math.Pow(hitWindowLeniency, 0.25)));
                    double jVal = val * jackNerfer(delta);

                    // Advance pointer until we reach the first base corner >= start.
                    while (pointer < baseCorners.Length && baseCorners[pointer] < start)
                    {
                        pointer++;
                    }

                    // For all base corners in [start, end), assign J_val and delta.
                    while (pointer < baseCorners.Length && baseCorners[pointer] < end)
                    {
                        jKs[k][pointer] = jVal;
                        deltaKs[k][pointer] = delta;
                        pointer++;
                    }
                }
            }

            // Smooth each column’s J using a sliding ±500 window.
            Dictionary<int, double[]> jbarKs = new Dictionary<int, double[]>();

            for (int k = 0; k < keyCount; k++)
            {
                jbarKs[k] = SmoothOnCorners(baseCorners, jKs[k], 500, 0.001, "sum");
            }

            // Now, for each base corner, aggregate across columns using the lambda_n–power average.
            double[] jbarBase = new double[baseCorners.Length];

            for (int j = 0; j < baseCorners.Length; j++)
            {
                double num = 0.0, den = 0.0;

                for (int k = 0; k < keyCount; k++)
                {
                    double v = Math.Max(jbarKs[k][j], 0);
                    double weight = 1.0 / deltaKs[k][j];
                    num += Math.Pow(v, lambda_n) * weight;
                    den += weight;
                }

                double avg = num / Math.Max(1e-9, den);
                jbarBase[j] = Math.Pow(avg, 1.0 / lambda_n);
            }

            // Interpolate Jbar from baseCorners to allCorners.
            double[] jbar = interpValues(allCorners, baseCorners, jbarBase);

            // --- Section 2.4: Compute Xbar ---
            // Console.WriteLine("2.4");
            List<List<double>> crossMatrix = new List<List<double>>
            {
                new List<double> { -1 },
                new List<double> { 0.075, 0.075 },
                new List<double> { 0.125, 0.05, 0.125 },
                new List<double> { 0.125, 0.125, 0.125, 0.125 },
                new List<double> { 0.175, 0.25, 0.05, 0.25, 0.175 },
                new List<double> { 0.175, 0.25, 0.175, 0.175, 0.25, 0.175 },
                new List<double> { 0.225, 0.35, 0.25, 0.05, 0.25, 0.35, 0.225 },
                new List<double> { 0.225, 0.35, 0.25, 0.225, 0.225, 0.25, 0.35, 0.225 },
                new List<double> { 0.275, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.275 },
                new List<double> { 0.275, 0.45, 0.35, 0.25, 0.275, 0.275, 0.25, 0.35, 0.45, 0.275 },
                new List<double> { 0.325, 0.55, 0.45, 0.35, 0.25, 0.05, 0.25, 0.35, 0.45, 0.55, 0.325 }
            };

            double[][] fastCross = new double[keyCount + 1][];
            for (int k = 0; k < fastCross.Length; k++)
                fastCross[k] = new double[baseCorners.Length];

            // Allocate an array for each key pair (for k=0..keyCount, total keyCount+1 arrays).
            Dictionary<int, double[]> xKs = new Dictionary<int, double[]>();

            for (int k = 0; k <= keyCount; k++)
            {
                xKs[k] = new double[baseCorners.Length]; // All values default to 0.
            }

            // For each k, compute a step function over the baseCorners that is constant over each interval.
            // Instead of checking every baseCorner for each interval, we “sweep” through baseCorners with a pointer.
            for (int k = 0; k <= keyCount; k++)
            {
                // Determine the merged note sequence for this key–pair:
                List<Note> notesInPair;

                if (k == 0)
                {
                    notesInPair = noteSeqByColumn[0];
                }
                else if (k == keyCount)
                {
                    notesInPair = noteSeqByColumn[keyCount - 1];
                }
                else
                {
                    // Merge the two sorted lists from columns (k–1) and k.
                    notesInPair = mergeSorted(noteSeqByColumn[k - 1], noteSeqByColumn[k]);
                }

                // pointer scans through baseCorners once.
                int pointer = 0;

                for (int i = 1; i < notesInPair.Count; i++)
                {
                    // For this note pair, define the interval [start, end)
                    int start = notesInPair[i - 1].Head;
                    int end = notesInPair[i].Head;
                    double delta = 0.001 * (end - start);
                    double val = 0.16 * Math.Pow(Math.Max(hitWindowLeniency, delta), -2);

                    // Advance the pointer until the current base corner is within [start, end)
                    while (pointer < baseCorners.Length && baseCorners[pointer] < start)
                    {
                        pointer++;
                    }

                    int pointerStart = pointer;

                    // Find the end value
                    while (pointer < baseCorners.Length && baseCorners[pointer] < end)
                    {
                        pointer++;
                    }

                    int pointerEnd = pointer;

                    // if ((k - 1) not in KU_s_cols[idx_start] or (k - 1) not in KU_s_cols[idx_end]) or (k not in KU_s_cols[idx_start] or k not in KU_s_cols[idx_end]):
                    // val*=(1-cross_coeff[k])

                    double crossVal = keyCount < crossMatrix.Count ? crossMatrix[keyCount][k] : 0.4;

                    bool leftKeyNotPresent = !kuSCols[pointerStart].Contains(k - 1) && !kuSCols[pointerEnd].Contains(k - 1);
                    bool keyNotPresent = !kuSCols[pointerStart].Contains(k) && !kuSCols[pointerEnd].Contains(k);

                    if (leftKeyNotPresent || keyNotPresent)
                        val *= 1 - crossVal;

                    // Now assign the value for all baseCorners in [start, end)
                    for (int p = pointerStart; p < pointerEnd; p++)
                    {
                        xKs[k][p] = val;
                        fastCross[k][p] = Math.Max(0, 0.4 * Math.Pow(Math.Max(Math.Max(delta, 0.06), 0.75 * hitWindowLeniency), -2) - 80);
                    }
                }
            }

            // Combine the X_ks values across k using the cross–matrix coefficients.
            // (The cross–matrix for keyCount returns a list of keyCount+1 coefficients.)
            double[] xBase = new double[baseCorners.Length];

            for (int i = 0; i < baseCorners.Length; i++)
            {
                double sum1 = 0.0;
                double sum2 = 0.0;

                for (int k = 0; k <= keyCount; k++)
                {
                    double crossVal = keyCount < crossMatrix.Count ? crossMatrix[keyCount][k] : 0.4;

                    sum1 += xKs[k][i] * crossVal;
                }

                for (int k = 0; k < keyCount; k++)
                {
                    double crossVal = keyCount < crossMatrix.Count ? crossMatrix[keyCount][k] : 0.4;
                    double crossValNext = keyCount < crossMatrix.Count ? crossMatrix[keyCount][k + 1] : 0.4;

                    sum2 += Math.Sqrt(fastCross[k][i] * crossVal * fastCross[k + 1][i] * crossValNext);
                }

                xBase[i] = sum1 + sum2;
            }

            // Smooth and interpolate as in the rest of the algorithm.
            double[] xbarBase = SmoothOnCorners(baseCorners, xBase, 500, 0.001, "sum");
            double[] xbar = interpValues(allCorners, baseCorners, xbarBase);

            // --- Section 2.5: Compute Pbar ---
            // Console.WriteLine("2.5");

            // Build LN_bodies array over time [0, T)
            double[] lnBodies = new double[maxT];
            for (int i = 0; i < maxT; i++)
                lnBodies[i] = 0.0;

            // For each long note, add contributions in three segments:
            //   from h to t0, add nothing;
            //   from t0 to t1, add 1.3;
            foreach (var note in lnSeq)
            {
                int h = note.Head;
                int t = note.Tail;
                int t0 = Math.Min(h + 60, t);
                int t1 = Math.Min(h + 120, t);
                for (int i = t0; i < t1; i++)
                    lnBodies[i] += 1.3;
                for (int i = t1; i < t; i++)
                    lnBodies[i] += 1.0;
            }

            // adjust the LN bodies count - this helps with high key inverse
            for (int i = 0; i < lnBodies.Length; i++)
            {
                lnBodies[i] = Math.Min(lnBodies[i], 2.5 + 0.5 * lnBodies[i]);
            }

            // Compute cumulative sum over LN_bodies
            double[] cumsumLn = new double[maxT + 1];
            cumsumLn[0] = 0.0;

            for (int i = 1; i <= maxT; i++)
            {
                cumsumLn[i] = cumsumLn[i - 1] + lnBodies[i - 1];
            }

            // LN_sum returns the exact sum over LN_bodies in the interval [a, b)
            Func<int, int, double> lnSum = (a, b) => cumsumLn[b] - cumsumLn[a];

            // Stream Booster
            Func<double, double> streamBooster = delta =>
            {
                double val = 7.5 / delta;
                if (val > 160 && val < 360)
                    return 1 + 1.7e-7 * (val - 160) * Math.Pow(val - 360, 2);

                return 1.0;
            };

            // Allocate P_step on the base grid.
            double[] pStep = new double[baseCorners.Length];
            for (int i = 0; i < baseCorners.Length; i++)
                pStep[i] = 0.0;

            // Process each adjacent pair of notes in noteSeq. Since noteSeq is sorted by head time,
            // the interval [h_l, h_r) for each pair will also be in increasing order.
            // We maintain a pointer into baseCorners that advances monotonically.
            int pointerP = 0;

            for (int i = 0; i < noteSeq.Count - 1; i++)
            {
                int hL = noteSeq[i].Head;
                int hR = noteSeq[i + 1].Head;
                double deltaTime = hR - hL;

                if (deltaTime < 1e-9)
                {
                    // Handle Dirac delta spikes when consecutive notes have identical times.
                    // Find the base corner exactly equal to h_l (using binary search is acceptable here)
                    int idx = Array.BinarySearch(baseCorners, hL);
                    if (idx < 0)
                        idx = ~idx;

                    if (idx < baseCorners.Length && Math.Abs(baseCorners[idx] - hL) < 1e-9)
                    {
                        double spike = 1000 * Math.Pow(0.02 * (4 / hitWindowLeniency - lambda_3), 0.25);
                        pStep[idx] += spike;
                    }

                    continue;
                }

                // Compute constant values for this interval.
                double delta = 0.001 * deltaTime;
                double v = 1 + lambda2 * 0.001 * lnSum(hL, hR);
                double bVal = streamBooster(delta);
                double inc;

                if (delta < 2 * hitWindowLeniency / 3)
                {
                    inc = 1.0 / delta * Math.Pow(0.08 * (1.0 / hitWindowLeniency) *
                                                 (1 - lambda_3 * (1.0 / hitWindowLeniency) * Math.Pow(delta - hitWindowLeniency / 2, 2)), 0.25) * Math.Max(bVal, v);
                }
                else
                {
                    inc = 1.0 / delta * Math.Pow(0.08 * (1.0 / hitWindowLeniency) *
                                                 (1 - lambda_3 * (1.0 / hitWindowLeniency) * Math.Pow(hitWindowLeniency / 6, 2)), 0.25) * Math.Max(bVal, v);
                }

                // Advance pointerP until the current base corner is at least h_l.
                while (pointerP < baseCorners.Length && baseCorners[pointerP] < hL)
                    pointerP++;

                // For every base corner in the interval [h_l, h_r), add the increment.
                while (pointerP < baseCorners.Length && baseCorners[pointerP] < hR)
                {
                    pStep[pointerP] += Math.Min(inc * anchor[pointerP], Math.Max(inc, inc * 2 - 10));
                    pointerP++;
                }
                // Since noteSeq is sorted, pointerP never resets backwards.
            }

            // Smooth and interpolate P_step as before.
            double[] pbarBase = SmoothOnCorners(baseCorners, pStep, 500, 0.001, "sum");
            double[] pbar = interpValues(allCorners, baseCorners, pbarBase);

            // --- Section 2.6: Compute Abar ---
            // Console.WriteLine("2.6");

            // Compute dks: For each baseCorner, for each adjacent pair of active keys,
            // compute the difference measure: |delta_ks[k0] - delta_ks[k1]| + max(0, max(delta_ks[k0], delta_ks[k1]) - 0.3).
            Dictionary<int, double[]> dks = new Dictionary<int, double[]>();

            for (int k = 0; k < keyCount - 1; k++)
            {
                dks[k] = new double[baseCorners.Length];
            }

            for (int i = 0; i < baseCorners.Length; i++)
            {
                List<int> cols = kuSCols[i];

                // Only if there are at least two active keys.
                for (int j = 0; j < cols.Count - 1; j++)
                {
                    int k0 = cols[j];
                    int k1 = cols[j + 1];
                    dks[k0][i] = Math.Abs(deltaKs[k0][i] - deltaKs[k1][i]) +
                                 0.4 * Math.Max(0, Math.Max(deltaKs[k0][i], deltaKs[k1][i]) - 0.11);
                }
            }

            // Compute A_step on the A–grid (ACorners). Start with a default value of 1.0.
            double[] aStep = new double[aCorners.Length];
            for (int i = 0; i < aCorners.Length; i++)
                aStep[i] = 1.0;

            // For each A–corner, determine the corresponding value from the base grid.
            // We do this by finding the nearest baseCorner using binary search, then using the active key list there.
            for (int i = 0; i < aCorners.Length; i++)
            {
                double s = aCorners[i];
                int idx = Array.BinarySearch(baseCorners, s);
                if (idx < 0)
                    idx = ~idx;
                if (idx >= baseCorners.Length)
                    idx = baseCorners.Length - 1;
                // Get the list of active keys at this base corner.
                List<int> cols = kuSCols[idx];

                // For each adjacent pair of active keys in that list, adjust A_step.
                for (int j = 0; j < cols.Count - 1; j++)
                {
                    int k0 = cols[j];
                    int k1 = cols[j + 1];
                    double dVal = dks[k0][idx];
                    if (dVal < 0.02)
                        aStep[i] *= Math.Min(0.75 + 0.5 * Math.Max(deltaKs[k0][idx], deltaKs[k1][idx]), 1);
                    else if (dVal < 0.07)
                        aStep[i] *= Math.Min(0.65 + 5 * dVal + 0.5 * Math.Max(deltaKs[k0][idx], deltaKs[k1][idx]), 1);
                }
            }

            // Finally, smooth A_step on the ACorners with a ±500 window (using average smoothing),
            // then interpolate Abar from the ACorners to the overall grid.
            double[] abarA = SmoothOnCorners(aCorners, aStep, 250, 1.0, "avg");
            double[] abar = interpValues(allCorners, aCorners, abarA);

            // --- Section 2.7: Compute Rbar ---
            // Console.WriteLine("2.7");

            double[] rBase = new double[baseCorners.Length];
            double[] iArr = new double[baseCorners.Length];

            for (int i = 0; i < rBase.Length; i++)
            {
                rBase[i] = 0;
                iArr[i] = 0;
            }

            double[] iList = new double[tailSeq.Count];

            for (int i = 0; i < iList.Length; i++)
            {
                iList[i] = 0;
            }

            for (int note = 0; note < tailSeq.Count; note++)
            {
                Note currentNote = tailSeq[note];

                int nextNoteIndex = currentNote.ColumnIndex + 1;

                bool nextNoteExists = !(nextNoteIndex >= noteSeqByColumn[currentNote.Column].Count);

                Note? nextNote = nextNoteExists ? noteSeqByColumn[currentNote.Column][nextNoteIndex] : null;

                double currentI = 0.001 * Math.Abs(currentNote.Tail - currentNote.Head - 80.0) / hitWindowLeniency;

                if (nextNote is null)
                {
                    iList[note] = 2 / (2 + Math.Exp(-5 * (currentI - 0.75)));
                    continue;
                }

                double nextI = 0.001 * Math.Abs(nextNote.Head - currentNote.Tail - 80.0) / hitWindowLeniency;

                iList[note] = 2 / (2 + Math.Exp(-5 * (currentI - 0.75)) + Math.Exp(-5 * (nextI - 0.75)));
            }

            int previousIdxStart = 0;

            for (int i = 0; i < tailSeq.Count - 1; i++)
            {
                Note note = tailSeq[i];
                Note nextNote = tailSeq[i + 1];

                int startTime = note.Tail;
                int endTime = nextNote.Tail;

                int idxStart = -1;

                for (int j = previousIdxStart; j < baseCorners.Length; j++)
                {
                    if (baseCorners[j] >= startTime)
                    {
                        idxStart = j;
                        previousIdxStart = j;
                        break;
                    }
                }

                if (idxStart == -1)
                {
                    continue;
                }

                double deltaR = 0.001 * (nextNote.Tail - note.Tail);

                for (int j = idxStart; j < baseCorners.Length; j++)
                {
                    if (baseCorners[j] >= endTime)
                        break;

                    iArr[j] = 1 + iList[i];

                    rBase[j] = 0.08 * Math.Pow(deltaR, -1.0 / 2.0) * (1 / hitWindowLeniency) * (1 + lambda_4 * (iList[i] + iList[i + 1]));
                }
            }

            // Smooth and interpolate as in the rest of the algorithm.
            double[] rbarBase = SmoothOnCorners(baseCorners, rBase, 500, 0.001, "sum");
            double[] rbar = interpValues(allCorners, baseCorners, rbarBase);

            // --- Section 3: Compute C and Ks ---
            // Console.WriteLine("3");
            var noteHitTimes = noteSeq.Select(n => n.Head).ToList();
            var noteHitTimesV2 = noteSeq.Select(n => n.Head)
                                        .Concat(noteSeq.Where(n => n.Tail >= 0)
                                                       .Select(n => n.Tail))
                                        .ToList();
            noteHitTimes.Sort();
            noteHitTimesV2.Sort();
            double[] cStep = new double[baseCorners.Length];
            double[] cStepV2 = new double[baseCorners.Length];

            for (int i = 0; i < baseCorners.Length; i++)
            {
                double s = baseCorners[i];
                double low = s - 500;
                double high = s + 500;
                int cntLow = lowerBound(noteHitTimes, (int)low);
                int cntHigh = lowerBound(noteHitTimes, (int)high);
                int cnt = cntHigh - cntLow;
                cStep[i] = cnt;
            }

            for (int i = 0; i < baseCorners.Length; i++)
            {
                double s = baseCorners[i];
                double low = s - 500;
                double high = s + 500;
                int cntLow = lowerBound(noteHitTimesV2, (int)low);
                int cntHigh = lowerBound(noteHitTimesV2, (int)high);
                int cnt = cntHigh - cntLow;
                cStepV2[i] = cnt;
            }

            double[] cArr = stepInterp(allCorners, baseCorners, cStep);
            double[] cArrV2 = stepInterp(allCorners, baseCorners, cStepV2);

            double[] ksStep = new double[baseCorners.Length];

            for (int i = 0; i < baseCorners.Length; i++)
            {
                int cntActive = 0;

                for (int k = 0; k < keyCount; k++)
                {
                    if (keyUsage[k][i])
                        cntActive++;
                }

                ksStep[i] = Math.Max(cntActive, 1);
            }

            double[] ksBase = ksStep;
            double[] ksArr = stepInterp(allCorners, baseCorners, ksBase);

            // --- Final Computations: Compute S, T, D ---
            int n = allCorners.Length;
            double[] sAll = new double[n];
            double[] tAll = new double[n];
            double[] dAll = new double[n];

            for (int i = 0; i < n; i++)
            {
                double aVal = abar[i];
                double jVal = jbar[i];
                double xVal = xbar[i];
                double pVal = pbar[i];
                double rVal = rbar[i];
                double cVal = cArr[i];
                double ksVal = ksArr[i];

                double term1 = Math.Pow(Math.Pow(aVal, 3.0 / ksVal) * Math.Min(jVal, 8 + 0.85 * jVal), 1.5);
                double term2 = Math.Pow(Math.Pow(aVal, 2.0 / 3.0) * (0.8 * pVal + rVal * 35.0 / (cVal + 8)), 1.5);
                double sVal = Math.Pow(w0 * term1 + (1 - w0) * term2, 2.0 / 3.0);
                sAll[i] = sVal;
                double tVal = Math.Pow(aVal, 3.0 / ksVal) * xVal / (xVal + sVal + 1);
                tAll[i] = tVal;
                dAll[i] = w1 * Math.Pow(sVal, 0.5) * Math.Pow(tVal, p1) + sVal * w2;
            }

            // --- Weighted–Percentile Calculation ---
            double[] gaps = new double[n];

            if (n == 1)
                gaps[0] = 0;
            else
            {
                gaps[0] = (allCorners[1] - allCorners[0]) / 2.0;
                gaps[n - 1] = (allCorners[n - 1] - allCorners[n - 2]) / 2.0;
                for (int i = 1; i < n - 1; i++)
                    gaps[i] = (allCorners[i + 1] - allCorners[i - 1]) / 2.0;
            }

            double[] effectiveWeights = new double[n];

            for (int i = 0; i < n; i++)
            {
                if (containsCl)
                {
                    effectiveWeights[i] = cArr[i] * gaps[i];
                }
                else
                {
                    effectiveWeights[i] = cArrV2[i] * gaps[i];
                }
            }

            List<CornerData> cornerDataList = new List<CornerData>();

            for (int i = 0; i < n; i++)
            {
                cornerDataList.Add(new CornerData
                {
                    Time = allCorners[i],
                    Jbar = jbar[i],
                    Xbar = xbar[i],
                    Pbar = pbar[i],
                    Abar = abar[i],
                    Rbar = rbar[i],
                    C = cArr[i],
                    Ks = ksArr[i],
                    D = dAll[i],
                    Weight = effectiveWeights[i]
                });
            }

            var sortedList = cornerDataList.OrderBy(cd => cd.D).ToList();
            double[] dSorted = sortedList.Select(cd => cd.D).ToArray();
            double[] cumWeights = new double[sortedList.Count];
            double sumW = 0.0;

            for (int i = 0; i < sortedList.Count; i++)
            {
                sumW += sortedList[i].Weight;
                cumWeights[i] = sumW;
            }

            double totalWeight = sumW;
            double[] normCumWeights = cumWeights.Select(cw => cw / totalWeight).ToArray();

            double[] targetPercentiles = { 0.945, 0.935, 0.925, 0.915, 0.845, 0.835, 0.825, 0.815 };
            List<int> indices = new List<int>();

            foreach (double tp in targetPercentiles)
            {
                int idx = Array.FindIndex(normCumWeights, cw => cw >= tp);
                if (idx < 0)
                    idx = sortedList.Count - 1;
                indices.Add(idx);
            }

            double percentile93, percentile83;

            if (indices.Count >= 8)
            {
                double sum93 = 0.0;
                for (int i = 0; i < 4; i++)
                    sum93 += sortedList[indices[i]].D;
                percentile93 = sum93 / 4.0;
                double sum83 = 0.0;
                for (int i = 4; i < 8; i++)
                    sum83 += sortedList[indices[i]].D;
                percentile83 = sum83 / 4.0;
            }
            else
            {
                percentile93 = sortedList.Average(cd => cd.D);
                percentile83 = percentile93;
            }

            double numWeighted = 0.0;
            double denWeighted = 0.0;

            for (int i = 0; i < sortedList.Count; i++)
            {
                numWeighted += Math.Pow(sortedList[i].D, lambda_n) * sortedList[i].Weight;
                denWeighted += sortedList[i].Weight;
            }

            double weightedMean = Math.Pow(numWeighted / denWeighted, 1.0 / lambda_n);

            double sr = 0.88 * percentile93 * 0.25 + 0.94 * percentile83 * 0.2 + weightedMean * 0.55;
            sr = Math.Pow(sr, p0) / Math.Pow(8, p0) * 8;

            // length weighting
            double totalNotes = noteSeq.Count + 0.5 * lnSeq.Sum(ln => Math.Min(ln.Tail - ln.Head, 1000) / 200.0);
            sr *= totalNotes / (totalNotes + 60);

            sr = rescaleHigh(sr);
            sr *= 0.975;

            double varianceSumTop = 0;
            double varianceSumBottom = denWeighted;

            for (int i = 0; i < dSorted.Length; i++)
            {
                varianceSumTop += Math.Pow(Math.Pow(dSorted[i], 8) - Math.Pow(weightedMean, 8), 2) * sortedList[i].Weight;
            }

            double weightedVariance = Math.Pow(varianceSumTop / varianceSumBottom, 1.0 / 8.0);

            double spikiness = Math.Sqrt(weightedVariance) / weightedMean;
            double switches = Switches(noteSeq, tailSeq, allCorners, ksArr, dAll);
            SrParams pr = new SrParams
            {
                Sr = sr,
                Spikiness = spikiness,
                Switches = switches,
            };

            return pr;
        }

        #region Helper Methods

        private static double rescaleHigh(double sr)
        {
            if (sr <= 9)
                return sr;

            return 9 + (sr - 9) * (1.0 / 1.2);
        }

        // Returns the cumulative sum array for f evaluated on x.
        private static double[] cumulativeSum(double[] x, double[] f)
        {
            int n = x.Length;
            double[] bigF = new double[n];
            bigF[0] = 0.0;

            for (int i = 1; i < n; i++)
            {
                bigF[i] = bigF[i - 1] + f[i - 1] * (x[i] - x[i - 1]);
            }

            return bigF;
        }

        // Query cumulative sum at q.
        private static double queryCumsum(double q, double[] x, double[] bigF, double[] f)
        {
            if (q <= x[0])
                return 0.0;
            if (q >= x[^1])
                return bigF[x.Length - 1];

            int idx = Array.BinarySearch(x, q);
            if (idx < 0)
                idx = ~idx;
            int i = idx - 1;
            return bigF[i] + f[i] * (q - x[i]);
        }

        // Smooth values f (defined on x) over a symmetric window.
        // If mode is "avg", returns the average; otherwise multiplies the integral by scale.
        private static double[] SmoothOnCorners(double[] x, double[] f, double window, double scale, string mode)
        {
            int n = f.Length;
            double[] bigF = cumulativeSum(x, f);
            double[] g = new double[n];

            for (int i = 0; i < n; i++)
            {
                double s = x[i];
                double a = Math.Max(s - window, x[0]);
                double b = Math.Min(s + window, x[^1]);
                double val = queryCumsum(b, x, bigF, f) - queryCumsum(a, x, bigF, f);
                if (mode == "avg")
                    g[i] = b - a > 0 ? val / (b - a) : 0.0;
                else
                    g[i] = scale * val;
            }

            return g;
        }

        // Linear interpolation from old_x, old_vals to new_x.
        private static double[] interpValues(double[] newX, double[] oldX, double[] oldVals)
        {
            int n = newX.Length;
            double[] newVals = new double[n];

            for (int i = 0; i < n; i++)
            {
                double xVal = newX[i];

                if (xVal <= oldX[0])
                    newVals[i] = oldVals[0];
                else if (xVal >= oldX[^1])
                    newVals[i] = oldVals[oldX.Length - 1];
                else
                {
                    int idx = Array.BinarySearch(oldX, xVal);
                    if (idx < 0)
                        idx = ~idx;
                    int j = idx - 1;
                    double t = (xVal - oldX[j]) / (oldX[j + 1] - oldX[j]);
                    newVals[i] = oldVals[j] + t * (oldVals[j + 1] - oldVals[j]);
                }
            }

            return newVals;
        }

        // Step–function interpolation (zero–order hold).
        private static double[] stepInterp(double[] newX, double[] oldX, double[] oldVals)
        {
            int n = newX.Length;
            double[] newVals = new double[n];

            for (int i = 0; i < n; i++)
            {
                double xVal = newX[i];
                int idx = Array.BinarySearch(oldX, xVal);
                if (idx < 0)
                    idx = ~idx;
                idx = idx - 1;
                if (idx < 0)
                    idx = 0;
                if (idx >= oldVals.Length)
                    idx = oldVals.Length - 1;
                newVals[i] = oldVals[idx];
            }

            return newVals;
        }

        // Merges two sorted lists of Note (sorted by Head) into one sorted list.
        private static List<Note> mergeSorted(List<Note> list1, List<Note> list2)
        {
            List<Note> merged = new List<Note>();
            int i = 0, j = 0;

            while (i < list1.Count && j < list2.Count)
            {
                if (list1[i].Head <= list2[j].Head)
                {
                    merged.Add(list1[i]);
                    i++;
                }
                else
                {
                    merged.Add(list2[j]);
                    j++;
                }
            }

            while (i < list1.Count)
            {
                merged.Add(list1[i]);
                i++;
            }

            while (j < list2.Count)
            {
                merged.Add(list2[j]);
                j++;
            }

            return merged;
        }

        // Implements lower_bound: first index at which list[index] >= value.
        private static int lowerBound(List<int> list, int value)
        {
            int low = 0;
            int high = list.Count;

            while (low < high)
            {
                int mid = (low + high) / 2;
                if (list[mid] < value)
                    low = mid + 1;
                else
                    high = mid;
            }

            return low;
        }

        /// <param name="values">A collection of integer values (treated as categories).</param>
        /// <param name="logIterations">A constant added inside the logarithm (default is 1.0).</param>
        /// <returns>The Rao Quadratic Entropy Q.</returns>
        public static double RaoQuadraticEntropyLog(IEnumerable<int> values, int logIterations = 1)
        {
            // Convert the input values to a list.
            List<int> valList = new List<int>(values);

            // Determine the unique categories and their counts.
            // Using a dictionary to map each unique value to its count.
            Dictionary<int, int> counts = new Dictionary<int, int>();

            foreach (int v in valList)
            {
                if (!counts.TryAdd(v, 1))
                    counts[v]++;
            }

            // Create arrays for the unique values and their corresponding relative frequencies.
            int nUnique = counts.Count;
            int[] unique = new int[nUnique];
            double[] p = new double[nUnique];
            int index = 0;
            int totalCount = valList.Count;

            foreach (KeyValuePair<int, int> kvp in counts)
            {
                unique[index] = kvp.Key;
                p[index] = (double)kvp.Value / totalCount;
                index++;
            }

            static double distanceFunc(int x, int y, int logIter)
            {
                double acc = Math.Abs(x - y);

                for (int i = 0; i < logIter; i++)
                {
                    acc = Math.Log(1 + acc);
                }

                return acc;
            }

            // Compute the distance (dissimilarity) matrix for the unique values.
            double[,] distMatrix = new double[nUnique, nUnique];

            for (int i = 0; i < nUnique; i++)
            {
                for (int j = 0; j < nUnique; j++)
                {
                    distMatrix[i, j] = distanceFunc(unique[i], unique[j], logIterations);
                }
            }

            // Compute Rao's Quadratic Entropy:
            // Q = sum_{i,j} p[i] * p[j] * distMatrix[i, j]
            double q = 0.0;

            for (int i = 0; i < nUnique; i++)
            {
                for (int j = 0; j < nUnique; j++)
                {
                    q += p[i] * p[j] * distMatrix[i, j];
                }
            }

            return q;
        }

        /// <param name="noteSeq">A list of notes (each note is an array of integers).</param>
        /// <param name="noteSeqByColumn"></param>
        /// <returns>The variety measure computed from the head gaps and tail gaps.</returns>
        public static double Variety(List<Note> noteSeq, List<List<Note>> noteSeqByColumn)
        {
            // Extract heads and tails.
            List<Note> tailSeq = noteSeq.OrderBy(n => n.Tail).ToList();

            // Compute the gaps between consecutive head values.
            List<int> headGaps = new List<int>();

            for (int i = 0; i < noteSeq.Count - 1; i++)
            {
                headGaps.Add(noteSeq[i + 1].Head - noteSeq[i].Head);
            }

            // Compute the gaps between consecutive tail values.
            List<int> tailGaps = new List<int>();

            for (int i = 0; i < tailSeq.Count - 1; i++)
            {
                tailGaps.Add(tailSeq[i + 1].Tail - tailSeq[i].Tail);
            }

            // Compute the variety measures using Rao's Quadratic Entropy function.
            double headVariety = RaoQuadraticEntropyLog(headGaps);
            double tailVariety = RaoQuadraticEntropyLog(tailGaps);

            List<int> headGapsNew = new List<int>();

            for (int k = 0; k < noteSeqByColumn.Count; k++)
            {
                List<Note> heads = noteSeqByColumn[k];

                List<int> headGapsColumn = new List<int>();

                for (int i = 0; i < heads.Count - 1; i++)
                {
                    headGapsColumn.Add(heads[i + 1].Head - heads[i].Head);
                }

                headGapsNew.AddRange(headGapsColumn);
            }

            double colVariety = 2.5 * RaoQuadraticEntropyLog(headGapsNew, 2);

            return 0.5 * headVariety + 0.11 * tailVariety + 0.45 * colVariety;
        }

        /// <summary>
        /// Computes the switch measure.
        /// noteSeq: List of notes used for heads.
        /// tailSeq: List of notes used for tails.
        /// allCorners: Sorted list of int values.
        /// KsArr, weights: Arrays of doubles.
        /// </summary>
        public static int LowerBound(double[] sortedArray, double value)
        {
            int index = Array.BinarySearch(sortedArray, value);
            // If not found, BinarySearch returns a negative number which is the bitwise complement of the index of the next element that is larger than value.
            return index < 0 ? ~index : index;
        }

        /// <summary>
        /// Computes the switch measure.
        /// noteSeq: List of notes used for heads.
        /// tailSeq: List of notes used for tails.
        /// allCorners: Sorted array of double values.
        /// KsArr, weights: Arrays of double values.
        /// </summary>
        public static double Switches(List<Note> noteSeq, List<Note> tailSeq, double[] allCorners, double[] ksArr, double[] weights)
        {
            // Extract head values.
            List<int> heads = noteSeq.Select(n => n.Head).ToList();

            // For each head, use LowerBound (which now uses Array.BinarySearch) to get the insertion index.
            List<int> idxList = heads.Select(h => LowerBound(allCorners, h)).ToList();

            // Use these indices (dropping the last element) to select values from KsArr and weights.
            List<double> ksArrAtNote = idxList.Take(idxList.Count - 1).Select(i => ksArr[i]).ToList();
            List<double> weightsAtNote = idxList.Take(idxList.Count - 1).Select(i => weights[i]).ToList();

            // Compute gaps between consecutive head values.
            List<double> headGaps = new List<double>();

            for (int i = 0; i < heads.Count - 1; i++)
            {
                headGaps.Add(heads[i + 1] - heads[i]);
            }

            int numHeadGaps = headGaps.Count;
            // Compute moving averages over a window defined by index offsets (from i-50 to i+50).
            List<double> avgs = new List<double>();

            for (int i = 0; i < numHeadGaps; i++)
            {
                int start = Math.Max(0, i - 50);
                int end = Math.Min(i + 50, numHeadGaps - 1);
                double sum = 0.0;
                int count = 0;

                for (int j = start; j <= end; j++)
                {
                    sum += headGaps[j];
                    count++;
                }

                avgs.Add(sum / count);
            }

            // Compute signature for heads.
            double signatureHead = 0.0;

            for (int i = 0; i < numHeadGaps; i++)
            {
                signatureHead += Math.Sqrt(headGaps[i] / avgs[i] / numHeadGaps * weightsAtNote[i])
                                 * Math.Pow(ksArrAtNote[i], 0.25);
            }

            double sumRefHead = 0.0;

            for (int i = 0; i < numHeadGaps; i++)
            {
                sumRefHead += headGaps[i] / avgs[i] * weightsAtNote[i];
            }

            double refSignatureHead = Math.Sqrt(sumRefHead);

            // Process tails similarly.
            List<int> tails = tailSeq.Select(n => n.Tail).ToList();
            List<int> idxListTails = tails.Select(t => LowerBound(allCorners, t)).ToList();
            List<double> ksArrAtTail = idxListTails.Take(idxListTails.Count - 1).Select(i => ksArr[i]).ToList();
            List<double> weightsAtTail = idxListTails.Take(idxListTails.Count - 1).Select(i => weights[i]).ToList();

            List<double> tailGaps = new List<double>();

            for (int i = 0; i < tails.Count - 1; i++)
            {
                tailGaps.Add(tails[i + 1] - tails[i]);
            }

            double signatureTail = 0.0;
            double refSignatureTail = 0.0;

            if (tails.Count > 0 && tails[^1] > tails[0] && tailGaps.Count > 0)
            {
                int numTailGaps = tailGaps.Count;
                List<double> avgsTail = new List<double>();

                for (int i = 0; i < numTailGaps; i++)
                {
                    int start = Math.Max(0, i - 50);
                    int end = Math.Min(i + 50, numTailGaps - 1);
                    double sum = 0.0;
                    int count = 0;

                    for (int j = start; j <= end; j++)
                    {
                        sum += tailGaps[j];
                        count++;
                    }

                    avgsTail.Add(sum / count);
                }

                for (int i = 0; i < numTailGaps; i++)
                {
                    signatureTail += Math.Sqrt(tailGaps[i] / avgsTail[i] / numTailGaps * weightsAtTail[i])
                                     * Math.Pow(ksArrAtTail[i], 0.25);
                }

                double sumRefTail = 0.0;

                for (int i = 0; i < numTailGaps; i++)
                {
                    sumRefTail += tailGaps[i] / avgsTail[i] * weightsAtTail[i];
                }

                refSignatureTail = Math.Sqrt(sumRefTail);
            }

            // Combine head and tail signatures.
            double numerator = signatureHead * numHeadGaps + signatureTail * tailGaps.Count;
            double denominator = refSignatureHead * numHeadGaps + refSignatureTail * tailGaps.Count;
            double switches = numerator / denominator;

            return switches / 2.0 + 0.5;
        }

        #endregion
    }
}
