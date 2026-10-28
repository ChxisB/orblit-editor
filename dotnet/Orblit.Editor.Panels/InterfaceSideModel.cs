namespace Orblit.Editor.Panels;

using System.Globalization;
using Orblit.Editor.Core;
using Orblit.Ui;

/// <summary>What the Interface workspace offers and says, apart from drawing it.</summary>
public static class InterfaceSideModel
{
    public const string NothingOpen = "No interface open";

    public const string NothingOpenAbout =
        "An interface is what the game shows over the scene, such as a menu, a score or a button. Make one here, or open one from Project.";

    public const string NewTip = "Create a screen to lay out menus and controls.";

    public const string NoElements = "The elements of an open interface show here. Choose New interface on the canvas.";

    public const string NoDesign = "Choose New interface on the canvas to add and style its elements.";

    public const string NarrowTip =
        "Whether this turns into a column on a screen narrower than the md breakpoint. Written as the classes col md:row, so it can be changed by hand as well.";

    public const string ReloadTip = "Read the files in strings/ again.";

    public const string NoLanguages = "One file in strings/ for each language, such as fr.json.";

    /// <summary>
    /// Sizes worth having one press away. Every one is a real screen somebody ships to, rather than a
    /// round number.
    /// </summary>
    public static IReadOnlyList<(double Width, double Height)> Sizes { get; } =
        [(1920, 1080), (2560, 1440), (1280, 720), (390, 844), (1024, 768)];

    /// <summary>
    /// Screens worth one press. Every one is a real device, and between them they cross every
    /// breakpoint there is. Null is the size the canvas was drawn against.
    /// </summary>
    public static IReadOnlyList<(string Label, (double Width, double Height)? Size)> Devices { get; } =
    [
        ("Canvas", null),
        ("Phone", (390, 844)),
        ("Tablet", (834, 1112)),
        ("Laptop", (1440, 900)),
        ("Desktop", (1920, 1080)),
        ("TV", (3840, 2160)),
    ];

    /// <summary>What a fit does, since its name is a few words and the behaviour is what somebody is choosing.</summary>
    public static string Explain(CanvasFit fit) => fit switch
    {
        CanvasFit.Responsive => "Laid out at whatever size the screen is. Prefixed classes decide what changes, and text and spacing grow with the screen instead of the whole picture being magnified.",
        CanvasFit.Width => "The width always fills the screen. The bottom of a taller screen is empty and a shorter one cuts the bottom off.",
        CanvasFit.Height => "The height always fits. A wider screen has space at the sides and a narrower one cuts them off.",
        CanvasFit.Contain => "All of it fits, with space where the shape does not match. Nothing is ever cut off.",
        _ => "Not scaled. Pixels are pixels, however big the screen is.",
    };

    public static string Size((double Width, double Height) size) =>
        string.Create(CultureInfo.InvariantCulture, $"{Math.Round(size.Width)} × {Math.Round(size.Height)}");

    /// <summary>The widest breakpoint in effect at <paramref name="width"/>, or base below them all.</summary>
    public static string Breakpoint(double width) => new UiBreakpoints().LabelAt(width);

    public static string ShowingTip((double Width, double Height) laidOut) =>
        $"Laid out at {Size(laidOut)}. Classes prefixed {Breakpoint(laidOut.Width)}: and narrower are in effect.";

    public static string DeviceTip((double Width, double Height)? size) =>
        size is { } upright ? $"{Size(upright)} upright" : "The size this was drawn against.";

    /// <summary>
    /// How many columns the grid draws at <paramref name="width"/>. Said out loud when it is fewer than
    /// authored, because a grid that quietly changed its mind is one somebody mistakes for their layout.
    /// </summary>
    public static string GridCount(UiCanvas canvas, double width)
    {
        var drawn = canvas.ColumnsAt(width);
        var authored = canvas.Columns;
        return drawn == authored ? $"{authored} across this screen"
            : drawn == 0 ? "No room for a column at this width"
            : $"{drawn} across this screen. {authored} is too fine here.";
    }

    /// <summary>
    /// Whether there is anything to say about languages: the interface asks for words by key, or the
    /// project has language files, good or bad. A project in one language has no use for the section.
    /// </summary>
    public static bool SpeaksMore(InterfaceBench bench) =>
        bench.Strings.Locales.Count > 0 || bench.StringProblems.Count > 0 || bench.Document?.Root.TextKeys.Count > 0;

    /// <summary>
    /// Whether a split means anything for <paramref name="node"/>. Splitting a piece of text into
    /// three columns is not a layout.
    /// </summary>
    public static bool IsContainer(UiNode node) => node.Type is "column" or "row" or "stack" or "box";

    /// <summary>Whether a container turns into a column on a narrow screen.</summary>
    public static bool Stacks(UiNode node) =>
        node.Classes.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Contains("md:row");

    /// <summary>The classes of <paramref name="node"/> with stacking on a narrow screen turned the other way.</summary>
    public static string Narrowed(UiNode node)
    {
        var flowing = UiElements.Flowing(node.Classes);
        return Stacks(node) ? flowing : $"{flowing} col md:row".Trim();
    }

    /// <summary><paramref name="node"/> with the prop <paramref name="name"/> set, or taken out when <paramref name="value"/> is empty.</summary>
    public static UiNode WithProp(UiNode node, string name, string value)
    {
        var props = new OrderedDictionary<string, object?>();
        foreach (var (key, held) in node.Props.Where(one => one.Key != name))
        {
            props[key] = held;
        }

        if (value.Length > 0)
        {
            props[name] = value;
        }

        return node with { Props = props };
    }
}
