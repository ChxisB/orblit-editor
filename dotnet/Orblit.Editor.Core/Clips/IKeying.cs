namespace Orblit.Editor.Core;

/// <summary>
/// What the inspector asks of whatever is keying, so a row can grow a key button without knowing
/// anything about clips.
/// </summary>
public interface IKeying
{
    /// <summary>Raised when what <see cref="MarkFor"/> would answer may have changed.</summary>
    event Action? Changed;

    /// <summary>How <paramref name="fieldPath"/> of <paramref name="target"/> looks from the clip, where the playhead is.</summary>
    KeyMark MarkFor(SceneObject target, string fieldPath);

    /// <summary>Keys <paramref name="fieldPath"/> of <paramref name="target"/> where the playhead is, as it is now.</summary>
    void Key(SceneObject target, string fieldPath);
}
