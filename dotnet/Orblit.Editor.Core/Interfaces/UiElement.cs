namespace Orblit.Editor.Core;

using System.Text.RegularExpressions;
using Orblit.Ui;

/// <summary>
/// An element somebody can put on a canvas.
/// </summary>
/// <remarks>
/// A short list on purpose. Every one of these is a real control with real layout behind it, and a
/// palette of forty things that mostly wrap each other is a palette nobody reads to the end of.
/// </remarks>
/// <param name="Label">What the palette calls it.</param>
/// <param name="Type">What a file writes it as.</param>
/// <param name="Classes">
/// What it looks like the moment it is added. Not nothing: an element with no styling is an invisible
/// element, and an invisible element added to a canvas reads as a button that did not work.
/// </param>
public sealed record UiElement(string Label, string Type, string Classes)
{
    public static UiElement Column { get; } = new("Column", "column", "gap-2");

    public static UiElement Row { get; } = new("Row", "row", "gap-2 items-center");

    public static UiElement Stack { get; } = new("Stack", "stack", "");

    public static UiElement Box { get; } = new("Box", "box", "p-4 bg-slate-800 rounded-lg");

    public static UiElement Text { get; } = new("Text", "text", "text-base text-slate-100");

    public static UiElement Button { get; } = new("Button", "button", "px-4 py-2 bg-ember-500 text-white rounded");

    public static UiElement Field { get; } = new("Field", "field", "px-3 py-2 bg-slate-900 rounded");

    public static UiElement Image { get; } = new("Image", "image", "w-32 h-32");

    public static UiElement Spacer { get; } = new("Spacer", "spacer", "");

    /// <summary>Every element, in the order the palette shows them.</summary>
    public static IReadOnlyList<UiElement> All { get; } = [Column, Row, Stack, Box, Text, Button, Field, Image, Spacer];

    public UiNode Make() => new()
    {
        Type = Type,
        Classes = Classes,
        Text = Type switch
        {
            "text" => "Text",
            "button" => "Button",
            "field" => "",
            _ => null,
        },
    };
}

/// <summary>What the Interface workspace says about an element and its classes.</summary>
public static partial class UiElements
{
    private static readonly HashSet<string> Directions = ["col", "column", "stack", "row"];

    /// <summary>
    /// Whether <paramref name="node"/> holds other elements. Splitting a piece of text into three
    /// columns is not a layout, and nothing goes inside a button.
    /// </summary>
    public static bool IsContainer(UiNode node) => node.Type is "column" or "row" or "stack" or "box";

    /// <summary>
    /// What an element is called in the tree and next to Undo.
    /// </summary>
    /// <remarks>
    /// The key of its words when it has one, since the canvas shows whichever language is in use and
    /// the key is the same in all of them. Otherwise the words, since "Start game" says more about
    /// which button this is than "button" does.
    /// </remarks>
    public static string NameOf(UiNode node)
    {
        var name = node.TextKey ?? node.Text?.Trim() ?? "";
        return name.Length == 0 ? node.Type : name;
    }

    /// <summary>
    /// A class list with any direction it named taken out.
    /// </summary>
    /// <remarks>
    /// The type says <c>row</c> now, and a leftover <c>stack</c> or <c>md:row</c> in the classes is
    /// applied over the top of it. The element would keep its old layout and the change would look
    /// like it did nothing.
    /// </remarks>
    public static string Flowing(string classes)
    {
        var kept = Spaces().Split(classes)
            .Where(name => name.Length > 0 && !Directions.Contains(name) && !Directions.Contains(name.Split(':')[^1]))
            .ToList();
        if (!kept.Exists(name => name.StartsWith("gap-", StringComparison.Ordinal)))
        {
            kept.Add("gap-4");
        }

        return string.Join(' ', kept);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
