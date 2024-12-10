// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Threading;
using osu.Game.Rulesets.Difficulty.Editor;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.Compose.Components
{
    public partial class HitObjectInspector : EditorInspector
    {
        [Resolved]
        private EditorDifficultyProvider difficultyProvider { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            Scheduler.AddDelayed(updateInspectorText, 20, true);
        }

        private void updateInspectorText()
        {
            InspectorText.Clear();

            HitObject[] objects;

            if (EditorBeatmap.SelectedHitObjects.Count > 0)
                objects = EditorBeatmap.SelectedHitObjects.ToArray();
            else if (EditorBeatmap.PlacementObject.Value != null)
                objects = new[] { EditorBeatmap.PlacementObject.Value };
            else
                objects = difficultyProvider.GetCurrentObject() is DifficultyHitObject o ? [o.BaseObject] : [];

            AddInspectorValues(objects);
        }

        protected virtual void AddInspectorValues(HitObject[] objects)
        {
            switch (objects.Length)
            {
                case 0:
                    AddValue("No selection");
                    break;

                case 1:
                    var selected = objects.Single();

                    AddHeader("Type");
                    AddValue($"{selected.GetType().ReadableName()}");

                    AddHeader("Time");
                    AddValue($"{selected.StartTime:#,0.##}ms");

                    switch (selected)
                    {
                        case IHasPosition pos:
                            AddHeader("Position");
                            AddValue($"x:{pos.X:#,0.##}");
                            AddValue($"y:{pos.Y:#,0.##}");
                            break;

                        case IHasXPosition x:
                            AddHeader("Position");

                            AddValue($"x:{x.X:#,0.##} ");
                            break;

                        case IHasYPosition y:
                            AddHeader("Position");

                            AddValue($"y:{y.Y:#,0.##}");
                            break;
                    }

                    if (selected is IHasDistance distance)
                    {
                        AddHeader("Distance");
                        AddValue($"{distance.Distance:#,0.##}px");
                    }

                    if (selected is IHasSliderVelocity sliderVelocity)
                    {
                        AddHeader("Slider Velocity");
                        AddValue($"{sliderVelocity.SliderVelocityMultiplier:#,0.00}x ({sliderVelocity.SliderVelocityMultiplier * EditorBeatmap.Difficulty.SliderMultiplier:#,0.00}x)");
                    }

                    if (selected is IHasRepeats repeats)
                    {
                        AddHeader("Repeats");
                        AddValue($"{repeats.RepeatCount:#,0.##}");
                    }

                    if (selected is IHasDuration duration)
                    {
                        AddHeader("End Time");
                        AddValue($"{duration.EndTime:#,0.##}ms");
                        AddHeader("Duration");
                        AddValue($"{duration.Duration:#,0.##}ms");
                    }

                    break;

                default:
                    AddHeader("Selected Objects");
                    AddValue($"{objects.Length:#,0.##}");

                    AddHeader("Start Time");
                    AddValue($"{objects.Min(o => o.StartTime):#,0.##}ms");

                    AddHeader("End Time");
                    AddValue($"{objects.Max(o => o.GetEndTime()):#,0.##}ms");
                    break;
            }
        }
    }
}
