// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Skinning;

namespace osu.Game.Screens.Play.HUD
{
    public abstract partial class PerformancePointsCounter : RollingCounter<int>
    {
        public bool UsesFixedAnchor { get; set; }

        [Resolved]
        private ScoreProcessor? scoreProcessor { get; set; }

        [Resolved]
        private GameplayState? gameplayState { get; set; }

        private readonly CancellationTokenSource loadCancellationSource = new CancellationTokenSource();

        private JudgementResult? lastJudgement;
        private DifficultyCalculator? difficultyCalculator;
        private ScoreInfo scoreInfo = null!;
        private IWorkingBeatmap? gameplayWorkingBeatmap;

        private Mod[]? clonedMods;

        [BackgroundDependencyLoader]
        private void load(BeatmapDifficultyCache difficultyCache)
        {
            if (gameplayState != null)
            {
                gameplayWorkingBeatmap = new GameplayWorkingBeatmap(gameplayState.Beatmap);

                difficultyCalculator = gameplayState.Ruleset.CreateDifficultyCalculator();
                clonedMods = gameplayState.Mods.Select(m => m.DeepClone()).ToArray();

                scoreInfo = new ScoreInfo(gameplayState.Score.ScoreInfo.BeatmapInfo, gameplayState.Score.ScoreInfo.Ruleset) { Mods = clonedMods };
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (scoreProcessor != null)
            {
                scoreProcessor.NewJudgement += onJudgementChanged;
                scoreProcessor.JudgementReverted += onJudgementChanged;
            }

            if (gameplayState?.LastJudgementResult.Value != null)
                onJudgementChanged(gameplayState.LastJudgementResult.Value);
        }

        public virtual bool IsValid { get; set; }

        private void onJudgementChanged(JudgementResult judgement)
        {
            lastJudgement = judgement;

            var beatmapUntilJudgement = getPartialWorkingBeatmapAt(judgement);

            if (gameplayState == null || beatmapUntilJudgement == null || scoreProcessor == null)
            {
                IsValid = false;
                return;
            }

            scoreProcessor.PopulateScore(scoreInfo);
            Current.Value = (int)Math.Round(difficultyCalculator?.Calculate(scoreInfo.Mods, beatmapUntilJudgement, scoreInfo).PerfAttribs?.Total ?? 0, MidpointRounding.AwayFromZero);
            IsValid = true;
        }

        private GameplayWorkingBeatmap? getPartialWorkingBeatmapAt(JudgementResult judgement)
        {
            if (gameplayWorkingBeatmap == null)
                return null;

            var objectStartTimes = gameplayWorkingBeatmap.Beatmap.HitObjects.Select(o => o.StartTime).ToList();

            int objectIndex = objectStartTimes.BinarySearch(judgement.HitObject.StartTime);
            if (objectIndex < 0)
                objectIndex = ~objectIndex - 1;

            var trimmedGameplayBeatmap = new GameplayWorkingBeatmap(gameplayWorkingBeatmap.Beatmap);

            trimmedGameplayBeatmap.PartialBeatmap.HitObjects = gameplayWorkingBeatmap.Beatmap.HitObjects.ToList()[..objectIndex];

            return trimmedGameplayBeatmap;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (scoreProcessor != null)
            {
                scoreProcessor.NewJudgement -= onJudgementChanged;
                scoreProcessor.JudgementReverted -= onJudgementChanged;
            }

            loadCancellationSource?.Cancel();
        }

        // TODO: This class shouldn't exist, but requires breaking changes to allow DifficultyCalculator to receive an IBeatmap.
        private class GameplayWorkingBeatmap : WorkingBeatmap
        {
            public readonly PartialBeatmap PartialBeatmap;

            public GameplayWorkingBeatmap(IBeatmap gameplayBeatmap)
                : base(gameplayBeatmap.BeatmapInfo, null)
            {
                PartialBeatmap = new PartialBeatmap(gameplayBeatmap);
            }

            public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken cancellationToken)
                => PartialBeatmap;

            protected override IBeatmap GetBeatmap() => PartialBeatmap;

            public override Texture GetBackground() => throw new NotImplementedException();

            protected override Track GetBeatmapTrack() => throw new NotImplementedException();

            protected internal override ISkin GetSkin() => throw new NotImplementedException();

            public override Stream GetStream(string storagePath) => throw new NotImplementedException();
        }

        /// <summary>
        /// Used to calculate timed difficulty attributes, where only a subset of hitobjects should be visible at any point in time.
        /// </summary>
        private class PartialBeatmap : IBeatmap
        {
            private readonly IBeatmap baseBeatmap;
            public List<HitObject> HitObjects;

            public PartialBeatmap(IBeatmap baseBeatmap)
            {
                this.baseBeatmap = baseBeatmap;
                HitObjects = baseBeatmap.HitObjects.ToList();
            }

            IReadOnlyList<HitObject> IBeatmap.HitObjects => HitObjects;

            #region Delegated IBeatmap implementation

            public BeatmapInfo BeatmapInfo
            {
                get => baseBeatmap.BeatmapInfo;
                set => baseBeatmap.BeatmapInfo = value;
            }

            public ControlPointInfo ControlPointInfo
            {
                get => baseBeatmap.ControlPointInfo;
                set => baseBeatmap.ControlPointInfo = value;
            }

            public BeatmapMetadata Metadata => baseBeatmap.Metadata;

            public BeatmapDifficulty Difficulty
            {
                get => baseBeatmap.Difficulty;
                set => baseBeatmap.Difficulty = value;
            }

            public List<BreakPeriod> Breaks => baseBeatmap.Breaks;
            public List<string> UnhandledEventLines => baseBeatmap.UnhandledEventLines;

            public double TotalBreakTime => baseBeatmap.TotalBreakTime;
            public IEnumerable<BeatmapStatistic> GetStatistics() => baseBeatmap.GetStatistics();
            public double GetMostCommonBeatLength() => baseBeatmap.GetMostCommonBeatLength();
            public IBeatmap Clone() => new PartialBeatmap(baseBeatmap.Clone());

            #endregion
        }
    }
}
