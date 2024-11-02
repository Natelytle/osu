// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Strain : Skill
    {
        private double strainMultiplier => 0.1;
        private double strainDecayBase => 0.15;
        private double tailDeviationMultiplier => 1.8;

        // To calculate accuracy at a skill level correctly, we need this information.
        private readonly bool lazerMechanics;
        private DiffHitWindows hitWindows;

        // We need to use dictionaries so that we can attach tails to the correct heads, or else we cannot process stable accuracy properly.
        private readonly List<double> noteDifficulties = new List<double>();
        private readonly List<(double Head, double Tail)> longNoteDifficulties = new List<(double, double)>();

        private BinNote[] binNotes = Array.Empty<BinNote>();
        private BinLongNote[] binLongNotes = Array.Empty<BinLongNote>();

        private double currChordStrain;
        private double prevChordStrain;

        public Strain(Mod[] mods, double od)
            : base(mods)
        {
            lazerMechanics = !mods.Any(m => m is ManiaModClassic);
            hitWindows = new DiffHitWindows(mods, od);
        }

        /* /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                                                      AGGREGATION BEGINS HERE
        */ /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        // These formulas ensure that when your skill equals the note difficulty, you get 100 (* tail deviation mult) UR, and when your skill level is 0 (mashing), you get 500 UR.
        private double skillToUr(double skill, double d) => d != 0 ? 50 * Math.Pow(10 / 50.0, Math.Pow(skill / d, BalancingConstants.ACC)) : 0;
        private double skillToUrTail(double skill, double d) => d != 0 ? 50 * Math.Pow(10 * tailDeviationMultiplier / 50.0, Math.Pow(skill / d, BalancingConstants.ACC)) : 0;

        private double getNoteAccuracy(double difficulty, double skill)
        {
            double unstableRate = skillToUr(skill, difficulty);

            double pMax = hitWindows.HitProbability(hitWindows.HMax, unstableRate);
            double p300 = hitWindows.HitProbability(hitWindows.H300, unstableRate) - hitWindows.HitProbability(hitWindows.HMax, unstableRate);
            double p200 = hitWindows.HitProbability(hitWindows.H200, unstableRate) - hitWindows.HitProbability(hitWindows.H300, unstableRate);
            double p100 = hitWindows.HitProbability(hitWindows.H100, unstableRate) - hitWindows.HitProbability(hitWindows.H200, unstableRate);
            double p50 = hitWindows.HitProbability(hitWindows.H50, unstableRate) - hitWindows.HitProbability(hitWindows.H100, unstableRate);

            return (320 * pMax + 300 * p300 + 200 * p200 + 100 * p100 + 50 * p50) / 320;
        }

        private double getTailAccuracy(double difficulty, double skill)
        {
            double unstableRate = skillToUrTail(skill, difficulty);

            double pMax = hitWindows.HitProbability(hitWindows.HMax * 1.5, unstableRate);
            double p300 = hitWindows.HitProbability(hitWindows.H300 * 1.5, unstableRate) - hitWindows.HitProbability(hitWindows.HMax * 1.5, unstableRate);
            double p200 = hitWindows.HitProbability(hitWindows.H200 * 1.5, unstableRate) - hitWindows.HitProbability(hitWindows.H300 * 1.5, unstableRate);
            double p100 = hitWindows.HitProbability(hitWindows.H100 * 1.5, unstableRate) - hitWindows.HitProbability(hitWindows.H200 * 1.5, unstableRate);
            double p50 = hitWindows.HitProbability(hitWindows.H50 * 1.5, unstableRate) - hitWindows.HitProbability(hitWindows.H100 * 1.5, unstableRate);

            return (320 * pMax + 300 * p300 + 200 * p200 + 100 * p100 + 50 * p50) / 320;
        }

        private double getLongNoteAccuracy((double head, double tail) difficulties, double skill)
        {
            double headUnstableRate = skillToUrTail(skill, difficulties.head);
            double tailUnstableRate = skillToUrTail(skill, difficulties.tail);

            double pMax = hitWindows.HitProbabilityLn(hitWindows.HMax, headUnstableRate, tailUnstableRate);
            double p300 = hitWindows.HitProbabilityLn(hitWindows.H300, headUnstableRate, tailUnstableRate) - hitWindows.HitProbabilityLn(hitWindows.HMax, headUnstableRate, tailUnstableRate);
            double p200 = hitWindows.HitProbabilityLn(hitWindows.H200, headUnstableRate, tailUnstableRate) - hitWindows.HitProbabilityLn(hitWindows.H300, headUnstableRate, tailUnstableRate);
            double p100 = hitWindows.HitProbabilityLn(hitWindows.H100, headUnstableRate, tailUnstableRate) - hitWindows.HitProbabilityLn(hitWindows.H200, headUnstableRate, tailUnstableRate);
            double p50 = hitWindows.HitProbabilityLn(hitWindows.H50, headUnstableRate, tailUnstableRate) - hitWindows.HitProbabilityLn(hitWindows.H100, headUnstableRate, tailUnstableRate);

            return (320 * pMax + 300 * p300 + 200 * p200 + 100 * p100 + 50 * p50) / 320;
        }

        private double totalAccuracyExact(double skill)
        {
            double averageAccuracy = 0;
            double count = 0;

            for (int i = 0; i < noteDifficulties.Count; i++)
            {
                averageAccuracy = (count * averageAccuracy + getNoteAccuracy(noteDifficulties[i], skill)) / (count + 1);
                count += 1;
            }

            if (lazerMechanics)
            {
                for (int i = 0; i < longNoteDifficulties.Count; i++)
                {
                    averageAccuracy = (count * averageAccuracy + getNoteAccuracy(longNoteDifficulties[i].Head, skill)) / (count + 1);
                    count += 1;

                    averageAccuracy = (count * averageAccuracy + getTailAccuracy(longNoteDifficulties[i].Tail, skill)) / (count + 1);
                    count += 1;
                }
            }
            else
            {
                for (int i = 0; i < longNoteDifficulties.Count; i++)
                {
                    averageAccuracy = (count * averageAccuracy + getLongNoteAccuracy(longNoteDifficulties[i], skill)) / (count + 1);
                    count += 1;
                }
            }

            return averageAccuracy;
        }

        private double totalAccuracyBinned(double skill)
        {
            double averageAccuracy = 0;
            double count = 0;

            for (int i = 0; i < binNotes.Length; i++)
            {
                if (count + binNotes[i].Count == 0) continue;

                averageAccuracy = (count * averageAccuracy + binNotes[i].Count * getNoteAccuracy(binNotes[i].Difficulty, skill)) / (count + binNotes[i].Count);
                count += binNotes[i].Count;
            }

            if (lazerMechanics)
            {
                for (int i = 0; i < binLongNotes.Length; i++)
                {
                    if (count + binLongNotes[i].Count == 0) continue;

                    averageAccuracy = (count * averageAccuracy + binLongNotes[i].Count * getNoteAccuracy(binLongNotes[i].HeadDifficulty, skill)) / (count + binLongNotes[i].Count);
                    count += binLongNotes[i].Count;

                    averageAccuracy = (count * averageAccuracy + binLongNotes[i].Count * getTailAccuracy(binLongNotes[i].TailDifficulty, skill)) / (count + binLongNotes[i].Count);
                    count += binLongNotes[i].Count;
                }
            }
            else
            {
                for (int i = 0; i < binLongNotes.Length; i++)
                {
                    if (count + binLongNotes[i].Count == 0) continue;

                    averageAccuracy = (count * averageAccuracy + binLongNotes[i].Count * getLongNoteAccuracy((binLongNotes[i].HeadDifficulty, binLongNotes[i].TailDifficulty), skill)) / (count + binLongNotes[i].Count);
                    count += binLongNotes[i].Count;
                }
            }

            return averageAccuracy;
        }

        private double totalAccuracy(double skill)
        {
            // Just a little root finding trick since accuracy can have be above 0% even at 0 skill. Doing this lets the root finding algorithm find a root anyway.
            if (skill == 0)
                return 0;

            return noteDifficulties.Count > 128 || longNoteDifficulties.Count > 128 ? totalAccuracyBinned(skill) : totalAccuracyExact(skill);
        }

        public override double DifficultyValue()
        {
            if (noteDifficulties.Count + longNoteDifficulties.Count == 0)
                return 0;

            double maxDifficulty = noteDifficulties.Count != 0 ? noteDifficulties.Max() : longNoteDifficulties.ConvertAll(obj => obj.Head + obj.Tail).Max();

            if (maxDifficulty == 0)
                return 0;

            binNotes = BinNote.CreateBins(noteDifficulties, 32);
            binLongNotes = BinLongNote.CreateBins(longNoteDifficulties, 64);

            double totalNotes = lazerMechanics ? noteDifficulties.Count + 2 * longNoteDifficulties.Count : noteDifficulties.Count + longNoteDifficulties.Count;

            double targetAccuracy = Math.Min(0.95, (totalNotes - 1) / totalNotes);

            double skillLevel = RootFinding.FindRootExpand(x => totalAccuracy(x) - targetAccuracy, 0, maxDifficulty, accuracy: 1e-4);

            if (noteDifficulties.Count % 100 == 0)
                Console.WriteLine(noteDifficulties.Count);

            return skillLevel;
        }

        // The skill level required to get the accuracy if you got a 320 on every note, with 1 miss added.
        // This value is not calculated for a true SS or else the star rating would be infinite.
        public double SSValue()
        {
            if (noteDifficulties.Count + longNoteDifficulties.Count == 0)
                return 0;

            double maxDifficulty = noteDifficulties.Count != 0 ? noteDifficulties.Max() : longNoteDifficulties.ConvertAll(obj => obj.Head + obj.Tail).Max();

            if (maxDifficulty == 0)
                return 0;

            binNotes = BinNote.CreateBins(noteDifficulties, 32);
            binLongNotes = BinLongNote.CreateBins(longNoteDifficulties, 64);

            double totalNotes = lazerMechanics ? noteDifficulties.Count + 2 * longNoteDifficulties.Count : noteDifficulties.Count + longNoteDifficulties.Count;

            double targetAccuracy = (totalNotes - 1) / totalNotes;

            double skillLevel = RootFinding.FindRootExpand(x => totalAccuracy(x) - targetAccuracy, 0, maxDifficulty * 3, accuracy: 1e-4);

            return skillLevel;
        }

        public ExpPolynomial AccuracyCurve()
        {
            double[] accuracyLosses = new double[21];
            double[] penalties = { 1, 0.95, 0.9, 0.85, 0.8, 0.75, 0.7, 0.65, 0.6, 0.55, 0.5, 0.45, 0.4, 0.35, 0.3, 0.25, 0.2, 0.15, 0.1, 0.05, 0 };

            double fcSkill = SSValue();

            ExpPolynomial curve = new ExpPolynomial();

            // If there are no notes, we just return the empty polynomial.
            if (noteDifficulties.Count + longNoteDifficulties.Count == 0)
                return curve;

            for (int i = 0; i < penalties.Length; i++)
            {
                if (i == 0)
                {
                    accuracyLosses[i] = 0;
                    continue;
                }

                double penalizedSkill = fcSkill * penalties[i];

                accuracyLosses[i] = 1 - totalAccuracy(penalizedSkill);
            }

            curve.Fit(accuracyLosses);

            return curve;
        }

        /* /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                                                      PROCESSING BEGINS HERE
        */ /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public override void Process(DifficultyHitObject current)
        {
            var prevInColumn = (ManiaDifficultyHitObject?)((ManiaDifficultyHitObject)current).PrevInColumn(0);

            switch (current.BaseObject)
            {
                case not (HeadNote or TailNote):
                    noteDifficulties.Add(strainValueNote(current));
                    break;

                case HeadNote:
                    longNoteDifficulties.Add((strainValueNote(current), 0));
                    break;

                case TailNote:
                    if (prevInColumn is null) break;

                    int headIndex = prevInColumn.LongNoteIndex;
                    var tuple = longNoteDifficulties[headIndex];
                    tuple.Tail = strainValueTail(current);
                    longNoteDifficulties[headIndex] = tuple;
                    break;
            }
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        private double strainValueNote(DifficultyHitObject current)
        {
            currChordStrain *= strainDecay(current.StartTime - current.Previous(0)?.StartTime ?? 0);

            double speedDifficulty = SpeedEvaluator.EvaluateDifficultyOf(current);
            double sameColumnDifficulty = SameColumnEvaluator.EvaluateDifficultyOf(current);
            double crossColumnDifficulty = CrossColumnEvaluator.EvaluateDifficultyOf(current);
            double chordDifficulty = ChordEvaluator.EvaluateDifficultyOf(current);

            double totalDifficulty = combinedValue(speedDifficulty, sameColumnDifficulty, crossColumnDifficulty, chordDifficulty);

            currChordStrain = norm(BalancingConstants.STRAIN, currChordStrain, totalDifficulty);

            totalDifficulty = norm(BalancingConstants.STRAIN, prevChordStrain * strainMultiplier, totalDifficulty);

            if (current.StartTime != current.Next(0)?.StartTime)
            {
                prevChordStrain = currChordStrain;
            }

            return totalDifficulty;
        }

        private double combinedValue(double speedValue, double sameColumnDifficulty, double crossColumnDifficulty, double chordDifficulty)
        {
            double combinedValue = norm(BalancingConstants.COLUMN, sameColumnDifficulty, crossColumnDifficulty);
            combinedValue = norm(BalancingConstants.SPEED, combinedValue, speedValue);
            combinedValue = norm(BalancingConstants.CHORD, combinedValue, chordDifficulty);

            return combinedValue;
        }

        private double strainValueTail(DifficultyHitObject current)
        {
            currChordStrain *= strainDecay(current.StartTime - current.Previous(0).StartTime);

            double holdingDifficulty = HoldingEvaluator.EvaluateDifficultyOf(current);
            double releaseDifficulty = ReleaseEvaluator.EvaluateDifficultyOf(current);

            double totalDifficulty = norm(BalancingConstants.HOLD, holdingDifficulty, releaseDifficulty);

            currChordStrain = norm(BalancingConstants.STRAIN, currChordStrain, totalDifficulty);

            totalDifficulty = norm(BalancingConstants.STRAIN, prevChordStrain * strainMultiplier, totalDifficulty);

            if (current.StartTime != current.Next(0)?.StartTime)
            {
                prevChordStrain = currChordStrain;
            }

            return totalDifficulty;
        }

        /// <summary>
        /// Returns the <i>p</i>-norm of an <i>n</i>-dimensional vector.
        /// </summary>
        /// <param name="p">The value of <i>p</i> to calculate the norm for.</param>
        /// <param name="values">The coefficients of the vector.</param>
        private double norm(double p, params double[] values) => Math.Pow(values.Sum(x => Math.Pow(x, p)), 1 / p);
    }
}
