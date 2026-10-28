namespace Orblit.Editor.Core;

using System.Diagnostics;
using System.Text.RegularExpressions;
using Orblit.Json;
using Orblit.VectorMath;

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

public static class DataTypes
{
    /// <summary>The word the file writes for it.</summary>
    public static string Name(this DataType type) => type.ToString().ToLowerInvariant();

    /// <summary>What a field of this type starts as.</summary>
    public static object? Blank(this DataType type) => type switch
    {
        DataType.Number => 0.0,
        DataType.Text or DataType.Asset => "",
        DataType.Toggle => false,
        DataType.Colour => "#D9634F",
        DataType.Vector => new List<object?> { 0.0, 0.0, 0.0 },
        _ => throw new UnreachableException(),
    };

    /// <summary>The type a file names, or null for a word that is not one.</summary>
    public static DataType? Named(object? name)
    {
        foreach (var type in Enum.GetValues<DataType>())
        {
            if (type.Name() == name as string)
            {
                return type;
            }
        }

        return null;
    }
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
    public double AsNumber => JsonNumber.Of(Value) ?? 0;

    public string AsText => Value as string ?? "";

    public bool AsToggle => Value is true;

    public Colour AsColour => Colour.FromHex(AsText);

    public Vector3 AsVector => Value is IReadOnlyList<object?> { Count: >= 3 } list
        ? new Vector3(JsonNumber.Of(list[0]) ?? 0, JsonNumber.Of(list[1]) ?? 0, JsonNumber.Of(list[2]) ?? 0)
        : Vector3.Zero();

    /// <summary>One field as a file holds it, or null when it has no key or no type.</summary>
    public static DataField? FromJson(IReadOnlyDictionary<string, object?> entry)
    {
        if (entry.GetValueOrDefault("key") is not string { Length: > 0 } key || DataTypes.Named(entry.GetValueOrDefault("type")) is not { } type)
        {
            return null;
        }

        var value = entry.GetValueOrDefault("value");
        // A value of the wrong shape falls back to a blank of the right one rather than making the
        // whole file unreadable.
        return new DataField(key, type, Holds(type, value) ? value : type.Blank(), entry.GetValueOrDefault("note") as string ?? "");
    }

    private static bool Holds(DataType type, object? value) => type switch
    {
        DataType.Number => JsonNumber.Of(value) is not null,
        DataType.Text or DataType.Colour or DataType.Asset => value is string,
        DataType.Toggle => value is bool,
        DataType.Vector => value is IReadOnlyList<object?> { Count: >= 3 },
        _ => throw new UnreachableException(),
    };

