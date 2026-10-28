using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Mesh;
using Orblit.VectorMath;

/// <summary>
/// The rule's own numbers, for a face that is still following one: how its texture fills it and where
/// it starts.
/// </summary>
internal static class UvRules
{
    private static readonly UvFit[] Fills = [UvFit.Tile, UvFit.Stretch, UvFit.Fit];

    private static readonly SliderOptions Repeats = new() { Min = 0.05, Max = 8, Decimals = 2 };
    private static readonly SliderOptions Moves = new() { Min = -2, Max = 2, Decimals = 2 };

    /// <param name="fields">Where the rows send their changes.</param>
    /// <param name="uv">The rule as it stands.</param>
    /// <param name="onChanged">Called with the rule it should become. Live while a slider is held.</param>
    public static Control Build(Fields fields, FaceUv uv, Action<FaceUv, bool> onChanged) => Fields.Column(
    [
        fields.Choice("Fill", [.. Fills.Select(fill => fill.ToString())], uv.Fit.ToString(), picked =>
            onChanged(uv.CopyWith(fit: Enum.Parse<UvFit>(picked)), false)),
        fields.Slider("Repeat across", uv.Scale.X, value => onChanged(uv.CopyWith(scale: new Vector2(value, uv.Scale.Y)), true), Repeats),
        fields.Slider("Repeat down", uv.Scale.Y, value => onChanged(uv.CopyWith(scale: new Vector2(uv.Scale.X, value)), true), Repeats),
        fields.Slider("Turn", uv.Rotation, value => onChanged(uv.CopyWith(rotation: value), true), new SliderOptions { Min = -180, Max = 180 }),
        fields.Slider("Across", uv.Offset.X, value => onChanged(uv.CopyWith(offset: new Vector2(value, uv.Offset.Y)), true), Moves),
        fields.Slider("Down", uv.Offset.Y, value => onChanged(uv.CopyWith(offset: new Vector2(uv.Offset.X, value)), true), Moves),
        fields.Flags("Flip", [("Across", uv.FlipU), ("Down", uv.FlipV), ("Swap", uv.Swap)], cell =>
            onChanged(Flipped(uv, cell), false)),
    ]);

    private static FaceUv Flipped(FaceUv uv, int cell) => cell switch
    {
        0 => uv.CopyWith(flipU: !uv.FlipU),
        1 => uv.CopyWith(flipV: !uv.FlipV),
        _ => uv.CopyWith(swap: !uv.Swap),
    };
}
