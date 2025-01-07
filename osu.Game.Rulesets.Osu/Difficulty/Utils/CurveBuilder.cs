// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Utils
{
    public class CurveBuilder
    {
        public static double BuildSmootherStep(double x, params (double x, double y)[] points)
        {
            if (points.Length == 0)
                return 0;

            points = points.OrderBy(p => p.x).ToArray();

            if (x < points[0].x)
                return points[0].y;
            if (x > points[^1].x)
                return points[^1].y;

            int i = Array.FindLastIndex(points, p => p.x < x);

            if (i < 0 || i >= points.Length)
                return 0;

            return DifficultyCalculationUtils.Smootherstep(x, points[i].x, points[i + 1].x) * (points[i + 1].y - points[i].y) + points[i].y;
        }

        public static double BuildLerp(double x, params (double x, double y)[] points)
        {
            if (points.Length == 0)
                return 0;

            points = points.OrderBy(p => p.x).ToArray();

            if (x < points[0].x)
                return points[0].y;
            if (x > points[^1].x)
                return points[^1].y;

            int i = Array.FindLastIndex(points, p => p.x < x);

            if (i == -1)
                return 0;

            double xAdj = (x - points[i].x) / (points[i + 1].x - points[i].x);

            return Interpolation.Lerp(points[i].y, points[i + 1].y, xAdj);
        }
    }
}
