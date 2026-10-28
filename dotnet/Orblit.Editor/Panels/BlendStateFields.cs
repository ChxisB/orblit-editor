using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;
using Orblit.Sequence;

/// <summary>The fields of one state: its name, what it plays, how it plays, and the changes it is in.</summary>
/// <param name="rows">What the fields are made of.</param>
/// <param name="blend">The blend the state is in.</param>
/// <param name="state">The state shown.</param>
/// <param name="clips">The clips a state could play.</param>
internal sealed class BlendStateFields(BlendRows rows, BlendDocument blend, BlendState state, IReadOnlyList<string> clips)
{
    private static readonly KeyValuePair<WhenDone?, string>[] Endings =
    [
        new(null, "The clip's own"),
        new(WhenDone.Hold, "Hold the last frame"),
        new(WhenDone.Loop, "Loop"),
        new(WhenDone.Bounce, "Bounce"),
        new(WhenDone.Release, "Let go"),
    ];

    // The name the state has now. Typing a new one is an edit at every keystroke, and the next keystroke has to find it by that.
    private string Here => rows.Bench.Pick is StatePicked(var current) ? current : state.Name;

    public Control Build() => Fields.Column(
    [
        rows.Fields.Section("State", StateSection()),
        rows.Fields.Section("Plays", PlaysSection()),
        rows.Fields.Section("Playback", PlaybackSection()),
        rows.Fields.Section("Changes", ChangesSection()),
    ]);

    private static Func<BlendState, BlendState> OnLine(Func<BlendLine, BlendLine> change) =>
        one => BlendSourceModel.LineOfClips(one.Plays) is { } line ? one.CopyWith(plays: change(line)) : one;

    private static IReadOnlyList<string> Marks(string text) =>
        [.. text.Split(',').Select(static mark => mark.Trim()).Where(static mark => mark.Length > 0)];

    private StackPanel StateSection()
    {
        var starts = blend.WrittenStart == state.Name;
        var start = rows.Act(
            starts ? "Starts here" : "Start here",
            "The blend begins in this state.",
            () => rows.Run("Set start state", one => one.WithStart(Here)),
            !starts);
        var delete = rows.Act(
            "Delete",
            "Take this state out, and every change into or out of it.",
            () => rows.Run("Delete state", one => one.WithStateRemoved(Here), BlendPick.Nothing),
            blend.WrittenStates.Count > 1);
        return Fields.Column(
        [
            rows.Name("Name", state.Name, [], text => RenameTo(text.Trim())),
            new ButtonRow { Margin = new Thickness(0, 6), Children = { start, delete } },
        ]);
    }

    private void RenameTo(string name) =>
        rows.Typed("Rename state", one => one.WithStateRenamed(Here, name), new StatePicked(name));

    private Control PlaysSection()
    {
        if (!BlendSourceModel.CanEdit(state.Plays))
        {
            var note = Fields.Note($"{BlendSourceModel.Describe(state.Plays)}. This one is edited in the file.");
            note.Name = "plays_note";
            return note;
        }

        var line = BlendSourceModel.LineOfClips(state.Plays);
        var kinds = new[] { new KeyValuePair<bool, string>(false, "One clip"), new KeyValuePair<bool, string>(true, "A mix along an input") };
        return Fields.Column(
        [
            rows.Fields.Pick("Plays", line is null ? kinds[0].Value : kinds[1].Value, kinds, toMix => SwitchTo(toMix, line is not null)),
            line is null ? ClipRow() : LineRows(line),
        ]);
    }

    private void SwitchTo(bool toMix, bool isMix)
    {
        if (toMix == isMix)
        {
            return;
        }

        if (!toMix)
        {
            rows.Run("Play one clip", one => one.WithState(Here, was => was.CopyWith(plays: new BlendClip(BlendSourceModel.FirstClipOf(was.Plays)))));
            return;
        }

        // A blend with no input yet gets the one a mix is most often along.
        rows.Run("Mix clips", one =>
        {
            var input = one.Inputs.Keys.FirstOrDefault() ?? "speed";
            var withInput = one.Inputs.ContainsKey(input) ? one : one.WithInput(input, 0);
            return withInput.WithState(Here, was => was.CopyWith(plays: BlendSourceModel.LineFrom(BlendSourceModel.FirstClipOf(was.Plays), input)));
        });
    }

    private Control ClipRow()
    {
        var clip = state.Plays is BlendClip one ? one.Clip : "";
        return rows.Name("Clip", clip, clips, text => rows.Typed("Change clip", blendNow => blendNow.WithState(Here, was => was.CopyWith(plays: new BlendClip(text.Trim())))));
    }

