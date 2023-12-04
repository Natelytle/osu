// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Scoring;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Judgements;

namespace osu.Game.Rulesets.Osu.Statistics
{
    public partial class OsuPerformanceChart : CompositeDrawable
    {
        private readonly ScoreInfo score;
        private readonly List<(double, double)> timedPpValues = new List<(double, double)>();
        private readonly IBeatmap playableBeatmap;

        public OsuPerformanceChart(ScoreInfo score, IBeatmap playableBeatmap)
        {
            this.score = score;
            this.playableBeatmap = playableBeatmap;

            setTimedPPValues();
        }

        private void load()
        {
        }

        private void setTimedPPValues()
        {
            var diffCalc = new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, new FlatWorkingBeatmap(playableBeatmap));

            var iterativePerformanceCalculator = new IterativePerformanceCalculator()
            {
                ScoreInfo = score,
                TimedAttributes = diffCalc.CalculateTimed(score.Mods),
                PerformanceCalculator = new OsuPerformanceCalculator()
            };

            var scoreHitEvents = score.HitEvents.ToArray();

            for (int i = 0; i < playableBeatmap.HitObjects.Count; i++)
            {
                var currentJudgement = new OsuJudgementResult(playableBeatmap.HitObjects[i], new OsuJudgement())
                {
                    Type = scoreHitEvents[i].Result
                };

                iterativePerformanceCalculator.OnJudgementChanged(currentJudgement);
                double currentTime = playableBeatmap.HitObjects[i].StartTime;

                timedPpValues.Add((iterativePerformanceCalculator.Value, currentTime));
            }
        }

        private class IterativePerformanceCalculator
        {
            public double Value;

            public ScoreInfo ScoreInfo;
            public List<TimedDifficultyAttributes> TimedAttributes;
            public PerformanceCalculator PerformanceCalculator;

            public void OnJudgementChanged(JudgementResult judgement)
            {
                var attrib = getAttributeAtTime(judgement);

                if (attrib == null)
                {
                    return;
                }

                Value = PerformanceCalculator?.Calculate(ScoreInfo, attrib).Total ?? 0;
            }

            [CanBeNull]
            private DifficultyAttributes getAttributeAtTime(JudgementResult judgement)
            {
                if (TimedAttributes == null || TimedAttributes.Count == 0)
                    return null;

                int attribIndex = TimedAttributes.BinarySearch(new TimedDifficultyAttributes(judgement.HitObject.GetEndTime(), null));
                if (attribIndex < 0)
                    attribIndex = ~attribIndex - 1;

                return TimedAttributes[Math.Clamp(attribIndex, 0, TimedAttributes.Count - 1)].Attributes;
            }
        }
    }
}
