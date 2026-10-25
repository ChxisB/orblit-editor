using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// The clipboard of the window it is attached to, so a copy can be pasted into another program or
/// another editor window. Before a window is attached, and where the system offers none, it holds
/// the text in memory.
/// </summary>
internal sealed class WindowTextClipboard : ITextClipboard
{
    private readonly InMemoryTextClipboard _memory = new();
    private TopLevel? _owner;

    public void Attach(TopLevel owner) => _owner = owner;

    public Task<string?> GetTextAsync() =>
        _owner?.Clipboard is { } system ? system.TryGetTextAsync() : _memory.GetTextAsync();

    public Task SetTextAsync(string text) =>
        _owner?.Clipboard is { } system ? system.SetTextAsync(text) : _memory.SetTextAsync(text);
}
