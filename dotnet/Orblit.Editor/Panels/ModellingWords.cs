namespace Orblit.Editor;

using System.Diagnostics;
using Orblit.Editor.Core;
using Orblit.Editor.Viewport;

/// <summary>What the modelling panel says that is not a label.</summary>
internal static class ModellingWords
{
    public const string NothingSelected = "Select a shape to work on it, or draw one.";

    public const string NoMaterials = "One material, the renderer's own. Add a slot to paint faces with something else.";

    public static string NotAShape(string name) =>
        $"{name} is not a shape, so it has no parts to change here. Draw a shape, or add one from the Add menu.";

    public static string SnapTarget(SnapTo to, double step) => $"{ViewportChips.GridLabel(step)} squares. " + to switch
    {
        SnapTo.Pivot => "Wherever the shape's own origin is.",
        SnapTo.Base => "Its underside, so it stands on the line rather than through it.",
        SnapTo.Centre => "Its middle, so it straddles the line.",
        SnapTo.Top => "Its top, for hanging it from something.",
        _ => throw new UnreachableException(),
    };

    public static string Drawing(int points) =>
        $"{points} {(points == 1 ? "point" : "points")}. Enter finishes, backspace takes one back, escape gives up.";

    public static string Parts(ElementMode mode, int selected) => selected == 0
        ? $"Click {mode.Label().ToLowerInvariant()} in the viewport. Shift adds. Escape leaves."
        : $"{selected} selected. Drag the handles to move them{(mode == ElementMode.Face ? ", shift-drag to extrude" : "")}.";

    public static string Used(int faces) => faces == 0 ? "unused" : $"{faces}";
}
