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
using System.Reflection;
using Humanizer;

namespace osu.Game.Rulesets.Difficulty.Editor
{
    internal partial class DifficultyAttributesInspector : EditorToolboxGroup
    {
        [Resolved]
        private EditorDifficultyProvider difficultyProvider { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private OsuTextFlowContainer text = null!;

        public DifficultyAttributesInspector() : base("Attributes", true) { }

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

            if (difficultyProvider.CurrentDifficultyAttributes is null)
                return;

            text.AddParagraph("Difficulty Attributes", s =>
            {
                s.Font = s.Font.With(weight: FontWeight.SemiBold);
                s.Font = s.Font.With(size: 16);
                s.Colour = colourProvider.Colour0;
            });

            foreach (PropertyInfo property in difficultyProvider.CurrentDifficultyAttributes.Attributes.GetType().GetProperties())
                addResult(property.Name.Titleize(), property.GetValue(difficultyProvider.CurrentDifficultyAttributes.Attributes));
        }

        private void addResult(string name, object? value)
        {
            string valueStr = value switch
            {
                null => "null",
                int i => i.ToString("N0"),
                double d => d.ToString("#,#0.#####"),
                _ => null!
            };

            if (valueStr is null)
                return;

            text.AddParagraph($"{name}:", s =>
            {
                s.Padding = new MarginPadding { Top = 2 };
                s.Font = s.Font.With(size: 12);
                s.Colour = colourProvider.Content2;
            });

            text.AddParagraph(valueStr, s =>
            {
                s.Font = s.Font.With(weight: FontWeight.SemiBold);
                s.Colour = colourProvider.Content1;
            });
        }
    }
}
