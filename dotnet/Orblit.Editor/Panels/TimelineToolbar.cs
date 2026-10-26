using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Sequence;

/// <summary>
/// The bar along the top of the timeline: which clip, where it plays, how long and how fast it is, and
/// what to do with the keys that are picked.
/// </summary>
/// <remarks>
/// Built once and told to <see cref="Refresh"/>. A frame of playback changes the bench, and a bar that
/// was rebuilt for each one would lose a half-typed length and close a menu it was in.
/// </remarks>
internal sealed class TimelineToolbar : Border
{
    private static readonly double[] Rates = [24, 25, 30, 60];
    private static readonly Hold[] Holds = [Hold.Step, Hold.Linear, Hold.Smooth, Hold.Curve];

    private readonly ClipBench _bench;
    private readonly TimelineModel _model;
    private readonly TimelineLinks _links;
    private readonly ToolbarMenu _clip;
    private readonly ToolbarAction _play;
    private readonly TextBox _length;
    private readonly ToolbarMenu _rate;
    private readonly ToolbarMenu _done;
    private readonly ToolbarMenu _owner;
    private readonly ToolbarMenu _hold;
    private readonly ToolbarAction _auto;
    private readonly ToolbarAction _delete;

    // What the length box last said, so the box reporting its own starting text is not an edit.
    private string _lastLength = "";

