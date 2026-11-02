namespace Orblit.Editor.Core;

/// <summary>The kind of screen the game view pretends to be.</summary>
public enum DeviceKind
{
    Desktop,
    Tablet,
    Mobile,
}

/// <summary>Which way a device is held.</summary>
public enum FrameOrientation
{
    Portrait,
    Landscape,
}

/// <summary>
/// The strips of a screen a game keeps clear of: a notch, a rounded corner or a home bar. Whole pixels
/// of the device's own size.
/// </summary>
public readonly record struct UnsafeEdges(int Left, int Top, int Right, int Bottom)
{
    public static UnsafeEdges None => default;
}

/// <summary>
/// A device and the way it is held, which says how big the game view's picture is and where its unsafe
/// edges are.
/// </summary>
/// <remarks>
/// A device is a way of looking at the game and is not part of it, so nothing here reaches the scene.
/// </remarks>
public sealed record DeviceFrame
{
    private sealed record Row(DeviceKind Kind, FrameOrientation Orientation, int Width, int Height, UnsafeEdges Edges);

    // F2: a list of cases is data. The sizes are those of the retired bench. The edges are those of one
    // current phone and are meant to be changed.
    private static readonly Row[] Rows =
    [
        new(DeviceKind.Desktop, FrameOrientation.Landscape, 1920, 1080, UnsafeEdges.None),
        new(DeviceKind.Tablet, FrameOrientation.Portrait, 834, 1112, UnsafeEdges.None),
        new(DeviceKind.Tablet, FrameOrientation.Landscape, 1112, 834, UnsafeEdges.None),
        new(DeviceKind.Mobile, FrameOrientation.Portrait, 390, 844, new UnsafeEdges(0, 47, 0, 34)),
        new(DeviceKind.Mobile, FrameOrientation.Landscape, 844, 390, new UnsafeEdges(47, 0, 47, 21)),
    ];

    private readonly Row _row;

    /// <param name="kind">The device.</param>
    /// <param name="orientation">The way it is held. A desktop is always landscape, so the answer is ignored for one.</param>
    public DeviceFrame(DeviceKind kind, FrameOrientation orientation)
    {
        var held = kind == DeviceKind.Desktop ? FrameOrientation.Landscape : orientation;
        _row = Rows.Single(row => row.Kind == kind && row.Orientation == held);
    }

    /// <summary>The five frames, in the order the bar offers them.</summary>
    public static IReadOnlyList<DeviceFrame> All { get; } = [.. Rows.Select(row => new DeviceFrame(row.Kind, row.Orientation))];

    public static DeviceFrame Desktop { get; } = new(DeviceKind.Desktop, FrameOrientation.Landscape);

    public DeviceKind Kind => _row.Kind;

    public FrameOrientation Orientation => _row.Orientation;

    public int Width => _row.Width;

    public int Height => _row.Height;

    public UnsafeEdges Edges => _row.Edges;

    /// <summary>False for a desktop, which has one shape.</summary>
    public bool CanTurn => Kind != DeviceKind.Desktop;

    /// <summary>The size the way a person reads it, such as <c>390 × 844</c>.</summary>
    public string Size => $"{Width} × {Height}";

    /// <summary>This device held the other way, or this frame when it cannot turn.</summary>
    public DeviceFrame Turned() => !CanTurn
        ? this
        : new DeviceFrame(Kind, Orientation == FrameOrientation.Portrait ? FrameOrientation.Landscape : FrameOrientation.Portrait);

    /// <summary>
    /// How much of its own size the frame is drawn at inside a room of the given size: all of it when
    /// it fits, and never more, so the picture is not magnified past the pixels a device would show.
    /// </summary>
    public double FitInto(double roomWidth, double roomHeight) =>
        Math.Max(0, Math.Min(1, Math.Min(roomWidth / Width, roomHeight / Height)));
}
