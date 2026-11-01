using Avalonia;
using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Panels;
using Orblit.Motion;

/// <summary>What the condition editor does with a condition it has made.</summary>
/// <param name="Chosen">A change that is a step by itself: a kind picked, a test added or taken out.</param>
/// <param name="Typed">A change that is one of a run of keystrokes.</param>
internal sealed record ConditionEdits(Action<BlendCondition> Chosen, Action<BlendCondition> Typed);

/// <summary>
/// The fields of one condition, and under a group the fields of each test it holds, set in from the
/// edge. A part's fields are numbered after it so that every field has a name of its own.
/// </summary>
/// <param name="rows">What the fields are made of.</param>
/// <param name="inputs">The inputs a test may read.</param>
/// <param name="edits">Where the condition goes when it has changed.</param>
internal sealed class BlendConditionEditor(BlendRows rows, IReadOnlyList<string> inputs, ConditionEdits edits)
{
    /// <param name="condition">What is shown.</param>
    /// <param name="tag">Where it sits in a group, such as <c> 1</c> for the first test or <c> 1.2</c> for the second in that one. Empty for the whole condition.</param>
    public Control Build(BlendCondition condition, string tag = "")
    {
        var kind = BlendConditionModel.KindOf(condition);
        var column = new List<Control> { KindPick(condition, kind, tag) };
        if (kind.ReadsInput())
        {
            column.Add(rows.Name($"Input{tag}", BlendConditionModel.InputOf(condition), inputs, name => edits.Typed(BlendConditionModel.Reading(condition, name.Trim()))));
        }

        if (kind is ConditionKind.Above or ConditionKind.Below)
        {
            column.Add(rows.Number($"Value{tag}", BlendConditionModel.NumberOf(condition), number => edits.Typed(BlendConditionModel.Numbered(condition, number))));
        }

        if (kind == ConditionKind.Through)
        {
            column.Add(rows.Number($"Laps{tag}", BlendConditionModel.NumberOf(condition), number => edits.Typed(BlendConditionModel.Numbered(condition, number)), BlendRows.Unit("laps")));
        }

        if (kind.IsComposite())
        {
            column.Add(Parts(condition, kind, tag));
        }

        return Fields.Column(column);
    }

    private Control KindPick(BlendCondition condition, ConditionKind kind, string tag) => rows.Fields.Pick(
        $"Test{tag}",
        kind.Label(),
        Enum.GetValues<ConditionKind>().Select(one => new KeyValuePair<ConditionKind, string>(one, one.Label())),
        chosen => edits.Chosen(BlendConditionModel.OfKind(chosen, condition)));

    private Border Parts(BlendCondition condition, ConditionKind kind, string tag)
    {
        var parts = BlendConditionModel.PartsOf(condition);
        var column = new List<Control>();
        for (var i = 0; i < parts.Count; i++)
        {
            var index = i;
            var partTag = tag.Length == 0 ? $" {i + 1}" : $"{tag}.{i + 1}";
            BlendCondition Replaced(BlendCondition part) => BlendConditionModel.MadeOf(condition, [.. parts.Select((one, j) => j == index ? part : one)]);
            var inside = new ConditionEdits(part => edits.Chosen(Replaced(part)), part => edits.Typed(Replaced(part)));
            column.Add(new BlendConditionEditor(rows, inputs, inside).Build(parts[i], partTag));
            if (kind != ConditionKind.Not)
            {
                column.Add(rows.Act(
                    $"Remove{partTag}",
                    "Take this test out of the group.",
                    () => edits.Chosen(BlendConditionModel.MadeOf(condition, [.. parts.Where((_, j) => j != index)]))));
            }
        }

        if (kind != ConditionKind.Not)
        {
            var added = BlendCondition.On(inputs.Count > 0 ? inputs[0] : "input");
            column.Add(rows.Act(
                $"Add a test{tag}",
                "Add another test to the group.",
                () => edits.Chosen(BlendConditionModel.MadeOf(condition, [.. parts, added]))));
        }

        // The edge says what is inside what, and a group inside a group is told from the one around it by it.
        return new Border
        {
            Margin = new Thickness(Space.Sm, 0, 0, 0),
            Padding = new Thickness(Space.Sm, 0, 0, 0),
            BorderBrush = OrblitColors.Line,
            BorderThickness = new Thickness(1, 0, 0, 0),
            Child = Fields.Column(column),
        };
    }
}
