namespace Orblit.Editor.Core;

/// <summary>A number as decoded JSON holds it: a long when it was written whole, a double otherwise.</summary>
internal static class JsonNumber
{
    public static double? Of(object? value) => value switch
    {
        long whole => whole,
        double real => real,
        _ => null,
    };
}
