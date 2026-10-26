namespace Orblit.Editor.Core;

/// <summary>What the cook made of one shader. Plain strings, so a result is the same wherever it was made.</summary>
/// <param name="Path">The shader as a scene names it: relative to the project, with <c>/</c>.</param>
/// <param name="Package">The compiled package, as an absolute path. Null when the cook failed.</param>
/// <param name="Params">The text of the <c>params.json</c> written beside the package.</param>
/// <param name="Problem">The author's line and what is wrong with it. Null when it cooked.</param>
public sealed record ShaderCooked(string Path, string? Package = null, string? Params = null, string? Problem = null);
