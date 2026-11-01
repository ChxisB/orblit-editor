namespace Orblit.Editor.Core;

public static class Elapsed
{
    /// <summary>How long ago, in the shortest form that is still true.</summary>
    public static string Ago(DateTime then, DateTime now)
    {
        var elapsed = now - then;
        return elapsed switch
        {
            { TotalMinutes: < 1 } => "just now",
            { TotalMinutes: < 60 } => $"{(int)elapsed.TotalMinutes}m ago",
            { TotalHours: < 24 } => $"{(int)elapsed.TotalHours}h ago",
            { TotalDays: < 30 } => $"{(int)elapsed.TotalDays}d ago",
            _ => $"{(int)elapsed.TotalDays / 30}mo ago",
        };
    }
}