    public TimelineToolbar(ClipBench bench, TimelineModel model, TimelineLinks links, SkeletonLook? look)
    {
        _bench = bench;
        _model = model;
        _links = links;
        Height = 38;
        Padding = new Thickness(Space.Sm, 0);
        BorderBrush = OrblitColors.LineSoft;
        BorderThickness = new Thickness(0, 0, 0, 1);

        var words = TimelineWords.For(bench.PlayedOn);
        _clip = new ToolbarMenu("timeline_clip", null, ClipItems, TimelineIcons.Of(bench.PlayedOn));
        _play = new ToolbarAction("timeline_play", AssetIcons.Play, "Play (Space)", model.Play);
        _length = LengthBox(words.Length);
        _rate = new ToolbarMenu("timeline_rate", "Frames a second, which is what keys and the playhead snap to", RateItems);
        _done = new ToolbarMenu("timeline_done", "What the clip does when it gets to the end", DoneItems);
        _owner = new ToolbarMenu("timeline_owner", "What the clip is played on while it is edited", OwnerItems, TimelineIcons.Owner);
        _hold = new ToolbarMenu("timeline_hold", "How the picked keys travel to the next", HoldItems) { Label = "Hold" };
        _auto = new ToolbarAction("timeline_auto", TimelineIcons.Auto, "Work the picked keys' slopes out again", model.AutoSlopes);
        _delete = new ToolbarAction("timeline_delete", TimelineIcons.Delete, "Delete the picked keys (Delete)", model.DeleteKeys);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = Left(look),
        });
        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        right.Children.AddRange([_hold, _auto, _delete]);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        Child = grid;
    }

    /// <summary>Brings every label, picture and enabled state up to date with the bench.</summary>
    public void Refresh()
    {
        if (_bench.Shown is not { } shown)
        {
            return;
        }

        var clip = shown.Clip;
        var playing = _bench.Playing;
        var picked = _bench.Selection.Count > 0;
        _clip.Label = Titled(shown);
        _play.Show(playing ? AssetIcons.Pause : AssetIcons.Play, playing ? "Pause (Space)" : "Play (Space)", playing);
        ShowLength(TimelineWords.Seconds(clip.Duration));
        _rate.Label = Rate(clip.Rate);
        _done.Label = TimelineWords.Ending(clip.WhenDone);
        ShowOwner();
        _hold.IsEnabled = picked;
        _auto.IsEnabled = picked;
        _delete.IsEnabled = picked;
    }

    private static string Rate(double rate) => $"{TimelineWords.Seconds(rate)} fps";

    private static TextBlock Caption(string text) => new TextBlock
    {
        Text = text,
        FontSize = OrblitText.Caption,
        Foreground = OrblitColors.InkDim,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private StackPanel Left(SkeletonLook? look)
    {
        var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Spacing = Space.Xs };
        left.Children.AddRange(
        [
            _clip,
            new ToolbarAction("timeline_start", TimelineIcons.Start, "To the start (Home)", _model.Home) { Margin = new Thickness(Space.Sm, 0, 0, 0) },
            _play,
            new ToolbarAction("timeline_end", TimelineIcons.End, "To the end (End)", _model.End),
        ]);
        if (look is not null)
        {
            left.Children.Add(new SkeletonControls(look) { Margin = new Thickness(Space.Md, 0, 0, 0) });
        }

        left.Children.AddRange([Caption("Length"), _length, Caption("s"), _rate, _done]);

        // A cutscene names what it moves by the scene's own ids, so there is nothing to choose to play it on.
        if (_bench.PlayedOn == PlayedOn.Owner)
        {
            left.Children.Add(_owner);
        }

        return left;
    }

    private TextBox LengthBox(string tip)
    {
        var box = new TextBox
        {
            Name = "timeline_length",
            Width = 64,
            Height = 26,
            MinHeight = 0,
            Padding = new Thickness(Space.Sm, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = OrblitText.Label,
            FontFamily = OrblitText.Mono,
            Margin = new Thickness(Space.Xs, 0),
        };
        ToolTip.SetTip(box, tip);
        box.TextChanged += (_, _) =>
        {
            var now = box.Text ?? "";
            if (now != _lastLength)
            {
                _lastLength = now;
                _model.SetLength(now);
            }
        };
        // A length that was not a number is put back to the one the clip has.
        box.LostFocus += (_, _) =>
        {
            _model.FinishLength();
            Refresh();
        };
        return box;
    }

    // Left alone while it is being typed in, so a caret does not jump to the end.
    private void ShowLength(string text)
    {
        if (!_length.IsFocused && _length.Text != text)
        {
            _lastLength = text;
            _length.Text = text;
        }
    }

    private void ShowOwner()
    {
        var owner = _bench.ObjectOf("");
        _owner.Label = owner is null ? "Played on nothing" : $"On {owner.Name}";
        _owner.Warn = owner is null;
    }

    private string Titled(OpenClip open) => _bench.IsUnsaved(open) ? $"{open.Clip.Name} •" : open.Clip.Name;

    private IEnumerable<Control> ClipItems()
    {
        if (_bench.Shown is not { } shown)
        {
            yield break;
        }

        var unsaved = _bench.IsUnsaved(shown);
        var index = 0;
        foreach (var open in _bench.Open)
        {
            var path = open.Path;
            var item = ToolbarMenu.Item($"timeline_clip_{index++}", Titled(open), () => _bench.Show(path));
            item.ToggleType = MenuItemToggleType.CheckBox;
            item.IsChecked = ReferenceEquals(open, shown);
            yield return item;
        }

        yield return ToolbarMenu.Item("timeline_clip_new", TimelineWords.For(_bench.PlayedOn).Make, _links.OnNew);
        if (_links.OnRetarget is { } retarget)
        {
            yield return ToolbarMenu.Item("timeline_clip_retarget", "Retarget to another model…", retarget);
        }

        yield return new Separator();
        yield return ToolbarMenu.Item("timeline_clip_save", $"Save {shown.Clip.Name}", unsaved ? () => Save(shown) : null);
        yield return ToolbarMenu.Item("timeline_clip_close", unsaved ? "Close without saving" : "Close", () => _bench.Close(shown.Path));
    }

    private void Save(OpenClip shown)
    {
        if (_bench.Save(shown) is { } problem)
        {
            _links.OnProblem?.Invoke(problem);
        }
    }

    private IEnumerable<Control> RateItems()
    {
        var now = _bench.Clip?.Rate;
        return Rates.Select(rate => ToolbarMenu.Item($"timeline_rate_{rate}", Rate(rate), rate == now ? null : () => _model.SetRate(rate)));
    }

    private IEnumerable<Control> DoneItems()
    {
        var now = _bench.Clip?.WhenDone;
        return Enum.GetValues<WhenDone>().Select(done => ToolbarMenu.Item(
            $"timeline_done_{done.ToString().ToLowerInvariant()}",
            TimelineWords.Ending(done),
            done == now ? null : () => _model.SetWhenDone(done)));
    }

    private IEnumerable<Control> OwnerItems()
    {
        var owner = _bench.ObjectOf("");
        var selected = _links.Selected?.Invoke();
        yield return ToolbarMenu.Item(
            "timeline_owner_selected",
            selected is null ? "Select something to play it on" : $"Play on {selected.Name}",
            selected is null || selected.Id == owner?.Id ? null : () => _bench.Owner = selected.Id);
        yield return ToolbarMenu.Item("timeline_owner_none", "Play on nothing", owner is null ? null : () => _bench.Owner = null);
    }

    private IEnumerable<Control> HoldItems()
    {
        foreach (var hold in Holds)
        {
            yield return ToolbarMenu.Item($"timeline_hold_{hold.ToString().ToLowerInvariant()}", TimelineWords.Holding(hold), () => _model.SetHold(hold));
        }

        var shaped = new MenuItem { Name = "timeline_hold_shaped", Header = TimelineWords.Holding(Hold.Shaped) };
        foreach (var shape in Enum.GetValues<Easing>())
        {
            shaped.Items.Add(ToolbarMenu.Item($"timeline_hold_shaped_{shape.ToString().ToLowerInvariant()}", TimelineWords.Shape(shape), () => _model.SetHold(Hold.Shaped, shape)));
        }

        yield return shaped;
    }
}
