namespace Orblit.Editor.Panels;

using Orblit.Mesh;

/// <summary>What the coordinate view says.</summary>
public static class UvWords
{
    public const string NothingToShow = "Select faces to see where their texture sits.";

    public const string WrongMode = "Texture coordinates belong to faces. Press G until Faces is on.";

    /// <summary>How the selection is getting its coordinates.</summary>
    public static string Status(IReadOnlyList<Face> faces) => faces.Count == 0
        ? "Nothing selected."
        : $"{faces.Count} selected · {(faces.Any(face => face.Uv.IsManual) ? "drawn by hand" : "following the rule")}";
}
