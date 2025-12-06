// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.Difficulty.Utils.AccuracySimulation;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Tests
{
    public class ManiaDifficultyAccuracySimulatorTest
    {
        [Test, Combinatorial]
        public void TestMoreNotesHigherSkill(
            [Range(1, 100)] int noteCount,
            [Values(0.80, 0.90, 0.95, 1.0)] double accuracy
        )
        {
            List<double> lessDifficulties = Enumerable.Repeat(10.0, noteCount).ToList();
            AccuracySimulator less = new AccuracySimulator(Array.Empty<Mod>(), 8, lessDifficulties, new List<double>());

            List<double> moreDifficulties = Enumerable.Repeat(10.0, noteCount + 1).ToList();
            AccuracySimulator more = new AccuracySimulator(Array.Empty<Mod>(), 8, moreDifficulties, new List<double>());

            double skillLevelLess = less.SkillLevelAtAccuracy(accuracy);
            double skillLevelMore = more.SkillLevelAtAccuracy(accuracy);

            Assert.That(skillLevelLess, Is.LessThanOrEqualTo(skillLevelMore));
        }

        [Test, Combinatorial]
        public void TestDiffSpikeHigherSkill(
            [Values(0.80, 0.90, 0.95, 1.0)] double accuracy
        )
        {
            List<double> lessDifficulties = Enumerable.Repeat(10.0, 600).ToList();
            AccuracySimulator less = new AccuracySimulator(Array.Empty<Mod>(), 8, lessDifficulties, new List<double>());

            List<double> moreDifficulties = Enumerable.Repeat(10.0, 600).Append(20.0).ToList();
            AccuracySimulator more = new AccuracySimulator(Array.Empty<Mod>(), 8, moreDifficulties, new List<double>());

            double skillLevelLess = less.SkillLevelAtAccuracy(accuracy);
            double skillLevelMore = more.SkillLevelAtAccuracy(accuracy);

            Assert.That(skillLevelLess, Is.LessThanOrEqualTo(skillLevelMore));
        }
    }
}
