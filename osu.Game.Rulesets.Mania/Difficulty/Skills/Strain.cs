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
using osu.Game.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Strain : Skill
    {
        private const double star_rating_accuracy = 0.95;

        // The player has a 2% chance of achieving the score's accuracy.
        private const double accuracy_prob = 0.02;

        private double strainMultiplier => 0.05;
        private double strainDecayBase => 0.15;
        private double tailDeviationMultiplier => 1.8;

        // To calculate accuracy at a skill level correctly, we need this information.
        private readonly bool lazerMechanics;
        private DiffHitWindows hitWindows;

        // We need to use dictionaries so that we can attach tails to the correct heads, or else we cannot process stable accuracy properly.
        private readonly List<double> noteDifficulties = new List<double>();
        private readonly List<(double Head, double Tail)> longNoteDifficulties = new List<(double, double)>();

        private BinNote[]? binNotes;
        private BinLongNote[]? binLongNotes;

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

        public override double DifficultyValue() => skillLevelAtAccuracy(star_rating_accuracy);

        public double[] AccuracyCurve()
        {
            double[] skillLevels = new double[20];
            double[] accuracies = { 1.00, 0.998, 0.995, 0.99, 0.98, 0.97, 0.96, 0.95, 0.94, 0.93, 0.92, 0.91, 0.90, 0.88, 0.86, 0.84, 0.82, 0.80, 0.75, 0.70 };

            // If there are no notes, we just return the empty polynomial.
            if (noteDifficulties.Count + longNoteDifficulties.Count == 0)
                return skillLevels;

            for (int i = 0; i < accuracies.Length; i++)
            {
                skillLevels[i] = skillLevelAtAccuracy(accuracies[i]);
            }

            return skillLevels;
        }

        private double skillLevelAtAccuracy(double accuracy)
        {
            if (noteDifficulties.Count + longNoteDifficulties.Count == 0)
                return 0;

            double maxDifficulty = noteDifficulties.Count != 0 ? noteDifficulties.Max() : longNoteDifficulties.ConvertAll(obj => obj.Head + obj.Tail).Max();

            if (maxDifficulty == 0)
                return 0;

            // binNotes ??= BinNote.CreateBins(noteDifficulties, 128);
            // binLongNotes ??= BinLongNote.CreateBins(longNoteDifficulties, 64);

            return RootFinding.FindRootExpand(skill => accuracyProb(accuracy, skill) - accuracy_prob, 0, maxDifficulty * 2);
        }

        private double accuracyProb(double accuracy, double skill)
        {
            // Just a little root finding trick since accuracy can have be above 0% even at 0 skill. Doing this lets the root finding algorithm find a root anyway.
            if (skill == 0)
                return 0;

            // return noteDifficulties.Count > 128 || longNoteDifficulties.Count > 128 ? totalAccuracyBinned(skill) : totalAccuracyExact(skill);

            return accuracyProbExact(accuracy, skill);
        }

        /// <summary>
        /// The probability of achieving x accuracy given y skill. We approximate this using the central limit theorem because it would be expensive to compute manually.
        /// </summary>
        /// <param name="accuracy"></param>
        /// <param name="skill"></param>
        /// <returns></returns>
        private double accuracyProbExact(double accuracy, double skill)
        {
            double count = noteDifficulties.Count;

            double sum = 0;
            double varSum = 0;

            for (int i = 0; i < noteDifficulties.Count; i++)
            {
                var noteProbs = getNoteProbs(noteDifficulties[i], skill);

                sum += noteProbs.Score;
                varSum += noteProbs.Variance;
            }

            if (lazerMechanics)
            {
                count += longNoteDifficulties.Count * 2;

                for (int i = 0; i < longNoteDifficulties.Count; i++)
                {
                    var noteProbs = getNoteProbs(longNoteDifficulties[i].Head, skill);

                    sum += noteProbs.Score;
                    varSum += noteProbs.Variance;

                    var tailProbs = getTailProbs(longNoteDifficulties[i].Tail, skill);

                    sum += tailProbs.Score;
                    varSum += tailProbs.Variance;
                }
            }
            else
            {
                count += longNoteDifficulties.Count;

                for (int i = 0; i < longNoteDifficulties.Count; i++)
                {
                    var longNoteProbs = getLongNoteProbs(longNoteDifficulties[i], skill);

                    sum += longNoteProbs.Score;
                    varSum += longNoteProbs.Variance;
                }
            }

            double mean = sum / count / 320;
            double dev = Math.Sqrt(varSum) / count / 320;

            double p = 1 - SpecialFunctions.NormalCdf(mean, dev, accuracy);

            return p;
        }

        /*
        private double expectedAccuracyBinned(double skill)
        {
            double count = noteDifficulties.Count;

            double sum = 0;
            double variance = 0;

            for (int i = 0; i < binNotes!.Length; i++)
            {
                if (binNotes[i].Count == 0)
                    continue;

                sum += binNotes[i].Count * getNoteProbs(binNotes[i].Difficulty, skill);
            }

            if (lazerMechanics)
            {
                count += longNoteDifficulties.Count * 2;

                for (int i = 0; i < binLongNotes!.Length; i++)
                {
                    if (binLongNotes[i].Count == 0)
                        continue;

                    sum += binLongNotes[i].Count * getNoteProbs(binLongNotes[i].HeadDifficulty, skill);
                    sum += binLongNotes[i].Count * getTailProbs(binLongNotes[i].TailDifficulty, skill);
                }
            }
            else
            {
                count += longNoteDifficulties.Count;

                for (int i = 0; i < binLongNotes!.Length; i++)
                {
                    if (binLongNotes[i].Count == 0) continue;

                    sum += binLongNotes[i].Count * getLongNoteProbs((binLongNotes[i].HeadDifficulty, binLongNotes[i].TailDifficulty), skill);
                }
            }

            return sum / count;
        }
        */

        private JudgementProbs getNoteProbs(double difficulty, double skill)
        {
            double unstableRate = skillToUr(skill, difficulty);

            double pMax = hitWindows.HitProbability(hitWindows.HMax, unstableRate);
            double p300 = hitWindows.HitProbability(hitWindows.H300, unstableRate) - hitWindows.HitProbability(hitWindows.HMax, unstableRate);
            double p200 = hitWindows.HitProbability(hitWindows.H200, unstableRate) - hitWindows.HitProbability(hitWindows.H300, unstableRate);
            double p100 = hitWindows.HitProbability(hitWindows.H100, unstableRate) - hitWindows.HitProbability(hitWindows.H200, unstableRate);
            double p50 = hitWindows.HitProbability(hitWindows.H50, unstableRate) - hitWindows.HitProbability(hitWindows.H100, unstableRate);

            return new JudgementProbs(pMax, p300, p200, p100, p50); // (320 * pMax + 300 * p300 + 200 * p200 + 100 * p100 + 50 * p50) / 320;
        }

        private JudgementProbs getTailProbs(double difficulty, double skill)
        {
            double unstableRate = skillToUrTail(skill, difficulty);

            double pMax = hitWindows.HitProbability(hitWindows.HMax * 1.5, unstableRate);
            double p300 = hitWindows.HitProbability(hitWindows.H300 * 1.5, unstableRate) - hitWindows.HitProbability(hitWindows.HMax * 1.5, unstableRate);
            double p200 = hitWindows.HitProbability(hitWindows.H200 * 1.5, unstableRate) - hitWindows.HitProbability(hitWindows.H300 * 1.5, unstableRate);
            double p100 = hitWindows.HitProbability(hitWindows.H100 * 1.5, unstableRate) - hitWindows.HitProbability(hitWindows.H200 * 1.5, unstableRate);
            double p50 = hitWindows.HitProbability(hitWindows.H50 * 1.5, unstableRate) - hitWindows.HitProbability(hitWindows.H100 * 1.5, unstableRate);

            return new JudgementProbs(pMax, p300, p200, p100, p50);
        }

        private JudgementProbs getLongNoteProbs((double head, double tail) difficulties, double skill)
        {
            double headUnstableRate = skillToUr(skill, difficulties.head);
            double tailUnstableRate = skillToUrTail(skill, difficulties.tail);

            double pMax = hitWindows.HitProbabilityLn(hitWindows.HMax, headUnstableRate, tailUnstableRate);
            double p300 = hitWindows.HitProbabilityLn(hitWindows.H300, headUnstableRate, tailUnstableRate) - hitWindows.HitProbabilityLn(hitWindows.HMax, headUnstableRate, tailUnstableRate);
            double p200 = hitWindows.HitProbabilityLn(hitWindows.H200, headUnstableRate, tailUnstableRate) - hitWindows.HitProbabilityLn(hitWindows.H300, headUnstableRate, tailUnstableRate);
            double p100 = hitWindows.HitProbabilityLn(hitWindows.H100, headUnstableRate, tailUnstableRate) - hitWindows.HitProbabilityLn(hitWindows.H200, headUnstableRate, tailUnstableRate);
            double p50 = hitWindows.HitProbabilityLn(hitWindows.H50, headUnstableRate, tailUnstableRate) - hitWindows.HitProbabilityLn(hitWindows.H100, headUnstableRate, tailUnstableRate);

            return new JudgementProbs(pMax, p300, p200, p100, p50);
        }

        // This formula ensure that when your skill equals the note difficulty, you get around 99%, and when your skill level is 0 (mashing), you get around 80%.
        private double skillToUr(double skill, double d) => d != 0 ? 50 * Math.Pow(12 / 50.0, Math.Pow(skill / d, BalancingConstants.ACC)) : 0;
        private double skillToUrTail(double skill, double d) => d != 0 ? 50 * Math.Pow(12 * tailDeviationMultiplier / 50.0, Math.Pow(skill / d, BalancingConstants.ACC)) : 0;

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
