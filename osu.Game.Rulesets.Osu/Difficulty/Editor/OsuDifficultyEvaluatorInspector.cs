// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Graphics.Containers;
using osuTK;
using osu.Game.Overlays;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Rulesets.Osu.Difficulty.Editor
{
    internal partial class OsuDifficultyEvaluatorInspector : EditorToolboxGroup
    {
        [Resolved]
        private DifficultyEditorBeatmap difficultyBeatmap { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private OsuTextFlowContainer evaluatorText = null!;
        private OsuSpriteText selectNoteHint = null!;

        public OsuDifficultyEvaluatorInspector() : base("Evaluators", true) { }

        [BackgroundDependencyLoader]
        private void load()
        {
            difficultyBeatmap.DifficultyHitObjects.ValueChanged += _ => update();

            Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Spacing = new Vector2(5),
                Children =
                [
                    selectNoteHint = new OsuSpriteText
                    {
                        Text = "Please select a note.",
                        RelativeSizeAxes = Axes.X
                    },
                    evaluatorText = new OsuTextFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y
                    }
                ]
            };
        }

        private void update()
        {
            evaluatorText.Clear();
            selectNoteHint.Show();

            if (difficultyBeatmap.SelectedDifficultyHitObjects.Length != 1)
                return;

            selectNoteHint.Hide();

            DifficultyHitObject diffObject = difficultyBeatmap.SelectedDifficultyHitObjects.Single();

            addResult("Snap Aim", SnapAimEvaluator.EvaluateDifficultyOf(diffObject));
            addResult("Flow Aim", FlowAimEvaluator.EvaluateDifficultyOf(diffObject));
            addResult("Speed", SpeedEvaluator.EvaluateDifficultyOf(diffObject));
            addResult("Rhythm", RhythmEvaluator.EvaluateDifficultyOf(diffObject));
            addResult("Flashlight (hidden = false)", FlashlightEvaluator.EvaluateDifficultyOf(diffObject, false));
            addResult("Flashlight (hidden = true)", FlashlightEvaluator.EvaluateDifficultyOf(diffObject, true));
        }

        private void addResult(string name, double value)
        {
            evaluatorText.AddParagraph($"{name}:", s =>
            {
                s.Padding = new MarginPadding { Top = 2 };
                s.Font = s.Font.With(size: 12);
                s.Colour = colourProvider.Content2;
            });

            evaluatorText.AddParagraph(value.ToString(), s =>
            {
                s.Font = s.Font.With(weight: FontWeight.SemiBold);
                s.Colour = colourProvider.Content1;
            });
        }
    }
}
