// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics;
using osu.Game.Graphics.Containers;
using osuTK;
using osu.Game.Overlays;
using osu.Game.Graphics;
using osu.Game.Rulesets.Edit;
using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Difficulty.Editor
{
    public partial class DifficultyEvaluatorInspector : EditorToolboxGroup
    {
        protected record Evaluator(string Name, Func<DifficultyHitObject, double> EvaluateDifficultyOf);

        [Resolved]
        private EditorDifficultyProvider difficultyProvider { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private OsuTextFlowContainer text = null!;

        protected virtual Evaluator[] Evaluators { get; } = [];

        public DifficultyEvaluatorInspector() : base("Evaluators", true) { }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Spacing = new Vector2(5),
                Children =
                [
                    text = new OsuTextFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y
                    }
                ]
            };

            Scheduler.AddDelayed(update, 20, true);
        }

        private void update()
        {
            text.Clear();

            if (difficultyProvider.CurrentObject is null)
                return;

            foreach (Evaluator eval in Evaluators)
            {
                text.AddParagraph($"{eval.Name}:", s =>
                {
                    s.Padding = new MarginPadding { Top = 2 };
                    s.Font = s.Font.With(size: 12);
                    s.Colour = colourProvider.Content2;
                });

                string evalOutput = Math.Round(eval.EvaluateDifficultyOf(difficultyProvider.CurrentObject), 5).ToString();

                if (eval.Name is "Snap Aim" or "Flow Aim")
                    evalOutput += $" ({Math.Round(Math.Pow(eval.EvaluateDifficultyOf(difficultyProvider.CurrentObject), 0.6) / 3.9, 2)})";

                text.AddParagraph(evalOutput, s =>
                {
                    s.Font = s.Font.With(weight: FontWeight.SemiBold);
                    s.Colour = colourProvider.Content1;
                });
            }
        }
    }
}
