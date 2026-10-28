using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;

/// <summary>What the inspector shows when nothing in the blend is chosen: the blend itself, its states and its inputs.</summary>
/// <param name="rows">What the fields are made of.</param>
/// <param name="blend">The blend shown.</param>
/// <param name="clips">The clips a new state could play.</param>
internal sealed class BlendOverviewFields(BlendRows rows, BlendDocument blend, IReadOnlyList<string> clips)
{
    public Control Build() => Fields.Column(
    [
        rows.Fields.Section("Blend", BlendSection()),
        rows.Fields.Section("States", rows.Add("New state", "The clip it plays", clips, AddState)),
        rows.Fields.Section("Inputs", InputsSection()),
    ]);

    private static string Counted(int count, string thing) => $"{count} {thing}{(count == 1 ? "" : "s")}";

    private StackPanel BlendSection()
    {
        var summary = Fields.Note(
            $"{Counted(blend.WrittenStates.Count, "state")} and {Counted(blend.WrittenChanges.Count, "change")}. "
            + "Choose a state or an arrow to edit it, and drag from a state onto another to join them.");
        summary.Name = "blend_summary";
        return Fields.Column(
        [
            rows.Name("Name", blend.Name, [], text => rows.Typed("Rename blend", one => one.WithName(text.Trim()))),
            rows.Fields.Pick(
                "Starts in",
                blend.WrittenStart,
                blend.WrittenStates.Select(static one => new KeyValuePair<string, string>(one.Name, one.Name)),
                name => rows.Run("Set start state", one => one.WithStart(name))),
            summary,
        ]);
    }

    private void AddState(string text)
    {
        var clip = text.Length > 0 ? text : clips.Count > 0 ? clips[0] : "clip";
        rows.Run("Add state", one => one.WithStateAdded(clip), new StatePicked(blend.UniqueStateName(clip)));
    }

    private StackPanel InputsSection()
    {
        var column = new List<Control>();
        foreach (var (name, value) in blend.Inputs)
        {
            var input = name;
            column.Add(rows.Number(
                input,
                value,
                number => rows.Typed($"Set input {input}", one => one.WithInput(input, number)),
                rows.Remove($"remove_input_{Fields.Key(input)}", $"Take out {input}", () => rows.Run($"Remove input {input}", one => one.WithInputRemoved(input)))));
        }

        column.Add(rows.Add("New input", "A name to read", [], text => rows.Run("Add input", one => one.WithInputAdded(text))));
        return Fields.Column(column);
    }
}
