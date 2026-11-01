using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;

/// <summary>
/// The strip of a cutscene's shots, one bar for each stretch a camera is cut to. Shots that overlap
/// show through each other, which is how a blend between two cameras reads.
/// </summary>
internal static class ShotStrip
{
    private static readonly IBrush Tint = new ImmutableSolidColorBrush(OrblitColors.EmberColor, 0.3);

    public static TimelineStrip Create(ClipBench bench) =>
        new("timeline_shots", "Shots", bench, axis => Bars(bench, axis));

    private static IEnumerable<Control> Bars(ClipBench bench, TimeAxis axis)
    {
        var index = 0;
        foreach (var shot in bench.Shown?.Shots ?? [])
        {
            var bar = new Border
            {
                Name = $"timeline_shot_{index++}",
                Width = shot.Duration * axis.PerSecond,
                Height = TimelineTracks.RowHeight - 6,
                Background = Tint,
                BorderBrush = OrblitColors.EmberDeep,
                BorderThickness = new Thickness(1),
                CornerRadius = Radii.Control,
                ClipToBounds = true,
                Padding = new Thickness(Space.Xs, 0),
                Child = new TextBlock
                {
                    Text = shot.Camera,
                    FontSize = OrblitText.Caption,
                    Foreground = OrblitColors.Ink,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            ToolTip.SetTip(bar, $"{shot.Camera}, {TimelineWords.Seconds(shot.Start)} to {TimelineWords.Seconds(shot.End)}");
            Canvas.SetLeft(bar, axis.XOf(shot.Start));
            Canvas.SetTop(bar, 2);
            yield return bar;
        }
    }
}
