namespace Orblit.Editor.Panels;

using Orblit.Motion;

/// <summary>Where a box of the blend graph is, in pixels from the corner of the canvas.</summary>
public readonly record struct SketchBox(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;

    public Spot CentreLeft => new(Left, Top + (Height / 2));

    public Spot CentreRight => new(Right, Top + (Height / 2));

    public Spot BottomCentre => new(Left + (Width / 2), Bottom);

    public Spot BottomLeft => new(Left, Bottom);

    public bool Contains(Spot at) => at.X >= Left && at.X <= Right && at.Y >= Top && at.Y <= Bottom;
}

/// <summary>One curve, from where it leaves a box to where it arrives at another.</summary>
public sealed record SketchCurve(Spot Start, Spot PullStart, Spot PullEnd, Spot End)
{
    /// <summary>The way the curve is heading as it arrives, in radians from the right.</summary>
    public double Heading => Math.Atan2(End.Y - PullEnd.Y, End.X - PullEnd.X);

    /// <summary>The point <paramref name="t"/> of the way along, from nought at the start to one at the end.</summary>
    public Spot At(double t)
    {
        var u = 1 - t;
        var a = u * u * u;
        var b = 3 * u * u * t;
        var c = 3 * u * t * t;
        var d = t * t * t;
        return new Spot(
            (Start.X * a) + (PullStart.X * b) + (PullEnd.X * c) + (End.X * d),
            (Start.Y * a) + (PullStart.Y * b) + (PullEnd.Y * c) + (End.Y * d));
    }
}

/// <summary>One arrow, and the changes it stands for in order of precedence.</summary>
/// <param name="From">The box it leaves.</param>
/// <param name="To">The box it enters.</param>
/// <param name="Changes">Indexes into <see cref="BlendDocument.WrittenChanges"/>.</param>
public sealed record SketchArrow(SketchBox From, SketchBox To, IReadOnlyList<int> Changes)
{
    /// <summary>How far under a box a loop's label sits.</summary>
    private const double LoopDrop = 14;

    /// <summary>How far under the boxes an arrow that goes back runs, as a share of the gap between rows.</summary>
    private const double Dip = 0.9;

    public bool IsLoop => From == To;

    /// <summary>
    /// Forwards across the gap between columns, and underneath both boxes when the change goes back or
    /// stays in a column, so it never crosses a box.
    /// </summary>
    public SketchCurve Curve
    {
        get
        {
            if (To.Left > From.Right)
            {
                var (a, b) = (From.CentreRight, To.CentreLeft);
                var pull = (b.X - a.X) / 2;
                return new SketchCurve(a, new Spot(a.X + pull, a.Y), new Spot(b.X - pull, b.Y), b);
            }

            var (start, end) = (From.BottomCentre, To.BottomCentre);
            var low = Math.Max(start.Y, end.Y) + (BlendSketch.GapY * Dip);
            return new SketchCurve(start, new Spot(start.X, low), new Spot(end.X, low), end);
        }
    }

    /// <summary>Where the arrow's label is centred: under the box for a loop, half way along otherwise.</summary>
    public Spot Anchor => IsLoop ? new Spot(From.Left, From.Bottom + LoopDrop) : Curve.At(0.5);
}

/// <summary>
/// Where everything in a blend is drawn: a box for each state, an arrow for each pair of states a change
/// joins, and a box for what every state can be left from, when a change is.
/// </summary>
/// <remarks>
/// Worked out from the blend and not stored, as the layout is. Nothing here reads a control.
/// </remarks>
/// <param name="States">Each state's box, in the order the layout gives them.</param>
/// <param name="Anywhere">The box that stands for every state, or null when no change is from there.</param>
/// <param name="Arrows">Every arrow, the ones from the state boxes first.</param>
/// <param name="Width">How wide the canvas must be.</param>
/// <param name="Height">How tall the canvas must be.</param>
public sealed record BlendSketch(
    IReadOnlyDictionary<string, SketchBox> States,
    SketchBox? Anywhere,
    IReadOnlyList<SketchArrow> Arrows,
    double Width,
    double Height)
{
    public const double NodeWidth = 156;
    public const double NodeHeight = 52;
    public const double GapX = 92;
    public const double GapY = 40;
    public const double Margin = 28;

    public static BlendSketch Of(BlendDocument blend)
    {
        var fromAnywhere = BlendGraphModel.ChangesFromAnywhere(blend);
        // The box for every state takes the first column, so what is started from it is read left to right like everything else.
        var shift = fromAnywhere.Count == 0 ? 0 : 1;
        SketchBox Cell(int column, int row) => new(
            Margin + ((column + shift) * (NodeWidth + GapX)),
            Margin + (row * (NodeHeight + GapY)),
            NodeWidth,
            NodeHeight);
        var states = new OrderedDictionary<string, SketchBox>();
        foreach (var (name, cell) in BlendGraphModel.Layout(blend))
        {
            states[name] = Cell(cell.Column, cell.Row);
        }

        SketchBox? hub = fromAnywhere.Count == 0 ? null : Cell(-1, 0);
        var boxes = hub is { } any ? [.. states.Values, any] : states.Values.ToList();
        return new BlendSketch(
            states,
            hub,
            ArrowsOf(blend, states, hub, fromAnywhere),
            boxes.Max(static box => box.Right) + Margin,
            boxes.Max(static box => box.Bottom) + Margin + GapY);
    }

    /// <summary>The next of the <paramref name="changes"/> an arrow stands for after <paramref name="picked"/>, or the first when it is none of them.</summary>
    public static int ChangeAfter(IReadOnlyList<int> changes, int picked) =>
        changes[(changes.ToList().IndexOf(picked) + 1) % changes.Count];

    /// <summary>The state whose box holds <paramref name="at"/>, or null for a spot that is on none.</summary>
    public string? StateAt(Spot at)
    {
        foreach (var (name, box) in States)
        {
            if (box.Contains(at))
            {
                return name;
            }
        }

        return null;
    }

    // The graph model only names states the blend has, and the layout places every one of them.
    private static List<SketchArrow> ArrowsOf(
        BlendDocument blend,
        OrderedDictionary<string, SketchBox> states,
        SketchBox? hub,
        IReadOnlyDictionary<string, IReadOnlyList<int>> fromAnywhere)
    {
        var arrows = BlendGraphModel.Edges(blend)
            .Select(edge => new SketchArrow(states[edge.From], states[edge.To], edge.Changes))
            .ToList();
        if (hub is { } any)
        {
            arrows.AddRange(fromAnywhere.Select(pair => new SketchArrow(any, states[pair.Key], pair.Value)));
        }

        return arrows;
    }
}
