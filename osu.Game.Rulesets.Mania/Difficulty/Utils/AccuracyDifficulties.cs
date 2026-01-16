// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class AccuracyDifficulties
    {
        // Difficulty multiplier at 99.6, 99, 98, 95, and 90% accuracy. 98% difficulty is our note's difficulty, and 80% is always 0
        private static readonly double[] accuracy_multipliers_lenient = [1.02, 1.01, 1.00, 0.97, 0.87, 0.65, 0];
        private static readonly double[] accuracy_multipliers_harsh = [1.05, 1.025, 1.00, 0.925, 0.75, 0.5, 0];

        private static readonly double[] accuracy_values = [1, 0.99, 0.98, 0.95, 0.9, 0.85, 0.8];
        private readonly double[] difficulties = new double[accuracy_values.Length];

        public AccuracyDifficulties(double difficulty, Lenience lenience)
        {
            for (int i = 0; i < accuracy_values.Length; i++)
            {
                difficulties[i] = difficulty * getMultipliersFor(lenience)[i];
            }
        }

        private AccuracyDifficulties(double[] difficulties)
        {
            this.difficulties = difficulties;
        }

        public static AccuracyDifficulties Pow(AccuracyDifficulties difficultiesBase, double exponent)
        {
            double[] newDifficulties = new double[accuracy_values.Length];

            for (int i = 0; i < accuracy_values.Length; i++)
            {
                newDifficulties[i] = Math.Pow(difficultiesBase.difficulties[i], exponent);
            }

            return new AccuracyDifficulties(newDifficulties);
        }

        public static AccuracyDifficulties operator +(AccuracyDifficulties left, AccuracyDifficulties right)
        {
            double[] newDifficulties = new double[accuracy_values.Length];

            for (int i = 0; i < accuracy_values.Length; i++)
            {
                newDifficulties[i] = left.difficulties[i] + right.difficulties[i];
            }

            return new AccuracyDifficulties(newDifficulties);
        }

        public static AccuracyDifficulties operator +(AccuracyDifficulties left, double right)
        {
            double[] newDifficulties = new double[accuracy_values.Length];

            for (int i = 0; i < accuracy_values.Length; i++)
            {
                newDifficulties[i] = left.difficulties[i] + right;
            }

            return new AccuracyDifficulties(newDifficulties);
        }

        public static AccuracyDifficulties operator -(AccuracyDifficulties left, AccuracyDifficulties right)
        {
            double[] newDifficulties = new double[accuracy_values.Length];

            for (int i = 0; i < accuracy_values.Length; i++)
            {
                newDifficulties[i] = left.difficulties[i] + right.difficulties[i];
            }

            return new AccuracyDifficulties(newDifficulties);
        }

        public static AccuracyDifficulties operator *(AccuracyDifficulties left, AccuracyDifficulties right)
        {
            double[] newDifficulties = new double[accuracy_values.Length];

            for (int i = 0; i < accuracy_values.Length; i++)
            {
                newDifficulties[i] = left.difficulties[i] * right.difficulties[i];
            }

            return new AccuracyDifficulties(newDifficulties);
        }

        public static AccuracyDifficulties operator *(AccuracyDifficulties left, double right)
        {
            double[] newDifficulties = new double[accuracy_values.Length];

            for (int i = 0; i < accuracy_values.Length; i++)
            {
                newDifficulties[i] = left.difficulties[i] * right;
            }

            return new AccuracyDifficulties(newDifficulties);
        }

        public static AccuracyDifficulties operator /(AccuracyDifficulties left, AccuracyDifficulties right)
        {
            double[] newDifficulties = new double[accuracy_values.Length];

            for (int i = 0; i < accuracy_values.Length; i++)
            {
                newDifficulties[i] = left.difficulties[i] / right.difficulties[i];
            }

            return new AccuracyDifficulties(newDifficulties);
        }

        public double AccuracyAt(double skill)
        {
            if (skill > difficulties[0])
                return accuracy_values[0];
            if (skill == 0)
                return accuracy_values[^1];

            for (int i = 1; i < difficulties.Length; i++)
            {
                if (difficulties[i] > skill)
                    continue;

                double upperSkillBound = difficulties[i - 1];
                double lowerSkillBound = difficulties[i];

                double upperAccuracyBound = accuracy_values[i - 1];
                double lowerAccuracyBound = accuracy_values[i];

                return Interpolation.Lerp(lowerAccuracyBound, upperAccuracyBound, (skill - lowerSkillBound) / (upperSkillBound - lowerSkillBound));
            }

            return 0;
        }

        public double DifficultyAt(double accuracy)
        {
            if (accuracy >= accuracy_values[0])
                return difficulties[0];
            if (accuracy <= accuracy_values[^1])
                return difficulties[^1];

            for (int i = 1; i < difficulties.Length; i++)
            {
                if (accuracy_values[i] > accuracy)
                    continue;

                double upperSkillBound = difficulties[i - 1];
                double lowerSkillBound = difficulties[i];

                double upperAccuracyBound = accuracy_values[i - 1];
                double lowerAccuracyBound = accuracy_values[i];

                return Interpolation.Lerp(lowerSkillBound, upperSkillBound, (accuracy - lowerAccuracyBound) / (upperAccuracyBound - lowerAccuracyBound));
            }

            return 0;
        }

        public enum Lenience
        {
            Lenient,
            Harsh
        }

        private double[] getMultipliersFor(Lenience lenience)
        {
            switch (lenience)
            {
                case Lenience.Lenient:
                    return accuracy_multipliers_lenient;

                case Lenience.Harsh:
                    return accuracy_multipliers_harsh;
            }

            return accuracy_multipliers_harsh;
        }
    }
}
