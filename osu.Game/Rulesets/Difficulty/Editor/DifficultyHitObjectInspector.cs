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
        private EditorDifficultyProvider difficultyProvider { get; set; } = null!;

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

            if (difficultyProvider.CurrentObject is null)
                return;

            foreach (PropertyInfo property in difficultyProvider.CurrentObject.GetType().GetProperties())
                addValue(property.Name.Titleize(), property.GetValue(difficultyProvider.CurrentObject));

            // Ignore fields where the name is all uppercase, as per naming convention their constants and it's the only way to identify them.
            static bool isConst(FieldInfo field) => field.Name.All(x => !char.IsLetter(x) || char.IsUpper(x));
            foreach (FieldInfo field in difficultyProvider.CurrentObject.GetType().GetFields().Where(isConst))
                addValue(field.Name.Titleize(), field.GetValue(difficultyProvider.CurrentObject));
        }

        private void addValue(string name, object? value)
        {
            string valueStr = value switch
            {
                null => "null",
                int i => i.ToString("N0"),
                float f => f.ToString("#,#0.#####"),
                double d => d.ToString("#,#0.#####"),
                bool b => b ? "Yes" : "No",
                Vector2 v => $"{v.X} {v.Y} ({v.Length:#,#0.#})",
                _ => null!
            };

            if (valueStr is null)
                return;

            valueStr += name switch
            {
                string s when s.EndsWith("Distance") => "px",
                string s when s.EndsWith("Time") => "ms",
                _ => ""
            };

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
