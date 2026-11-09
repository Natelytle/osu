// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing
{
    public static class ManiaMapData
    {
        private const double rate_smoothing = 0.32481;
        private const double max_column_rate = 30.0;

        public static void ProcessAndAssign(IReadOnlyList<ManiaDifficultyHitObject> objects, int totalColumns, double clockRate)
        {
            double chordTolerance = ChordUtils.ToleranceAt(clockRate);
            List<ManiaRow> rows = groupIntoRows(objects, totalColumns, chordTolerance);

            trackLoad(rows, totalColumns, chordTolerance);

            ManiaRepetitionPreprocessor.ProcessAndAssign(rows);

            HeldNoteMultiplier.ProcessAndAssign(rows);
        }

        /// <summary>
        /// Groups consecutive hit objects that start within chord tolerance of each other into <see cref="ManiaRow"/>s.
        /// </summary>
        private static List<ManiaRow> groupIntoRows(IReadOnlyList<ManiaDifficultyHitObject> objects, int totalColumns, double chordTolerance)
        {
            var rows = new List<ManiaRow>();

            int i = 0;

            bool[] taken = new bool[totalColumns];

            while (i < objects.Count)
            {
                double rowStart = objects[i].StartTime;
                var members = new List<ManiaDifficultyHitObject>();

                Array.Clear(taken);

                while (i < objects.Count && Math.Abs(objects[i].StartTime - rowStart) <= chordTolerance && !taken[objects[i].Column])
                {
                    taken[objects[i].Column] = true;
                    members.Add(objects[i]);
                    i++;
                }

                int[] columns = members.Select(m => m.Column).Order().ToArray();

                ManiaRow row = new ManiaRow(columns, rowStart, members, rows.Count, totalColumns, rows);

                foreach (var rowMember in members)
                    rowMember.Row = row;

                rows.Add(row);
            }

            return rows;
        }

        private static void trackLoad(List<ManiaRow> rows, int totalColumns, double chordTolerance)
        {
            double[] columnGap = new double[totalColumns];
            double[] lastStart = new double[totalColumns];
            double[] lastEnd = new double[totalColumns];

            Array.Fill(lastStart, double.NegativeInfinity);
            Array.Fill(lastEnd, double.NegativeInfinity);

            double[] handGap = new double[2];
            double[] handLastStart = { double.NegativeInfinity, double.NegativeInfinity };

            double rowGap = 0.0;
            double lastRowStart = double.NegativeInfinity;

            double fingers = HandLayout.FingerCount(totalColumns);

            foreach (var row in rows)
            {
                double time = row.StartTime;

                // Held state is read before this row's presses overwrite lastStart / lastEnd.
                double heldLeft = heldFingersOf(lastStart, lastEnd, time, chordTolerance, ManiaHand.Left, totalColumns);
                double heldRight = heldFingersOf(lastStart, lastEnd, time, chordTolerance, ManiaHand.Right, totalColumns);

                foreach (var note in row.Objects)
                {
                    int column = note.Column;

                    columnGap[column] = legacyGap(columnGap[column], time - lastStart[column]);
                    lastStart[column] = time;
                    lastEnd[column] = Math.Max(lastEnd[column], note.EndTime);
                }

                for (int hand = 0; hand < 2; hand++)
                {
                    if (row.ChordSizeOn((ManiaHand)hand) <= 0)
                        continue;

                    handGap[hand] = legacyGap(handGap[hand], time - handLastStart[hand]);
                    handLastStart[hand] = time;
                }

                rowGap = legacyGap(rowGap, time - lastRowStart);
                lastRowStart = time;

                double[] rates = new double[totalColumns];

                for (int column = 0; column < totalColumns; column++)
                    rates[column] = rateAt(columnGap[column], time - lastStart[column]);

                row.Load = new RowLoad(rates,
                    handLoadOf(rates, ManiaHand.Left, rateAt(handGap[0], time - handLastStart[0]), heldLeft, fingers, totalColumns),
                    handLoadOf(rates, ManiaHand.Right, rateAt(handGap[1], time - handLastStart[1]), heldRight, fingers, totalColumns),
                    rateAt(rowGap, 0.0));
            }
        }

        private static double legacyGap(double meanGap, double gap)
        {
            if (double.IsInfinity(gap))
                return meanGap;

            double rate = meanGap > 0 ? 1000.0 / meanGap : 0.0;
            double instantRate = Math.Min(1000.0 / Math.Max(gap, 1.0), max_column_rate);
            double advanced = rate > 0 ? rate + rate_smoothing * (instantRate - rate) : instantRate;

            return advanced > 0 ? 1000.0 / advanced : 0.0;
        }

        private static double rateAt(double meanGap, double sinceLast)
        {
            if (double.IsInfinity(sinceLast))
                return 0.0;

            return meanGap > 0 ? 1000.0 / Math.Max(meanGap, sinceLast) : 0.0;
        }

        private static double heldFingersOf(double[] lastStart, double[] lastEnd, double time, double chordTolerance, ManiaHand hand, int totalColumns)
        {
            double held = 0.0;

            for (int column = 0; column < totalColumns; column++)
            {
                double share = HandLayout.ShareOf(column, totalColumns, hand);

                if (share <= 0)
                    continue;

                bool mid = lastEnd[column] > time + chordTolerance && lastStart[column] < time - chordTolerance;

                if (mid)
                    held += share * HoldUtils.HoldGateOf(lastEnd[column] - lastStart[column]);
            }

            return held;
        }

        private static HandLoad handLoadOf(double[] rates, ManiaHand hand, double rowRate, double heldFingers, double fingerCapacity, int totalColumns)
        {
            double throughput = 0.0;
            double secondMoment = 0.0;

            for (int column = 0; column < totalColumns; column++)
            {
                double share = HandLayout.ShareOf(column, totalColumns, hand);

                if (share <= 0)
                    continue;

                throughput += share * rates[column];
                secondMoment += share * rates[column] * rates[column];
            }

            return new HandLoad(throughput, secondMoment, rowRate, heldFingers, fingerCapacity);
        }
    }
}
