// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.IO.Serialization;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Difficulty.Editor
{
    public partial class EditorDifficultyProvider : Component
    {
        [Resolved] private Screens.Edit.Editor editor { get; set; } = null!;
        [Resolved] private EditorClock editorClock { get; set; } = null!;
        [Resolved] private EditorBeatmap editorBeatmap { get; set; } = null!;

        private Ruleset ruleset = null!;
        private DifficultyCalculator diffCalc = null!;
        private DifficultyHitObject[] difficultyHitObjects = [];
        private TimedDifficultyAttributes[] timedDifficultyAttributes = [];

        [BackgroundDependencyLoader]
        private void load(Bindable<RulesetInfo> rulesetInfo)
        {
            ruleset = rulesetInfo.Value.CreateInstance();
            diffCalc = ruleset.CreateDifficultyCalculator(new FlatWorkingBeatmap(editorBeatmap.PlayableBeatmap));

            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    difficultyHitObjects = diffCalc.CreateDifficultyHitObjectsPublic(editorBeatmap.PlayableBeatmap, 1).ToArray();
                    await Task.Delay(20).ConfigureAwait(true);
                }
            }, TaskCreationOptions.LongRunning);

            // Task.Factory.StartNew(async () =>
            // {
            //     while (true)
            //     {
            //         if (!editor.CalculateDifficultyAttributes.Value)
            //             continue;
            //
            //         //long start = Stopwatch.GetTimestamp();
            //         IBeatmap clonedBeatmap = DifficultyEditorUtils.CloneBeatmap(editorBeatmap.PlayableBeatmap);
            //         DifficultyCalculator diffCalc = ruleset.CreateDifficultyCalculator(new FlatWorkingBeatmap(clonedBeatmap));
            //         //long afterClone = Stopwatch.GetTimestamp();
            //         timedDifficultyAttributes = diffCalc.CalculateTimed([], default).ToArray();
            //         //long afterCalc = Stopwatch.GetTimestamp();
            //         //timedDifficultyAttributes = [new TimedDifficultyAttributes(0, new DifficultyAttributes()
            //         //    {
            //         //    StarRating = (afterClone - start) / TimeSpan.TicksPerMillisecond,
            //         //    MaxCombo = (int)((afterCalc - afterClone) / TimeSpan.TicksPerMillisecond)
            //         //})];
            //         await Task.Delay(1000).ConfigureAwait(true);
            //     }
            // }, TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Returns the "current" difficulty hit object (DHO), which is either:<br/>
        /// 1. The DHO corresponding to the currently selected hit object, if only one object is selected<br/>
        /// 2. The last difficulty hit object from the point of the cursor position in the editor timeline (or null if there's none)<br/><br/>
        /// If <see cref="Screens.Edit.Editor.UseTimelineIfNoSelection"/> is <see langword="false"/>, the second point is ignored, and null is returned.
        /// </summary>
        public DifficultyHitObject? GetCurrentObject()
        {
            DifficultyHitObject[] selected = getSelectedDifficultyHitObjects();

            if (selected.Length == 1)
                return selected[0];

            if (!editor.UseTimelineIfNoSelection.Value)
                return null;

            return difficultyHitObjects.LastOrDefault(x => x.StartTime <= editorClock.CurrentTime);
        }

        /// <summary>
        /// Returns the most recent timed difficulty attributes (TDA) at the time X, with X being either:<br/>
        /// 1. The *end time* of the currently selected hit object (thus including it, since TDAs are always based on the endtime of a hit object)<br/>
        /// 2. The current cursor position in the editor timeline (not including the the latest hit object if the cursor is not past its endtime)<br/>
        /// If there is no most recent TDA at the time X, null is returned.<br/><br/>
        /// If <see cref="Screens.Edit.Editor.UseTimelineIfNoSelection"/> is <see langword="false"/>, the second point is ignored, and null is returned.
        /// </summary>
        public TimedDifficultyAttributes? GetCurrentDifficultyAttributes()
        {
            DifficultyHitObject[] selected = getSelectedDifficultyHitObjects();

            if (selected.Length == 1)
                return timedDifficultyAttributes.FirstOrDefault(x => x.Time == selected[0].EndTime);

            if (!editor.UseTimelineIfNoSelection.Value)
                return null;

            return timedDifficultyAttributes.LastOrDefault(x => x.Time <= editorClock.CurrentTime);
        }

        /// <summary>
        /// Returns the corresponding difficulty hit object for the specified hit object. If the hit object has no corresponding object, null is returned.
        /// </summary>
        /// <param name="obj">The hit object.</param>
        /// <returns>The corresponding difficulty hit object.</returns>
        public DifficultyHitObject? FromBaseObject(HitObject obj) => difficultyHitObjects.FirstOrDefault(x => x.BaseObject == obj);

        /// <summary>
        /// The difficulty hit objects corresponding to the selected hit objects. Hit objects without an equivalent are ignored.
        /// </summary>
        private DifficultyHitObject[] getSelectedDifficultyHitObjects()
            => difficultyHitObjects.Where(x => editorBeatmap.SelectedHitObjects.Contains(x.BaseObject)).ToArray();
    }
}
