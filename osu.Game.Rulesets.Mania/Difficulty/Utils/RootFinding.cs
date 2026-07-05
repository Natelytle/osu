// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public static class RootFinding
    {
        private const double golden_ratio = 1.6180339887498949;
        private const double golden_section = 0.3819660112501051; // 1 - 1/golden_ratio
        private const double eps = 1e-11;

        /// <summary>
        /// Finds the minimum of an <paramref name="function"/> using Brent's method (golden-section search with
        /// inverse parabolic interpolation), expanding the bounds if a bracketing minimum is not located within them.
        /// Expansion only occurs for the upward bound, as this function is optimized for functions of range [0, x),
        /// which is useful for finding skill level (skill can never be below 0).
        /// </summary>
        /// <param name="function">The function of which to find the minimum.</param>
        /// <param name="guessLowerBound">The lower bound of the function inputs.</param>
        /// <param name="guessUpperBound">The upper bound of the function inputs.</param>
        /// <param name="maxIterations">The maximum number of iterations before the function returns its best estimate.</param>
        /// <param name="accuracy">The desired precision in which the minimum's location is returned.</param>
        public static double FindMinimumExpand(Func<double, double> function, double guessLowerBound, double guessUpperBound, int maxIterations = 25, double accuracy = 1e-6D)
        {
            double a = guessLowerBound;
            double b = guessUpperBound;
            double fa = function(a);
            double fb = function(b);
            double expansions = 0;

            // Expand upward, following the downhill direction, until the function starts increasing again -
            // i.e. until we have three points a < b < c with fb <= fa and fb <= fc (a bracketed minimum).
            double c, fc;

            if (fb > fa)
            {
                // Function is already increasing from the lower bound - assume the minimum lies within
                // [a, b] (possibly at the boundary a) and use it directly as the bracket.
                c = b;
                fc = fb;
                b = 0.5 * (a + c);
                fb = function(b);
            }
            else
            {
                c = b + golden_ratio * (b - a);
                fc = function(c);

                while (fc <= fb)
                {
                    a = b;
                    fa = fb;
                    b = c;
                    fb = fc;
                    c = b + golden_ratio * (b - a);
                    fc = function(c);

                    expansions += 1;

                    if (expansions > 32)
                    {
                        double test = function(c);
                        throw new ArgumentOutOfRangeException();
                    }
                }
            }

            // Brent's method: golden-section search safeguarding inverse parabolic interpolation.
            double lo = Math.Min(a, c);
            double hi = Math.Max(a, c);
            double x = b, w = b, v = b;
            double fx = fb, fw = fb, fv = fb;
            double d = 0, e = 0;

            for (int i = 0; i < maxIterations; i++)
            {
                double xm = 0.5 * (lo + hi);
                double tol1 = accuracy * Math.Abs(x) + eps;
                double tol2 = 2 * tol1;

                if (Math.Abs(x - xm) <= tol2 - 0.5 * (hi - lo))
                    return x;

                bool useGolden = true;

                if (Math.Abs(e) > tol1)
                {
                    double r = (x - w) * (fx - fv);
                    double q = (x - v) * (fx - fw);
                    double p = (x - v) * q - (x - w) * r;
                    q = 2 * (q - r);
                    if (q > 0)
                        p = -p;
                    q = Math.Abs(q);
                    double etemp = e;
                    e = d;

                    if (!(Math.Abs(p) >= Math.Abs(0.5 * q * etemp) || p <= q * (lo - x) || p >= q * (hi - x)))
                    {
                        d = p / q;
                        double u = x + d;
                        if (u - lo < tol2 || hi - u < tol2)
                            d = xm >= x ? tol1 : -tol1;
                        useGolden = false;
                    }
                }

                if (useGolden)
                {
                    e = x >= xm ? lo - x : hi - x;
                    d = golden_section * e;
                }

                double xt = Math.Abs(d) >= tol1 ? x + d : x + (d >= 0 ? tol1 : -tol1);
                double ft = function(xt);

                if (ft <= fx)
                {
                    if (xt >= x)
                        lo = x;
                    else
                        hi = x;

                    v = w; fv = fw;
                    w = x; fw = fx;
                    x = xt; fx = ft;
                }
                else
                {
                    if (xt < x)
                        lo = xt;
                    else
                        hi = xt;

                    if (ft <= fw || w == x)
                    {
                        v = w; fv = fw;
                        w = xt; fw = ft;
                    }
                    else if (ft <= fv || v == x || v == w)
                    {
                        v = xt; fv = ft;
                    }
                }
            }

            return x;
        }
    }
}