    public OrderedDictionary<string, object?> ToJson()
    {
        var json = new OrderedDictionary<string, object?>
        {
            ["key"] = Key,
            ["type"] = Type.Name(),
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
/// <param name="Name">What it is called, which need not match the file name.</param>
/// <param name="Note">What it is for. The first thing somebody opening it in six months needs.</param>
public sealed partial record DataObject(string Name, IReadOnlyList<DataField> Fields, string Note = "")
{
    public const string Marker = "orblit.data";

    public const int FormatVersion = 1;

    public const string Extension = ".odata";

    /// <summary>
    /// A new one, with a field already in it. Not empty: an empty data object gives nothing to react
    /// to, and the first thing anybody does is add a field anyway.
    /// </summary>
    public static DataObject Blank(string name) => new(name, [new DataField("value", DataType.Number, 0.0)]);

    public DataField? this[string key] => Fields.FirstOrDefault(field => field.Key == key);

    /// <summary>A key nothing else here is using.</summary>
    public string Available(string wanted)
    {
        if (this[wanted] is null)
        {
            return wanted;
        }

        for (var i = 2; ; i++)
        {
            var name = $"{wanted}{i}";
            if (this[name] is null)
            {
                return name;
            }
        }
    }

    /// <summary>
    /// Reads one, or null if it is not a data object. Null rather than an exception: a file may have
    /// been written by a newer build or edited by hand, and a browser that throws on one bad file
    /// shows nothing at all.
    /// </summary>
    public static DataObject? Read(string text)
    {
        object? parsed;
        try
        {
            parsed = JsonText.Decode(text);
        }
        catch (JsonFormatException)
        {
            return null;
        }

        if (parsed is not IReadOnlyDictionary<string, object?> map || map.GetValueOrDefault("kind") is not Marker)
        {
            return null;
        }

        var raw = map.GetValueOrDefault("fields") as IReadOnlyList<object?> ?? [];
        return new DataObject(
            map.GetValueOrDefault("name") as string ?? "Data",
            [.. raw.OfType<IReadOnlyDictionary<string, object?>>().Select(DataField.FromJson).OfType<DataField>()],
            map.GetValueOrDefault("note") as string ?? "");
    }

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

    /// <summary>
    /// The TypeScript a script imports this as. Generated rather than written by hand, so a field
    /// renamed in the editor is a type error in the script rather than an undefined at runtime.
    /// </summary>
    public string ToTypeScript(string importName)
    {
        var type = Pascal(Name);
        List<string> lines = [.. Header(importName), "", $"export interface {type} {{"];
        foreach (var field in Fields)
        {
            if (field.Note.Length > 0)
            {
                lines.Add($"  /** {field.Note} */");
            }

            lines.Add($"  {field.Key}: {TypeScriptOf(field.Type)};");
        }

        lines.AddRange(["}", "", $"declare const data: {type};", "export default data;", ""]);
        return string.Join('\n', lines);
    }

    /// <summary>
    /// The C++ a script includes to read this, the same declaration as <see cref="ToTypeScript"/>.
    /// Accessors rather than a struct of values, because a struct would be a copy, and a copy is
    /// what a shared data object is not.
    /// </summary>
    public string ToCpp(string importName)
    {
        var type = Pascal(Name);
        List<string> lines =
        [
            .. Header(importName),
            "",
            "#pragma once",
            "",
            "#include \"orblit_script.h\"",
            "",
            "/// Each value is resolved to its address once and read through the",
            "/// pointer after that: the read costs a load from memory, and the",
            "/// value behind it still changes when somebody edits the file. That",
            "/// is what makes these safe to call inside a loop over everything.",
            $"namespace {type} {{",
            "",
            $"inline constexpr const char *asset = \"{importName}{Extension}\";",
            "",
        ];
        foreach (var field in Fields)
        {
            if (field.Note.Length > 0)
            {
                lines.Add($"/// {field.Note}");
            }

            lines.AddRange(CppReader(field));
            lines.Add("");
        }

        lines.AddRange([$"}}  // namespace {type}", ""]);
        return string.Join('\n', lines);
    }

    // The words the Dart editor writes, so either editor leaves a binding as it found it.
    private string[] Header(string importName) =>
    [
        $"// Written by the editor from {importName}{Extension}. Do not edit.",
        "//",
        $"// {Name}{(Note.Length == 0 ? "" : $" \u2014 {Note}")}",
    ];

    private static string TypeScriptOf(DataType type) => type switch
    {
        DataType.Number => "number",
        DataType.Text or DataType.Colour or DataType.Asset => "string",
        DataType.Toggle => "boolean",
        DataType.Vector => "[number, number, number]",
        _ => throw new UnreachableException(),
    };

    private static string[] CppReader(DataField field) => field.Type switch
    {
        DataType.Number =>
        [
            $"inline double {field.Key}(double fallback = 0) {{",
            "  static const double *at =",
            $"      ::orblit::number_at(asset, \"{field.Key}\", 0.0);",
            "  return at ? *at : fallback;",
            "}",
        ],
        DataType.Toggle =>
        [
            $"inline bool {field.Key}(bool fallback = false) {{",
            "  static const bool *at =",
            $"      ::orblit::toggle_at(asset, \"{field.Key}\", false);",
            "  return at ? *at : fallback;",
            "}",
        ],
        DataType.Text or DataType.Colour or DataType.Asset =>
        [
            $"inline const char *{field.Key}() {{",
            "  static const char *const *at =",
            $"      ::orblit::text_at(asset, \"{field.Key}\");",
            "  return at ? *at : nullptr;",
            "}",
        ],
        // Three numbers under one key, read as three: the host table carries scalars, and a struct
        // return would be a fourth call shape for the sake of one type.
        DataType.Vector =>
        [
            $"inline double {field.Key}(int axis, double fallback = 0) {{",
            "  static const double *at[3] = {",
            $"      ::orblit::number_at(asset, \"{field.Key}.x\", 0.0),",
            $"      ::orblit::number_at(asset, \"{field.Key}.y\", 0.0),",
            $"      ::orblit::number_at(asset, \"{field.Key}.z\", 0.0)}};",
            "  if (axis < 0 || axis > 2 || !at[axis]) return fallback;",
            "  return *at[axis];",
            "}",
        ],
        _ => throw new UnreachableException(),
    };

    private static string Pascal(string words)
    {
        var joined = string.Concat(NotAWord().Split(words).Where(part => part.Length > 0).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        if (joined.Length == 0)
        {
            return "Data";
        }

        // A type name cannot start with a digit, and a data object called "3rd faction" is reasonable.
        return char.IsAsciiDigit(joined[0]) ? $"Data{joined}" : joined;
    }

    [GeneratedRegex("[^A-Za-z0-9]+")]
    private static partial Regex NotAWord();
}
