namespace Orblit.Editor.Core;

/// <summary>
/// The system's text clipboard, as the session needs it: read and written a string at a time, after a
/// wait, because a real one cannot be read without one.
/// </summary>
/// <remarks>
/// A copy goes out as text so it can cross into another window, or into a text editor where it is
/// readable.
/// </remarks>
public interface ITextClipboard
{
    Task<string?> GetTextAsync();

    Task SetTextAsync(string text);
}

/// <summary>A clipboard that lives in this process, for a test and for a window with no system to ask.</summary>
public sealed class InMemoryTextClipboard : ITextClipboard
{
    public string? Text { get; set; }

    public Task<string?> GetTextAsync() => Task.FromResult(Text);

    public Task SetTextAsync(string text)
    {
        Text = text;
        return Task.CompletedTask;
    }
}
