namespace Orblit.Editor.Core;

/// <summary>
/// Whether the person has agreed to run the code in a project. Opening a project never builds or runs
/// anything, so Play asks this before it builds the game.
/// </summary>
public interface IProjectTrust
{
    bool IsTrusted(string projectFolder);
}

/// <summary>Trusts nothing. A session has this until the editor is given the real answer.</summary>
public sealed class NoProjectTrust : IProjectTrust
{
    public bool IsTrusted(string projectFolder) => false;
}
