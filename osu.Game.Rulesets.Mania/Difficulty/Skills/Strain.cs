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
        private readonly List<double?> tailDifficulties = new List<double?>();

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

        private double getNoteAccuracy(double difficulty, double? tailDifficulty, double skill, double windowMultiplier = 1)
        {
            if (difficulty + (tailDifficulty ?? 0) == 0)
                return 1;

            // These formulas is uuuugly, but it ensures that when your skill equals the note difficulty, you get 100 UR (or 100 * tailDeviationMultiplier), and when your skill level is 0 (mashing), you get 500 UR.
            double skillToUr(double d) => 10 / Math.Pow((skill * (1 - Math.Pow(0.2, 1 / BalancingConstants.ACC)) + d * Math.Pow(0.2, 1 / BalancingConstants.ACC)) / d, BalancingConstants.ACC);
            double skillToUrTail(double d) => 10 * tailDeviationMultiplier / Math.Pow((skill * (1 - Math.Pow(0.2 * tailDeviationMultiplier, 1 / BalancingConstants.ACC)) + d * Math.Pow(0.2 * tailDeviationMultiplier, 1 / BalancingConstants.ACC)) / d, BalancingConstants.ACC);

            // Check window multiplier to see if the current note is a lazer LN tail.
            double unstableRate = windowMultiplier == 1.0 ? skillToUr(difficulty) : skillToUrTail(difficulty);

            double pMax;
            double p300;
            double p200;
            double p100;
            double p50;

            double accuracy;

            // In case the current note is a classic Long Note, we need a special formula to account for it only giving one judgement.
            if (tailDifficulty is not null)
            {
                double tailUnstableRate = skillToUrTail(tailDifficulty.Value);

                pMax = hitWindows.HitProbabilityLn(hitWindows.HMax, unstableRate, tailUnstableRate);
                p300 = hitWindows.HitProbabilityLn(hitWindows.H300, unstableRate, tailUnstableRate) - hitWindows.HitProbabilityLn(hitWindows.HMax, unstableRate, tailUnstableRate);
                p200 = hitWindows.HitProbabilityLn(hitWindows.H200, unstableRate, tailUnstableRate) - hitWindows.HitProbabilityLn(hitWindows.H300, unstableRate, tailUnstableRate);
                p100 = hitWindows.HitProbabilityLn(hitWindows.H100, unstableRate, tailUnstableRate) - hitWindows.HitProbabilityLn(hitWindows.H200, unstableRate, tailUnstableRate);
                p50 = hitWindows.HitProbabilityLn(hitWindows.H50, unstableRate, tailUnstableRate) - hitWindows.HitProbabilityLn(hitWindows.H100, unstableRate, tailUnstableRate);

                accuracy = (320 * pMax + 300 * p300 + 200 * p200 + 100 * p100 + 50 * p50) / 320;
                return accuracy;
            }

            pMax = hitWindows.HitProbability(hitWindows.HMax * windowMultiplier, unstableRate);
            p300 = hitWindows.HitProbability(hitWindows.H300 * windowMultiplier, unstableRate) - hitWindows.HitProbability(hitWindows.HMax * windowMultiplier, unstableRate);
            p200 = hitWindows.HitProbability(hitWindows.H200 * windowMultiplier, unstableRate) - hitWindows.HitProbability(hitWindows.H300 * windowMultiplier, unstableRate);
            p100 = hitWindows.HitProbability(hitWindows.H100 * windowMultiplier, unstableRate) - hitWindows.HitProbability(hitWindows.H200 * windowMultiplier, unstableRate);
            p50 = hitWindows.HitProbability(hitWindows.H50 * windowMultiplier, unstableRate) - hitWindows.HitProbability(hitWindows.H100 * windowMultiplier, unstableRate);

            accuracy = (320 * pMax + 300 * p300 + 200 * p200 + 100 * p100 + 50 * p50) / 320;
            return accuracy;
        }

        public double TotalAccuracyAt(double skill)
        {
            double averageAccuracy = 0;
            double count = 0;

            if (lazerMechanics)
            {
                for (int i = 0; i < noteDifficulties.Count; i++)
                {
                    averageAccuracy = (count * averageAccuracy + getNoteAccuracy(noteDifficulties[i], null, skill)) / (count + 1);
                    count += 1;

                    if (tailDifficulties[i] is not null)
                    {
                        averageAccuracy = (count * averageAccuracy + getNoteAccuracy(tailDifficulties[i]!.Value, null, skill, 1.5 / tailDeviationMultiplier)) / (count + 1);
                        count += 1;
                    }
                }
            }
            else
            {
                for (int i = 0; i < noteDifficulties.Count; i++)
                {
                    averageAccuracy = (count * averageAccuracy + getNoteAccuracy(noteDifficulties[i], tailDifficulties[i], skill)) / (count + 1);
                    count += 1;
                }
            }

            return averageAccuracy;
        }

        public override double DifficultyValue()
        {
            if (noteDifficulties.Count == 0 || noteDifficulties.Max() == 0)
                return 0;

            const double target_accuracy = 0.95;

            double skillLevel = RootFinding.FindRootExpand(x => TotalAccuracyAt(x) - target_accuracy, 0, noteDifficulties.Max(), accuracy: 1e-4);

            if (noteDifficulties.Count % 100 == 0)
                Console.WriteLine(noteDifficulties.Count);

            return skillLevel;
        }

        // The skill level required to get 99% or the accuracy attained if you got an additional 300, whichever accuracy is higher.
        // This value is not calculated for a true SS or else the star rating would be infinite.
        public double SSValue()
        {
            if (noteDifficulties.Count == 0 || noteDifficulties.Max() == 0)
                return 0;

            double totalNotes = lazerMechanics ? noteDifficulties.Count + tailDifficulties.Count(x => x is not null) : noteDifficulties.Count;

            double targetAccuracy = Math.Max(0.99, (totalNotes * 320 + 300) / (totalNotes * 320 + 320));

            double skillLevel = RootFinding.FindRootExpand(x => TotalAccuracyAt(x) - targetAccuracy, 0, noteDifficulties.Max() * 2, accuracy: 1e-4);

            return skillLevel;
        }

        public ExpPolynomial AccuracyCurve()
        {
            double[] accuracyLosses = new double[21];
            double[] penalties = { 1, 0.95, 0.9, 0.85, 0.8, 0.75, 0.7, 0.65, 0.6, 0.55, 0.5, 0.45, 0.4, 0.35, 0.3, 0.25, 0.2, 0.15, 0.1, 0.05, 0 };

            double fcSkill = SSValue();

            ExpPolynomial curve = new ExpPolynomial();

            // If there are no notes, we just return the empty polynomial.
            if (noteDifficulties.Count == 0 || noteDifficulties.Max() == 0)
                return curve;

            // Still need to add binning
            // var bins = Bin.CreateBins(difficulties);

            for (int i = 0; i < penalties.Length; i++)
            {
                if (i == 0)
                {
                    accuracyLosses[i] = 0;
                    continue;
                }

                double penalizedSkill = fcSkill * penalties[i];

                accuracyLosses[i] = 1 - TotalAccuracyAt(penalizedSkill);
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

            if (current.BaseObject is TailNote && prevInColumn is not null)
            {
                int headIndex = prevInColumn.NoNestedIndex;

                tailDifficulties[headIndex] = strainValueTail(current);
            }
            else
            {
                noteDifficulties.Add(strainValueNote(current));
                tailDifficulties.Add(null);
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
