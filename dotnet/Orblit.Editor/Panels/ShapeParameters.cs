namespace Orblit.Editor;

using Orblit.Mesh;

/// <summary>One control a kind of shape has, read from a shape and written back as a new one.</summary>
internal abstract record ShapeRow(string Label);

/// <param name="Read">The number as the shape holds it.</param>
/// <param name="Write">The shape with the number changed. A whole number is rounded here.</param>
/// <param name="Options">The range, and how many decimals show.</param>
internal sealed record ShapeNumber(
    string Label,
    Func<Shape, double> Read,
    Func<Shape, double, Shape> Write,
    SliderOptions Options) : ShapeRow(Label);

/// <param name="Read">Whether the flag is on.</param>
/// <param name="Write">The shape with the flag set.</param>
/// <param name="Names">What the flag reads as when it is off and when it is on. Off and On when null.</param>
internal sealed record ShapeFlag(
    string Label,
    Func<Shape, bool> Read,
    Func<Shape, bool, Shape> Write,
    string[]? Names = null) : ShapeRow(Label);

/// <summary>
/// The controls that belong to each kind of shape and no other. A cylinder has sides and a torus has a
/// tube radius, and showing every field for every kind is twenty controls of which two matter.
/// </summary>
internal static class ShapeParameters
{
    private static readonly SliderOptions Size = new() { Min = 0.05, Max = 20, Decimals = 2 };
    private static readonly SliderOptions Sides = new() { Min = 3, Max = 64 };
    private static readonly SliderOptions Cuts = new() { Min = 0, Max = 24 };
    private static readonly SliderOptions Wall = new() { Min = 0.01, Max = 2, Decimals = 2 };
    private static readonly SliderOptions Sweep = new() { Min = 1, Max = 360 };
    private static readonly SliderOptions Piece = new() { Min = 0.05, Max = 4, Decimals = 2 };

    // Size first: it is what somebody reaches for, and it means the same thing for every kind.
    private static readonly ShapeRow[] SizeRows =
    [
        new ShapeNumber("Width", shape => shape.Width, (shape, value) => shape with { Width = value }, Size),
        new ShapeNumber("Height", shape => shape.Height, (shape, value) => shape with { Height = value }, Size),
        new ShapeNumber("Depth", shape => shape.Depth, (shape, value) => shape with { Depth = value }, Size),
    ];

    private static readonly ShapeNumber HeightCuts = new("Height cuts", shape => shape.HeightCuts, (shape, value) => shape with { HeightCuts = Whole(value) }, Cuts);
    private static readonly ShapeNumber Thickness = new("Thickness", shape => shape.Thickness, (shape, value) => shape with { Thickness = value }, Wall);
    private static readonly ShapeNumber Circumference = new("Sweep", shape => shape.Circumference, (shape, value) => shape with { Circumference = value }, Sweep);
    private static readonly ShapeFlag Smooth = new("Smooth", shape => shape.Smooth, (shape, on) => shape with { Smooth = on });

    private static readonly ShapeNumber[] StairsCount =
    [
        new("Steps", shape => shape.Steps, (shape, value) => shape with { Steps = Whole(value) }, new SliderOptions { Min = 1, Max = 64 }),
    ];

    private static readonly ShapeNumber[] StairsHeight =
    [
        new("Step height", shape => shape.StepHeight, (shape, value) => shape with { StepHeight = value }, new SliderOptions { Min = 0.05, Max = 1, Decimals = 2 }),
    ];

    private static readonly Dictionary<ShapeKind, ShapeRow[]> ByKind = new()
    {
        [ShapeKind.Plane] =
        [
            new ShapeNumber("Cuts across", shape => shape.WidthCuts, (shape, value) => shape with { WidthCuts = Whole(value) }, Cuts),
            new ShapeNumber("Cuts along", shape => shape.HeightCuts, (shape, value) => shape with { HeightCuts = Whole(value) }, Cuts),
        ],
        [ShapeKind.Sphere] =
        [
            new ShapeNumber("Divisions", shape => shape.Subdivisions, (shape, value) => shape with { Subdivisions = Whole(value) }, new SliderOptions { Min = 1, Max = 5 }),
            Smooth,
        ],
        [ShapeKind.Cylinder] = [SidesRow("Sides"), HeightCuts, Capped("Ends"), Smooth],
        [ShapeKind.Cone] = [SidesRow("Sides"), Capped("Base"), Smooth],
        [ShapeKind.Pipe] = [Thickness, SidesRow("Sides"), HeightCuts, Capped("Rims")],
        [ShapeKind.Torus] =
        [
            new ShapeNumber("Rows", shape => shape.Rings, (shape, value) => shape with { Rings = Whole(value) }, Sides),
            new ShapeNumber("Columns", shape => shape.Columns, (shape, value) => shape with { Columns = Whole(value) }, Sides),
            new ShapeNumber("Tube", shape => shape.TubeRadius, (shape, value) => shape with { TubeRadius = value }, Wall),
            Circumference,
        ],
        [ShapeKind.Arch] =
        [
            Thickness,
            new ShapeNumber("Segments", shape => shape.Sides, (shape, value) => shape with { Sides = Whole(value) }, new SliderOptions { Min = 2, Max = 64 }),
            Circumference,
            Capped("End caps"),
        ],
        [ShapeKind.Door] =
        [
            new ShapeNumber("Opening top", shape => shape.PedimentHeight, (shape, value) => shape with { PedimentHeight = value }, Piece),
            new ShapeNumber("Side width", shape => shape.SideWidth, (shape, value) => shape with { SideWidth = value }, Piece),
        ],
    };

    private static readonly ShapeFlag StairsBy = new(
        "Steps by",
        shape => shape.ByCount,
        (shape, byCount) => shape with { ByCount = byCount },
        ["Height", "Count"]);

    /// <summary>The controls for <paramref name="shape"/>, from its size down.</summary>
    public static IEnumerable<ShapeRow> For(Shape shape) => [.. SizeRows, .. OfKind(shape)];

    // A staircase shows the field that its own choice makes true, and the other would do nothing.
    private static ShapeRow[] OfKind(Shape shape) => shape.Kind == ShapeKind.Stairs
        ? [StairsBy, .. shape.ByCount ? StairsCount : StairsHeight]
        : ByKind.GetValueOrDefault(shape.Kind, []);

    private static ShapeNumber SidesRow(string label) =>
        new(label, shape => shape.Sides, (shape, value) => shape with { Sides = Whole(value) }, Sides);

    private static ShapeFlag Capped(string label) =>
        new(label, shape => shape.Capped, (shape, on) => shape with { Capped = on });

    private static int Whole(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
