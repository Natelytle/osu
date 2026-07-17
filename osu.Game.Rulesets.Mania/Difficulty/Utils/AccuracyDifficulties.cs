// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class AccuracyDifficulties
    {
        public double BaseDifficulty => difficulties[2]; // Hacky - the difficulty at 98%.
        private readonly double[] difficulties = new double[AccuracyValueMultipliers.ACCURACY_VALUES.Length];

        public AccuracyDifficulties() { }

        public AccuracyDifficulties(double difficulty, AccuracyValueMultipliers multipliers)
        {
            for (int i = 0; i < AccuracyValueMultipliers.ACCURACY_VALUES.Length; i++)
            {
                difficulties[i] = difficulty * multipliers.AccuracyMultipliers[i];
            }
        }

        private AccuracyDifficulties(double[] difficulties)
        {
            this.difficulties = difficulties;
        }

        public static AccuracyDifficulties Pow(AccuracyDifficulties difficultiesBase, double exponent)
        {
            double[] newDifficulties = new double[AccuracyValueMultipliers.ACCURACY_VALUES.Length];

            for (int i = 0; i < AccuracyValueMultipliers.ACCURACY_VALUES.Length; i++)
            {
                newDifficulties[i] = Math.Pow(difficultiesBase.difficulties[i], exponent);
            }

            return new AccuracyDifficulties(newDifficulties);
        }

        public static AccuracyDifficulties Norm(double exponent, params AccuracyDifficulties[] normedDifficulties)
        {
            AccuracyDifficulties newDifficulties = new AccuracyDifficulties();

            for (int i = 0; i < normedDifficulties.Length; i++)
            {
                newDifficulties += Pow(normedDifficulties[i], exponent);
            }

            newDifficulties = Pow(newDifficulties, 1 / exponent);

            return newDifficulties;
        }

        public static AccuracyDifficulties operator +(AccuracyDifficulties left, AccuracyDifficulties right)
        {
            double[] newDifficulties = new double[AccuracyValueMultipliers.ACCURACY_VALUES.Length];

            for (int i = 0; i < AccuracyValueMultipliers.ACCURACY_VALUES.Length; i++)
            {
                newDifficulties[i] = left.difficulties[i] + right.difficulties[i];
            }

            return new AccuracyDifficulties(newDifficulties);
        }

        public static AccuracyDifficulties operator +(AccuracyDifficulties left, double right)
        {
            double[] newDifficulties = new double[AccuracyValueMultipliers.ACCURACY_VALUES.Length];

            for (int i = 0; i < AccuracyValueMultipliers.ACCURACY_VALUES.Length; i++)
            {
                newDifficulties[i] = left.difficulties[i] + right;
            }

            return new AccuracyDifficulties(newDifficulties);
        }

        public static AccuracyDifficulties operator -(AccuracyDifficulties left, AccuracyDifficulties right)
        {
            double[] newDifficulties = new double[AccuracyValueMultipliers.ACCURACY_VALUES.Length];

            for (int i = 0; i < AccuracyValueMultipliers.ACCURACY_VALUES.Length; i++)
            {
                newDifficulties[i] = left.difficulties[i] - right.difficulties[i];
            }

            return new AccuracyDifficulties(newDifficulties);
        }

        public static AccuracyDifficulties operator *(AccuracyDifficulties left, AccuracyDifficulties right)
        {
            double[] newDifficulties = new double[AccuracyValueMultipliers.ACCURACY_VALUES.Length];

            for (int i = 0; i < AccuracyValueMultipliers.ACCURACY_VALUES.Length; i++)
            {
                newDifficulties[i] = left.difficulties[i] * right.difficulties[i];
            }

            return new AccuracyDifficulties(newDifficulties);
        }

        public static AccuracyDifficulties operator *(AccuracyDifficulties left, double right)
        {
            double[] newDifficulties = new double[AccuracyValueMultipliers.ACCURACY_VALUES.Length];

            for (int i = 0; i < AccuracyValueMultipliers.ACCURACY_VALUES.Length; i++)
            {
                newDifficulties[i] = left.difficulties[i] * right;
            }

            return new AccuracyDifficulties(newDifficulties);
        }

        public static AccuracyDifficulties operator /(AccuracyDifficulties left, AccuracyDifficulties right)
        {
            double[] newDifficulties = new double[AccuracyValueMultipliers.ACCURACY_VALUES.Length];

            for (int i = 0; i < AccuracyValueMultipliers.ACCURACY_VALUES.Length; i++)
            {
                newDifficulties[i] = left.difficulties[i] / right.difficulties[i];
            }

            return new AccuracyDifficulties(newDifficulties);
        }

        public double AccuracyAt(double skill)
        {
            if (skill >= difficulties[0])
                return AccuracyValueMultipliers.ACCURACY_VALUES[0];
            if (skill <= 0)
                return AccuracyValueMultipliers.ACCURACY_VALUES[^1];

            for (int i = 1; i < difficulties.Length; i++)
            {
                if (difficulties[i] > skill)
                    continue;

                double upperSkillBound = difficulties[i - 1];
                double lowerSkillBound = difficulties[i];

                double upperAccuracyBound = AccuracyValueMultipliers.ACCURACY_VALUES[i - 1];
                double lowerAccuracyBound = AccuracyValueMultipliers.ACCURACY_VALUES[i];

                return Interpolation.Lerp(lowerAccuracyBound, upperAccuracyBound, (skill - lowerSkillBound) / (upperSkillBound - lowerSkillBound));
            }

            return 0;
        }

        public double DifficultyAt(double accuracy)
        {
            if (accuracy >= AccuracyValueMultipliers.ACCURACY_VALUES[0])
                return difficulties[0];
            if (accuracy <= AccuracyValueMultipliers.ACCURACY_VALUES[^1])
                return difficulties[^1];

            for (int i = 1; i < difficulties.Length; i++)
            {
                if (AccuracyValueMultipliers.ACCURACY_VALUES[i] > accuracy)
                    continue;

                double upperSkillBound = difficulties[i - 1];
                double lowerSkillBound = difficulties[i];

                double upperAccuracyBound = AccuracyValueMultipliers.ACCURACY_VALUES[i - 1];
                double lowerAccuracyBound = AccuracyValueMultipliers.ACCURACY_VALUES[i];

                return Interpolation.Lerp(lowerSkillBound, upperSkillBound, (accuracy - lowerAccuracyBound) / (upperAccuracyBound - lowerAccuracyBound));
            }

            return 0;
        }
    }
}
