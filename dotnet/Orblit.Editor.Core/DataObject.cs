namespace Orblit.Editor.Core;

using Orblit.Json;

/// <summary>What a field in a data object holds.</summary>
/// <remarks>
/// A closed set rather than anything JSON can express. Every one of these has an editor that knows
/// how to show it, and a type nothing can edit is a value somebody can only change by opening the
/// file in a text editor, which is the situation data objects exist to end.
/// </remarks>
public enum DataType
{
    Number,
    Text,
    Toggle,
    Colour,
    Vector,
    Asset,
}

/// <summary>One named value in a data object.</summary>
/// <param name="Key">What scripts and the inspector call it. Unique within an object.</param>
/// <param name="Value">
/// Held as the JSON shape rather than a type per field: the file is the source of truth and every
/// reader of it, whether the editor, a script or a person with a text editor, sees the same thing.
/// </param>
/// <param name="Note">What the field is for, shown beside it. Data outlives whoever set it.</param>
public sealed record DataField(string Key, DataType Type, object? Value, string Note = "")
{
    public OrderedDictionary<string, object?> ToJson()
    {
        var json = new OrderedDictionary<string, object?>
        {
            ["key"] = Key,
            ["type"] = Type.ToString().ToLowerInvariant(),
            ["value"] = Value,
        };
        if (Note.Length > 0)
        {
            json["note"] = Note;
        }

        return json;
    }
}

/// <summary>
/// Values that live in the project rather than in a scene: what a scene object is configured by,
/// such as how fast the ball moves or what a crate weighs. One asset, referenced by everything that
/// needs it, so the value is changed in one place.
/// </summary>
public sealed class DataObject(string name, IReadOnlyList<DataField> fields, string note = "")
{
    public const string Marker = "orblit.data";

    public const int FormatVersion = 1;

    public const string Extension = ".odata";

    /// <summary>What it is called, which need not match the file name.</summary>
    public string Name { get; } = name;

    /// <summary>What it is for. The first thing somebody opening it in six months needs.</summary>
    public string Note { get; } = note;

    public IReadOnlyList<DataField> Fields { get; } = fields;

    /// <summary>
    /// A new one, with a field already in it. Not empty: an empty data object gives nothing to react
    /// to, and the first thing anybody does is add a field anyway.
    /// </summary>
    public static DataObject Blank(string name) => new(name, [new DataField("value", DataType.Number, 0.0)]);

    /// <summary>The file's text, which the Dart editor reads as it reads its own.</summary>
    public string ToText()
    {
        var json = new OrderedDictionary<string, object?>
        {
            ["kind"] = Marker,
            ["formatVersion"] = FormatVersion,
            ["name"] = Name,
        };
        if (Note.Length > 0)
        {
            json["note"] = Note;
        }

        json["fields"] = Fields.Select(field => (object?)field.ToJson()).ToList();
        return JsonText.Encode(json, indent: "  ") + "\n";
    }
}
