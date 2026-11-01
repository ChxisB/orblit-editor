namespace Orblit.Editor.Core;

using Orblit.Terrain;

/// <summary>What a tool is called, what it does and which icon it has on the shelf.</summary>
/// <param name="Label">The name on the shelf and next to Undo.</param>
/// <param name="Hint">What it does, and what it does with Shift or ⌘ held.</param>
/// <param name="Icon">The name of its icon.</param>
public sealed record ToolWords(string Label, string Hint, string Icon);

/// <summary>The words and icons of the terrain brush's tools.</summary>
public static class BrushWords
{
    private static readonly Dictionary<BrushTool, ToolWords> Words = new()
    {
        [BrushTool.Raise] = new("Raise", "Lifts the ground. Shift lowers it.", "arrow_upward"),
        [BrushTool.Lower] = new("Lower", "Sinks the ground. Shift raises it.", "arrow_downward"),
        [BrushTool.Smooth] = new("Smooth", "Evens out the bumps.", "blur_on"),
        [BrushTool.Flatten] = new("Flatten", "Draws the ground to the height the stroke began at.", "horizontal_rule"),
        [BrushTool.Slope] = new(
            "Slope",
            "Drag from one end of the ramp to the other, then back over it: the ramp runs between the ground at the two ends.",
            "trending_up"),
        [BrushTool.Cover] = new(
            "Paint cover",
            "Lays the chosen set. Shift hands the ground back to automatic cover.",
            "texture"),
        [BrushTool.Colour] = new("Paint colour", "Tints the ground. Shift washes the tint out.", "format_paint_outlined"),
        [BrushTool.Roughness] = new(
            "Paint roughness",
            "Makes the ground rougher or glossier. Shift takes it back off.",
            "grain"),
        [BrushTool.Hole] = new("Punch holes", "Cuts holes. Shift fills them.", "adjust"),
    };

    public static ToolWords Of(BrushTool tool) => Words[tool];

    /// <summary>What a stroke is called next to Undo.</summary>
    public static string StrokeLabel(BrushTool tool, string terrain) => tool switch
    {
        BrushTool.Cover or BrushTool.Colour or BrushTool.Roughness => $"{Of(tool).Label} on {terrain}",
        BrushTool.Hole => $"Punch holes in {terrain}",
        _ => $"{Of(tool).Label} {terrain}",
    };
}