    private StackPanel LineRows(BlendLine line)
    {
        var column = new List<Control>
        {
            rows.Name(
                "Along",
                line.Input,
                [.. blend.Inputs.Keys],
                text => rows.Typed("Change input", one => one.WithState(Here, OnLine(was => BlendSourceModel.LineAlong(was, text.Trim()))))),
        };
        for (var i = 0; i < line.Points.Count; i++)
        {
            var place = new PointPlace(i);
            column.Add(PointAt(line, place));
            column.Add(PointClip(line, place));
        }

        column.Add(rows.Act("Add a point", "Add a point after the last one.", () => rows.Run("Add a point", one => one.WithState(Here, OnLine(BlendSourceModel.PointAdded)))));
        return Fields.Column(column);
    }

    // Putting the points in order again moves a point while its boxes are still being typed in, so the boxes follow it.
    private sealed class PointPlace(int index)
    {
        public int Index { get; set; } = index;
    }

    private Control PointAt(BlendLine line, PointPlace place)
    {
        var at = place.Index;
        var take = line.Points.Count > 1
            ? rows.Remove($"remove_point_{at + 1}", "Take this point out", () => rows.Run("Remove a point", one => one.WithState(Here, OnLine(was => BlendSourceModel.PointRemoved(was, place.Index)))))
            : null;
        return rows.Number($"At {at + 1}", line.Points[at].At, number =>
        {
            rows.Typed("Move a point", one => one.WithState(Here, OnLine(was => BlendSourceModel.PointChanged(was, place.Index, at: number))));
            place.Index = Math.Max(0, IndexAt(number, place.Index));
        }, take);
    }

    private Control PointClip(BlendLine line, PointPlace place) => rows.Name(
        $"Clip {place.Index + 1}",
        line.Points[place.Index].Plays is BlendClip clip ? clip.Clip : "",
        clips,
        text => rows.Typed("Change clip", one => one.WithState(Here, OnLine(was => BlendSourceModel.PointChanged(was, place.Index, clip: text.Trim())))));

    // shortcut: two points at one position are told apart by their order only, so the box may follow the other one. Tell them apart by identity when a line can hold two there.
    private int IndexAt(double at, int fallback)
    {
        var line = rows.Bench.Blend?.WrittenStates.FirstOrDefault(one => one.Name == Here)?.Plays as BlendLine;
        var found = line?.Points.ToList().FindIndex(point => point.At == at) ?? -1;
        return found < 0 ? fallback : found;
    }

    private StackPanel PlaybackSection() => Fields.Column(
    [
        rows.Number(
            "Speed",
            state.Speed,
            number => rows.Typed("Change speed", one => one.WithState(Here, was => was.CopyWith(speed: number))),
            BlendRows.Unit("x")),
        rows.Fields.Pick(
            "At the end",
            Endings.First(one => one.Key == state.WhenDone).Value,
            Endings,
            ending => rows.Run("Change what happens at the end", one => one.WithState(Here, was => ending is { } done ? was.CopyWith(whenDone: done) : was.CopyWith(clearWhenDone: true)))),
        rows.Name(
            "Sync marks",
            string.Join(", ", state.Sync),
            [],
            text => rows.Typed("Change sync marks", one => one.WithState(Here, was => was.CopyWith(sync: Marks(text))))),
    ]);

    private StackPanel ChangesSection()
    {
        var column = new List<Control>();
        for (var i = 0; i < blend.WrittenChanges.Count; i++)
        {
            if (blend.WrittenChanges[i].From == state.Name || blend.WrittenChanges[i].To == state.Name)
            {
                column.Add(ChangeRow(i));
            }
        }

        var other = blend.WrittenStates.FirstOrDefault(one => one.Name != state.Name)?.Name;
        column.Add(rows.Act("Add a change", "Add a way out of this state into another.", () => AddChangeTo(other), other is not null));
        return Fields.Column(column);
    }

    private void AddChangeTo(string? other)
    {
        if (other is not null)
        {
            rows.Run("Add change", one => one.WithChangeAdded(Here, other), new ChangePicked(blend.WrittenChanges.Count));
        }
    }

    private Button ChangeRow(int index)
    {
        var change = blend.WrittenChanges[index];
        var button = new Button
        {
            Name = $"change_{index}",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 2),
            Content = new TextBlock
            {
                Name = $"change_text_{index}",
                Text = $"{change.From ?? "Any state"} to {change.To}, {BlendConditionModel.Describe(change.When)}",
                FontSize = OrblitText.Label,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxLines = 2,
            },
        };
        button.Click += (_, _) => rows.Fields.Host.Apply(() => rows.Bench.Pick = new ChangePicked(index));
        return button;
    }
}
