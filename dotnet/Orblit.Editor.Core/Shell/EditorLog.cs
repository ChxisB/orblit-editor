namespace Orblit.Editor.Core;

/// <summary>One thing the editor said.</summary>
/// <param name="Level">How much somebody needs to care.</param>
/// <param name="Message">One line. What the status bar said.</param>
/// <param name="Detail">
/// Everything else, such as a compiler's output or a stack trace. Shown when the entry is opened,
/// because most of the time the first line is enough.
/// </param>
/// <param name="At">When it was said, the first time if it was said again.</param>
public sealed record LogEntry(SayLevel Level, string Message, string Detail, DateTimeOffset At)
{
    /// <summary>
    /// How many times in a row this exact thing has been said. A message that arrives once a frame
    /// would otherwise fill the console in two seconds.
    /// </summary>
    public int Repeats { get; init; } = 1;

    public bool SameAs(LogEntry other) => other.Level == Level && other.Message == Message && other.Detail == Detail;
}

/// <summary>
/// Everything the editor has said, kept rather than shown for four seconds in a corner and lost.
/// </summary>
public sealed class EditorLog
{
    private readonly List<LogEntry> _entries = [];
    private readonly int _limit;
    private readonly TimeProvider _clock;

    /// <param name="limit">
    /// How many to keep. Old ones fall off the top, because a console that grows without bound is a leak.
    /// </param>
    /// <param name="clock">For a test.</param>
    public EditorLog(int limit = 500, TimeProvider? clock = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        _limit = limit;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Raised for each entry and each repeat, since a count on screen has to redraw too.</summary>
    public event Action? Changed;

    public IReadOnlyList<LogEntry> Entries => _entries.AsReadOnly();

    public bool HasErrors => _entries.Exists(entry => entry.Level == SayLevel.Error);

    public int CountOf(SayLevel level) => _entries.Count(entry => entry.Level == level);

    public void Say(string message, SayLevel level, string detail = "")
    {
        var entry = new LogEntry(level, message, detail, _clock.GetLocalNow());
        // Folded rather than repeated. The same complaint every frame is one problem, not four hundred.
        if (_entries.Count > 0 && _entries[^1].SameAs(entry))
        {
            _entries[^1] = _entries[^1] with { Repeats = _entries[^1].Repeats + 1 };
        }
        else
        {
            _entries.Add(entry);
            _entries.RemoveRange(0, Math.Max(0, _entries.Count - _limit));
        }

        Changed?.Invoke();
    }

    public void Clear()
    {
        _entries.Clear();
        Changed?.Invoke();
    }
}
