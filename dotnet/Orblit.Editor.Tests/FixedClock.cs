namespace Orblit.Editor.Tests;

/// <summary>A clock a test sets. Local time is UTC, so a test reads the same in every time zone.</summary>
internal sealed class FixedClock(DateTime at) : TimeProvider
{
    public DateTime At { get; set; } = at;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(At, DateTimeKind.Utc));
}
