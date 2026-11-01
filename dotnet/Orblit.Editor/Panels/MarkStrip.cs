using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Sequence;

/// <summary>
/// The strip of marks: named moments in the clip. A cutscene is started by a mark that is named after it.
/// </summary>
internal static class MarkStrip
{
    private const string AddHint = "Name it after a cutscene to start that cutscene here.";

    public static TimelineStrip Create(ClipBench bench, MarkModel marks)
    {
        var strip = new TimelineStrip("timeline_marks", "Marks", bench, axis => Flags(marks, bench, axis));

        // Smaller than the bar's buttons, to sit in a thin row.
        strip.AddToName(new ToolbarAction("timeline_mark_add", AssetIcons.Add, "Add a mark at the playhead", () => AskAdd(strip, marks))
        {
            Width = 20,
            Height = 20,
        });
        return strip;
    }

    /// <summary>Shows <paramref name="prompt"/> over the window that <paramref name="from"/> is in, and returns what was typed, or null.</summary>
    public static async Task<string?> Ask(Control from, NamePrompt prompt) =>
        TopLevel.GetTopLevel(from) is Window owner ? await prompt.ShowDialog<string?>(owner) : null;

    private static IEnumerable<Control> Flags(MarkModel marks, ClipBench bench, TimeAxis axis)
    {
        var index = 0;
        foreach (var mark in bench.Clip?.Marks ?? [])
        {
            var flag = new MarkFlag(index++, mark, marks);

            // The flag's pole is at its left, a little in from the edge.
            Canvas.SetLeft(flag, axis.XOf(mark.At) - 6);
            Canvas.SetTop(flag, 2);
            yield return flag;
        }
    }

    private static async void AskAdd(Control from, MarkModel marks)
    {
        var prompt = new NamePrompt("Add a mark", AddHint, "Mark") { AcceptText = "Add" };
        if (await Ask(from, prompt) is { } name)
        {
            marks.Add(name);
        }
    }
}

/// <summary>One mark on its strip. Pressing it offers to rename it, move it to the playhead, or delete it.</summary>
internal sealed class MarkFlag : Button
{
    private readonly int _index;
    private readonly Mark _mark;
    private readonly MarkModel _marks;

    public MarkFlag(int index, Mark mark, MarkModel marks)
    {
        _index = index;
        _mark = mark;
        _marks = marks;
        Name = $"timeline_mark_{index}";
        Height = TimelineTracks.RowHeight - 4;
        MinHeight = 0;
        Padding = new Thickness(2, 0, Space.Xs, 0);
        Background = Brushes.Transparent;
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new Glyph(TimelineIcons.Flag, 12, OrblitColors.Warn),
                new TextBlock { Text = mark.Name, FontSize = OrblitText.Caption, Foreground = OrblitColors.InkMid },
            },
        };
        ToolTip.SetTip(this, $"{mark.Name} at {TimelineWords.Seconds(mark.At)}");
        Click += (_, _) => OpenMenu();
    }

    private void OpenMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(ToolbarMenu.Item($"timeline_mark_rename_{_index}", "Rename", AskRename));
        menu.Items.Add(ToolbarMenu.Item($"timeline_mark_move_{_index}", "Move to the playhead", () => _marks.MoveToPlayhead(_index)));
        menu.Items.Add(ToolbarMenu.Item($"timeline_mark_delete_{_index}", "Delete", () => _marks.Delete(_index)));
        ContextMenu = menu;
        menu.Open(this);
    }

    private async void AskRename()
    {
        var prompt = new NamePrompt("Rename mark", "Name", _mark.Name) { AcceptText = "Rename" };
        if (await MarkStrip.Ask(this, prompt) is { } name)
        {
            _marks.Rename(_index, name);
        }
    }
}
