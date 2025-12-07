// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils.AccuracySimulation
{
    public class AccuracySimulator
    {
        // The value of the max judgement. Increasing this value increases the value of high ratios.
        public const double MAX_JUDGEMENT_WEIGHT = 305;

        // The player has a 2% chance of achieving the score's accuracy.
        private const double accuracy_prob = 0.02;

        // The UR a player is expected to get on a note with the same difficulty as their skill level.
        private const double skill_ur = 12;

        // The UR a player is expected to get when mashing, the very highest their UR can ever be.
        private const double mash_ur = 100;

        // Constant threshold for binning
        private const int bin_threshold = 128;

        // How much the player's UR should change relative to the note's difficulty, when it is higher or lower.
        private double accuracyExponent => 3.2;

        // How much long note tails should increase the player's UR.
        private double tailDeviationMultiplier => 1.8;

        private DifficultyHitWindows hitWindows;

        // We need to use dictionaries so that we can attach tails to the correct heads, or else we cannot process stable accuracy properly.
        private readonly List<double> noteDifficulties;
        private readonly List<double> tailDifficulties;

        private readonly List<Bin> binNotes = new List<Bin>();
        private readonly List<Bin> binTails = new List<Bin>();

        public AccuracySimulator(Mod[] mods, double od, List<double> noteDifficulties, List<double> tailDifficulties)
        {
            hitWindows = new DifficultyHitWindows(mods, od);

            this.noteDifficulties = noteDifficulties;
            this.tailDifficulties = tailDifficulties;

            if (noteDifficulties.Count >= bin_threshold)
            {
                binNotes = Bin.CreateBins(noteDifficulties, 64);
                binTails = Bin.CreateBins(tailDifficulties, 64);
            }
        }

        public double[] AccuracyCurve(double ssValue)
        {
            double[] skillLevels = { 1.00, 0.95, 0.90, 0.85, 0.80, 0.75, 0.70, 0.65, 0.60, 0.55, 0.50, 0.45, 0.40, 0.35, 0.30, 0.25, 0.20, 0.15, 0.10, 0.05, 0 };
            double[] accuracies = new double[21];

            // If there are no notes, we just return the empty polynomial.
            if (noteDifficulties.Count + tailDifficulties.Count == 0)
                return skillLevels;

            accuracies[0] = 1;

            for (int i = 1; i < skillLevels.Length; i++)
            {
                accuracies[i] = accuracyAt(ssValue * skillLevels[i]);
            }

            return accuracies;
        }

        public double SkillLevelAtAccuracy(double accuracy)
        {
            if (noteDifficulties.Count == 0)
                return 0;

            double maxNoteDifficulty = noteDifficulties.Max();
            double maxTailDifficulty = tailDifficulties.Count > 0 ? tailDifficulties.Max() : 0;

            if (maxNoteDifficulty + maxTailDifficulty == 0)
                return 0;

            double skill = RootFinding.FindRootExpand(skill => accuracyProb(accuracy, skill) - accuracy_prob, 0, Math.Max(maxNoteDifficulty, maxTailDifficulty) * 2, accuracy: 0.002);

            return skill;
        }

        /// <summary>
        /// The probability of achieving x accuracy given y skill. We approximate this using the central limit theorem because it would be expensive to compute manually.
        /// </summary>
        /// <param name="accuracy"></param>
        /// <param name="skill"></param>
        private double accuracyProb(double accuracy, double skill)
        {
            // Just a little root finding trick since accuracy can have be above 0% even at 0 skill. Doing this lets the root finding algorithm find a root anyway.
            if (skill == 0)
                return 0;

            // Special handling for SS scores, which don't play nice with the normal approximation.
            if (accuracy >= 1)
            {
                double p = 1;

                if (noteDifficulties.Count < bin_threshold)
                {
                    for (int i = 0; i < noteDifficulties.Count; i++)
                    {
                        double unstableRate = skillToUr(skill, noteDifficulties[i]);

                        p *= hitWindows.HitProbability(hitWindows.HMax, unstableRate);
                    }

                    for (int i = 0; i < tailDifficulties.Count; i++)
                    {
                        double unstableRate = skillToUrTail(skill, tailDifficulties[i]);

                        p *= hitWindows.HitProbability(hitWindows.HMax * 1.5, unstableRate);
                    }
                }
                else
                {
                    for (int i = 0; i < binNotes.Count; i++)
                    {
                        double unstableRate = skillToUr(skill, binNotes[i].Difficulty);

                        p *= Math.Pow(hitWindows.HitProbability(hitWindows.HMax, unstableRate), binNotes[i].Count);
                    }

                    for (int i = 0; i < binTails.Count; i++)
                    {
                        double unstableRate = skillToUr(skill, binTails[i].Difficulty);

                        p *= Math.Pow(hitWindows.HitProbability(hitWindows.HMax * 1.5, unstableRate), binTails[i].Count);
                    }
                }

                return p;
            }

            (double mean, double dev) = accuracyDistributionAt(skill);

            return 1 - DifficultyCalculationUtils.NormalCdf(mean, dev, accuracy);
        }

        /// <summary>
        /// The percentile accuracy achieved at skill.
        /// </summary>
        /// <param name="skill"></param>
        private double accuracyAt(double skill)
        {
            // Just a little root finding trick since accuracy can have be above 0% even at 0 skill. Doing this lets the root finding algorithm find a root anyway.
            if (skill == 0)
                return 0;

            (double mean, double dev) = accuracyDistributionAt(skill);

            double accuracy = RootFinding.FindRootExpand(accuracy => (1 - DifficultyCalculationUtils.NormalCdf(mean, dev, accuracy)) - accuracy_prob, 0, 1);

            return accuracy;
        }

        private (double mean, double deviation) accuracyDistributionAt(double skill)
        {
            double count = noteDifficulties.Count + tailDifficulties.Count;

            double sum = 0;
            double varSum = 0;

            // Threshold for binning
            if (noteDifficulties.Count < bin_threshold)
            {
                for (int i = 0; i < noteDifficulties.Count; i++)
                {
                    var noteProbs = getNoteProbabilities(noteDifficulties[i], skill);

                    sum += noteProbs.Score;
                    varSum += noteProbs.Variance;
                }

                for (int i = 0; i < tailDifficulties.Count; i++)
                {
                    var tailProbs = getTailProbabilities(tailDifficulties[i], skill);

                    sum += tailProbs.Score;
                    varSum += tailProbs.Variance;
                }
            }
            else
            {
                for (int i = 0; i < binNotes.Count; i++)
                {
                    var noteProbs = getNoteProbabilities(binNotes[i].Difficulty, skill);

                    sum += binNotes[i].Count * noteProbs.Score;
                    varSum += binNotes[i].Count * noteProbs.Variance;
                }

                for (int i = 0; i < binTails.Count; i++)
                {
                    var tailProbs = getTailProbabilities(binTails[i].Difficulty, skill);

                    sum += binTails[i].Count * tailProbs.Score;
                    varSum += binTails[i].Count * tailProbs.Variance;
                }
            }

            double mean = sum / count / MAX_JUDGEMENT_WEIGHT;
            double deviation = Math.Sqrt(varSum) / count / MAX_JUDGEMENT_WEIGHT + 1e-6;

            return (mean, deviation);
        }

        private JudgementProbabilities getNoteProbabilities(double difficulty, double skill)
        {
            double unstableRate = skillToUr(skill, difficulty);

            // Probability of landing in each hit window
            double hMax = hitWindows.HitProbability(hitWindows.HMax * 1.5, unstableRate);
            double h300 = hitWindows.HitProbability(hitWindows.H300 * 1.5, unstableRate);
            double h200 = hitWindows.HitProbability(hitWindows.H200 * 1.5, unstableRate);
            double h100 = hitWindows.HitProbability(hitWindows.H100 * 1.5, unstableRate);
            double h50 = hitWindows.HitProbability(hitWindows.H50 * 1.5, unstableRate);

            // Probability of getting each hit judgement
            double pMax = hMax;
            double p300 = h300 - hMax;
            double p200 = h200 - h300;
            double p100 = h100 - h200;
            double p50 = h50 - h100;

            return new JudgementProbabilities(pMax, p300, p200, p100, p50);
        }

        private JudgementProbabilities getTailProbabilities(double difficulty, double skill)
        {
            double unstableRate = skillToUrTail(skill, difficulty);

            // Probability of landing in each hit window
            double hMax = hitWindows.HitProbability(hitWindows.HMax * 1.5, unstableRate);
            double h300 = hitWindows.HitProbability(hitWindows.H300 * 1.5, unstableRate);
            double h200 = hitWindows.HitProbability(hitWindows.H200 * 1.5, unstableRate);
            double h100 = hitWindows.HitProbability(hitWindows.H100 * 1.5, unstableRate);
            double h50 = hitWindows.HitProbability(hitWindows.H50 * 1.5, unstableRate);

            // Probability of getting each hit judgement
            double pMax = hMax;
            double p300 = h300 - hMax;
            double p200 = h200 - h300;
            double p100 = h100 - h200;
            double p50 = h50 - h100;

            return new JudgementProbabilities(pMax, p300, p200, p100, p50);
        }

        // When your skill equals the note difficulty, you get around 99%, and when your skill level is 0 (mashing), you get around 50%.
        private double skillToUr(double skill, double d) => d != 0 ? mash_ur * Math.Pow(skill_ur / mash_ur, Math.Pow(skill / d, accuracyExponent)) : 0;
        private double skillToUrTail(double skill, double d) => d != 0 ? mash_ur * Math.Pow(skill_ur * tailDeviationMultiplier / mash_ur, Math.Pow(skill / d, accuracyExponent)) : 0;
    }
}
