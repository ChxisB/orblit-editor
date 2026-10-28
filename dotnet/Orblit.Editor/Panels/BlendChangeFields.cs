using Avalonia;
using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;
using Orblit.Sequence;

/// <summary>The fields of one change: where it goes, in what order it is tried, when it holds, and how it fades.</summary>
/// <param name="rows">What the fields are made of.</param>
/// <param name="blend">The blend the change is in.</param>
/// <param name="index">Where the change stands among the blend's changes.</param>
internal sealed class BlendChangeFields(BlendRows rows, BlendDocument blend, int index)
{
    private static readonly KeyValuePair<Easing, string>[] Shapes =
    [
        new(Easing.Linear, "Even"),
        new(Easing.In, "Ease in"),
        new(Easing.Out, "Ease out"),
        new(Easing.InOut, "Ease in and out"),
        new(Easing.Smooth, "Smooth"),
        new(Easing.Back, "Overshoot"),
        new(Easing.Bounce, "Bounce"),
    ];

    private BlendChange Change => blend.WrittenChanges[index];

    public Control Build() => Fields.Column(
    [
        rows.Fields.Section("Change", ChangeSection()),
        rows.Fields.Section("When", WhenSection()),
        rows.Fields.Section("Fade", FadeSection()),
    ]);

    private void Edit(string label, Func<BlendChange, BlendChange> change, BlendPick? pick = null) =>
        rows.Run(label, one => one.WithChange(index, change), pick);

    private StackPanel ChangeSection()
    {
        var to = blend.WrittenStates.Select(static one => new KeyValuePair<string, string>(one.Name, one.Name)).ToList();
        var from = new List<KeyValuePair<string, string>> { new(string.Empty, "Any state") };
        from.AddRange(to);
        var order = Fields.Note($"Number {index + 1} of {blend.WrittenChanges.Count}. The first that holds is the one taken.");
        order.Name = "change_order";
        return Fields.Column(
        [
            rows.Fields.Pick(
                "From",
                Change.From ?? "Any state",
                from,
                name => Edit("Change where it leaves", one => name.Length == 0 ? one.CopyWith(clearFrom: true) : one.CopyWith(from: name))),
            rows.Fields.Pick("To", Change.To, to, name => Edit("Change where it goes", one => one.CopyWith(to: name))),
            order,
            Order(),
        ]);
    }

    private Grid Order()
    {
        var last = blend.WrittenChanges.Count - 1;
        var buttons = new[]
        {
            rows.Act("Earlier", "Try this change sooner.", () => rows.Run("Move change earlier", one => one.WithChangeMoved(index, -1), new ChangePicked(index - 1)), index > 0),
            rows.Act("Later", "Try this change later.", () => rows.Run("Move change later", one => one.WithChangeMoved(index, 1), new ChangePicked(index + 1)), index < last),
            rows.Act("Delete", "Take this change out.", () => rows.Run("Delete change", one => one.WithChangeRemoved(index), BlendPick.Nothing)),
        };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), Margin = new Thickness(0, 6) };
        for (var i = 0; i < buttons.Length; i++)
        {
            Grid.SetColumn(buttons[i], i);
            buttons[i].Margin = new Thickness(i == 0 ? 0 : Space.Xs, 0, 0, 0);
            grid.Children.Add(buttons[i]);
        }

        return grid;
    }

    private Control WhenSection()
    {
        var edits = new ConditionEdits(
            when => Edit("Change condition", one => one.CopyWith(when: when)),
            when => rows.Typed("Change condition", one => one.WithChange(index, was => was.CopyWith(when: when))));
        return new BlendConditionEditor(rows, [.. blend.Inputs.Keys], edits).Build(Change.When);
    }

    private StackPanel FadeSection() => Fields.Column(
    [
        rows.Number(
            "Length",
            Change.Fade,
            seconds => rows.Typed("Change fade", one => one.WithChange(index, was => was.CopyWith(fade: Math.Max(0, seconds)))),
            BlendRows.Unit("s")),
        rows.Fields.Pick(
            "Shape",
            Shapes.First(one => one.Key == Change.Shape).Value,
            Shapes,
            shape => Edit("Change fade shape", one => one.CopyWith(shape: shape))),
        rows.Fields.Toggle("Keep step", Change.InStep, on => Edit("Change keep step", one => one.CopyWith(inStep: on))),
        rows.Fields.Toggle("Freeze pose", Change.FromPose, on => Edit("Change freeze pose", one => one.CopyWith(fromPose: on))),
    ]);
}
