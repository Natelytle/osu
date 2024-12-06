// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
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
using System;

namespace osu.Game.Rulesets.Difficulty.Editor
{
    internal partial class DifficultyHitObjectInspector : EditorToolboxGroup
    {
        [Resolved]
        private DifficultyEditorBeatmap difficultyBeatmap { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private OsuTextFlowContainer text = null!;

        public DifficultyHitObjectInspector() : base("Difficulty Hit Object", true) { }

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

            if (difficultyBeatmap.CurrentObject is null)
                return;

            foreach (PropertyInfo property in difficultyBeatmap.CurrentObject.GetType().GetProperties())
                addResult(property.Name.Titleize(), property.GetValue(difficultyBeatmap.CurrentObject));

            // Ignore fields where the name is all uppercase, as per naming convention their constants and it's the only way to identify them.
            foreach (FieldInfo field in difficultyBeatmap.CurrentObject.GetType().GetFields().Where(x => x.Name.Any(x => char.IsLetter(x) && !char.IsUpper(x))))
                addResult(field.Name.Titleize(), field.GetValue(difficultyBeatmap.CurrentObject));
        }

        private void addResult(string name, object? value)
        {
            string valueStr = value switch
            {
                null => "null",
                int i => i.ToString("N0"),
                float f => Math.Round(f, 5).ToString("N"),
                double d => Math.Round(d, 5).ToString("N"),
                bool b => b ? "Yes" : "No",
                Vector2 v => $"{v.X} ; {v.Y} ({v.Length})",
                Vector3 v => $"{v.X} ; {v.Y} ; {v.Z} ({v.Length})",
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
