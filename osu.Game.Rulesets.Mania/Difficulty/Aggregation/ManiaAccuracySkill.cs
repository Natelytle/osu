// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Aggregation
{
    public abstract class ManiaAccuracySkill : Skill
    {
        protected abstract double DifficultyMultiplier { get; }

        // The value of the max judgement. Increasing this value increases the value of high ratios.
        public const double MAX_JUDGEMENT_WEIGHT = 305;

        // Star rating for a map is the difficulty of achieving 95% accuracy.
        private const double star_rating_accuracy = 0.95;

        // The player has a 2% chance of achieving the score's accuracy.
        private const double accuracy_prob = 0.02;

        // The UR a player is expected to get on a note with the same difficulty as their skill level.
        private const double skill_ur = 12;

        // The UR a player is expected to get when mashing, the very highest their UR can ever be.
        private const double mash_ur = 100;

        // How much the player's UR should change relative to the note's difficulty, when it is higher or lower.
        private double accuracyExponent => 1.8;

        // How much long note tails should increase the player's UR.
        private double tailDeviationMultiplier => 1.8;

        // To calculate accuracy at a skill level correctly, we need this information.
        private readonly bool lazerMechanics;
        private DiffHitWindows hitWindows;

        // We need to use dictionaries so that we can attach tails to the correct heads, or else we cannot process stable accuracy properly.
        private readonly List<double> noteDifficulties = new List<double>();
        private readonly List<(double Head, double Tail)> longNoteDifficulties = new List<(double, double)>();

        private List<BinNote>? binNotes;

        // Lazer mechanics let us split heads and tails and treat them like notes.
        private List<BinNote>? binHeads;
        private List<BinNote>? binTails;

        // Stable mechanics depend on having both heads and tails available at the same time, so we must bin them together.
        // Since this is slower, we only do it when necessary.
        private List<BinLongNote>? binLongNotes;

        protected ManiaAccuracySkill(Mod[] mods, double od)
            : base(mods)
        {
            lazerMechanics = !mods.Any(m => m is ManiaModClassic);
            hitWindows = new DiffHitWindows(mods, od);
        }

        public override void Process(DifficultyHitObject current)
        {
            double strainValue = StrainValueOf(current);

            switch (current.BaseObject)
            {
                case not HoldNote:
                    noteDifficulties.Add(strainValue);
                    break;

                case HoldNote:
                    longNoteDifficulties.Add((strainValue, strainValue));
                    break;
            }
        }

        protected abstract double StrainValueOf(DifficultyHitObject current);

        public override double DifficultyValue()
        {
            binNotes = null;
            binHeads = null;
            binTails = null;
            binLongNotes = null;

            return skillLevelAtAccuracy(star_rating_accuracy);
        }

        public double[] AccuracyCurve()
        {
            double[] skillLevels = new double[20];
            double[] accuracies = { 1.00, 0.998, 0.995, 0.99, 0.98, 0.97, 0.96, 0.95, 0.90, 0.80, 0.70 };

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

            binNotes ??= BinNote.CreateBins(noteDifficulties, 32);

            if (lazerMechanics)
            {
                binHeads ??= BinNote.CreateBins(longNoteDifficulties.ConvertAll(d => d.Head), 32);
                binTails ??= BinNote.CreateBins(longNoteDifficulties.ConvertAll(d => d.Tail), 32);
            }
            else
            {
                binLongNotes ??= BinLongNote.CreateBins(longNoteDifficulties, 8);
            }

            double skill = RootFinding.FindRootExpand(skill => accuracyProb(accuracy, skill) - accuracy_prob, 0, maxDifficulty * 2);

            return skill * DifficultyMultiplier;
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

            return noteDifficulties.Count > 128 || longNoteDifficulties.Count > 128 ? accuracyProbBinned(accuracy, skill) : accuracyProbExact(accuracy, skill);
        }

        private double accuracyProbExact(double accuracy, double skill)
        {
            double count = noteDifficulties.Count;

            double sum = 0;
            double varSum = 0;

            for (int i = 0; i < noteDifficulties.Count; i++)
            {
                var noteProbs = getNoteProbabilities(noteDifficulties[i], skill);

                sum += noteProbs.Score;
                varSum += noteProbs.Variance;
            }

            if (lazerMechanics)
            {
                count += longNoteDifficulties.Count * 2;

                for (int i = 0; i < longNoteDifficulties.Count; i++)
                {
                    var noteProbs = getNoteProbabilities(longNoteDifficulties[i].Head, skill);

                    sum += noteProbs.Score;
                    varSum += noteProbs.Variance;

                    var tailProbs = getTailProbabilities(longNoteDifficulties[i].Tail, skill);

                    sum += tailProbs.Score;
                    varSum += tailProbs.Variance;
                }
            }
            else
            {
                count += longNoteDifficulties.Count;

                for (int i = 0; i < longNoteDifficulties.Count; i++)
                {
                    var longNoteProbs = getLongNoteProbabilities(longNoteDifficulties[i], skill);

                    sum += longNoteProbs.Score;
                    varSum += longNoteProbs.Variance;
                }
            }

            double mean = sum / count / MAX_JUDGEMENT_WEIGHT;
            double dev = Math.Sqrt(varSum) / count / MAX_JUDGEMENT_WEIGHT + 1e-6;

            // Due to real world factors, deviation is actually a bit higher than the model says.
            // 2.5 is chosen to bring the variance at 96% up from 0.2% to 0.5%.
            dev *= 2.5;

            double p = 1 - DifficultyCalculationUtils.NormalCdf(mean, dev, accuracy);

            return p;
        }

        private double accuracyProbBinned(double accuracy, double skill)
        {
            double count = noteDifficulties.Count;

            double sum = 0;
            double varSum = 0;

            for (int i = 0; i < binNotes!.Count; i++)
            {
                var noteProbs = getNoteProbabilities(binNotes[i].Difficulty, skill);

                sum += binNotes[i].Count * noteProbs.Score;
                varSum += binNotes[i].Count * noteProbs.Variance;
            }

            if (lazerMechanics)
            {
                count += longNoteDifficulties.Count * 2;

                for (int i = 0; i < binHeads!.Count; i++)
                {
                    var noteProbs = getNoteProbabilities(binHeads[i].Difficulty, skill);

                    sum += binHeads[i].Count * noteProbs.Score;
                    varSum += binHeads[i].Count * noteProbs.Variance;
                }

                for (int i = 0; i < binTails!.Count; i++)
                {
                    var tailProbs = getTailProbabilities(binTails![i].Difficulty, skill);

                    sum += binTails[i].Count * tailProbs.Score;
                    varSum += binTails[i].Count * tailProbs.Variance;
                }
            }
            else
            {
                count += longNoteDifficulties.Count;

                for (int i = 0; i < binLongNotes!.Count; i++)
                {
                    var longNoteProbs = getLongNoteProbabilities((binLongNotes[i].HeadDifficulty, binLongNotes[i].TailDifficulty), skill);

                    sum += binLongNotes[i].Count * longNoteProbs.Score;
                    varSum += binLongNotes[i].Count * longNoteProbs.Variance;
                }
            }

            double mean = sum / count / MAX_JUDGEMENT_WEIGHT;
            double dev = Math.Sqrt(varSum) / count / MAX_JUDGEMENT_WEIGHT + 1e-6;

            // Due to real world factors, deviation is actually a bit higher than the model says.
            // 2.5 is chosen to bring the variance at 96% up from 0.2% to 0.5%.
            dev *= 2.5;

            double p = 1 - DifficultyCalculationUtils.NormalCdf(mean, dev, accuracy);

            return p;
        }

        private JudgementProbs getNoteProbabilities(double difficulty, double skill)
        {
            double unstableRate = skillToUr(skill, difficulty);

            double pMax = hitWindows.HitProbability(hitWindows.HMax, unstableRate);
            double p300 = hitWindows.HitProbability(hitWindows.H300, unstableRate) - hitWindows.HitProbability(hitWindows.HMax, unstableRate);
            double p200 = hitWindows.HitProbability(hitWindows.H200, unstableRate) - hitWindows.HitProbability(hitWindows.H300, unstableRate);
            double p100 = hitWindows.HitProbability(hitWindows.H100, unstableRate) - hitWindows.HitProbability(hitWindows.H200, unstableRate);
            double p50 = hitWindows.HitProbability(hitWindows.H50, unstableRate) - hitWindows.HitProbability(hitWindows.H100, unstableRate);

            return new JudgementProbs(pMax, p300, p200, p100, p50);
        }

        private JudgementProbs getTailProbabilities(double difficulty, double skill)
        {
            double unstableRate = skillToUrTail(skill, difficulty);

            double pMax = hitWindows.HitProbability(hitWindows.HMax * 1.5, unstableRate);
            double p300 = hitWindows.HitProbability(hitWindows.H300 * 1.5, unstableRate) - hitWindows.HitProbability(hitWindows.HMax * 1.5, unstableRate);
            double p200 = hitWindows.HitProbability(hitWindows.H200 * 1.5, unstableRate) - hitWindows.HitProbability(hitWindows.H300 * 1.5, unstableRate);
            double p100 = hitWindows.HitProbability(hitWindows.H100 * 1.5, unstableRate) - hitWindows.HitProbability(hitWindows.H200 * 1.5, unstableRate);
            double p50 = hitWindows.HitProbability(hitWindows.H50 * 1.5, unstableRate) - hitWindows.HitProbability(hitWindows.H100 * 1.5, unstableRate);

            return new JudgementProbs(pMax, p300, p200, p100, p50);
        }

        private JudgementProbs getLongNoteProbabilities((double head, double tail) difficulties, double skill)
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

        // When your skill equals the note difficulty, you get around 99%, and when your skill level is 0 (mashing), you get around 50%.
        private double skillToUr(double skill, double d) => d != 0 ? mash_ur * Math.Pow(skill_ur / mash_ur, Math.Pow(skill / d, accuracyExponent)) : 0;
        private double skillToUrTail(double skill, double d) => d != 0 ? mash_ur * Math.Pow(skill_ur * tailDeviationMultiplier / mash_ur, Math.Pow(skill / d, accuracyExponent)) : 0;
    }
}
