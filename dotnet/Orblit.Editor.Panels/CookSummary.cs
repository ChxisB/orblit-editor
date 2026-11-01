namespace Orblit.Editor.Panels;

using Orblit.Asset;
using Orblit.Editor.Core;

/// <summary>What the browser's header says about the cook: the words, what to show on hover, and the colour.</summary>
/// <param name="Text">The line in the header.</param>
/// <param name="Tip">Said on hover, or null when the line says it all.</param>
/// <param name="Tone">The colour of the line, or null for the ordinary one.</param>
public sealed record CookSummary(string Text, string? Tip, CookMark? Tone)
{
    /// <summary>
    /// What the cook has left to do, or null when there is nothing to say.
    /// </summary>
    /// <remarks>
    /// Only what is outstanding: the number cooked is the number nobody has to act on, and a header
    /// that reads "396 cooked, 4 need cooking" buries the four.
    /// </remarks>
    public static CookSummary? Of(CookStatusIndex status)
    {
        if (status.Problem is { } problem)
        {
            return new CookSummary("Cook unknown", $"The cook could not be asked.\n{problem}", CookMark.Bad);
        }

        if (status.Asking)
        {
            return new CookSummary("Checking assets…", null, null);
        }

        var counts = status.Counts;
        var stale = counts.GetValueOrDefault(CookState.Stale);
        var failed = counts.GetValueOrDefault(CookState.Failed);
        if (stale == 0 && failed == 0)
        {
            return null;
        }

        var parts = new List<string>();
        if (failed > 0)
        {
            parts.Add($"{failed} would not cook");
        }

        if (stale > 0)
        {
            parts.Add($"{stale} to cook");
        }

        return new CookSummary(
            string.Join(" · ", parts),
            $"For {status.Target.Name}. {counts.GetValueOrDefault(CookState.Cooked)} already cooked.",
            failed > 0 ? CookMark.Bad : CookMark.Warn);
    }
}
