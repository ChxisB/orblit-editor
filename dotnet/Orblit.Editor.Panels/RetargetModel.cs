namespace Orblit.Editor.Panels;

using Orblit.Motion;

/// <summary>How a bone of the target comes to be moved, as the table shows it.</summary>
public enum PairHow
{
    /// <summary>Names and the body decide. There may be nothing that pairs.</summary>
    Auto,

    /// <summary>A bone of the source was chosen for it.</summary>
    Hand,

    /// <summary>Left alone, whatever would pair it.</summary>
    None,
}

/// <summary>One bone of the target and what moves it.</summary>
/// <param name="Bone">The bone of the target.</param>
/// <param name="Driver">The bone of the source that moves <paramref name="Bone"/>, or null for nothing.</param>
/// <param name="How">Whether that was chosen by hand, left alone, or left to names and the body.</param>
public sealed record PairRow(string Bone, string? Driver, PairHow How);

/// <summary>A bone map read against the two skeletons it is for.</summary>
/// <param name="Rows">Every bone of the target, in the skeleton's order.</param>
/// <param name="Map">
/// The map with every name a skeleton lacks taken out, which is what a retarget can be given. A map made
/// for another model has such names.
/// </param>
/// <param name="Stale">Bones of the target the map names that one of the skeletons lacks.</param>
/// <param name="Problems">What the pairing could not do, as retargeting words it.</param>
public sealed record PairTable(IReadOnlyList<PairRow> Rows, BoneMap Map, IReadOnlyList<string> Stale, IReadOnlyList<string> Problems)
{
    /// <summary>How many bones of the target something moves.</summary>
    public int Driven => Rows.Count(static row => row.Driver is not null);

    /// <summary>
    /// What <paramref name="map"/> does between <paramref name="from"/> and <paramref name="to"/>, bone by
    /// bone.
    /// </summary>
    /// <remarks>
    /// Pairs are not worked out again here. The map's own pairing is the one a retarget uses, so what the
    /// table says is what a clip gets.
    /// </remarks>
    public static PairTable Of(RestSkeleton from, RestSkeleton to, BoneMap map)
    {
        var fitted = map.Fitted(from, to);
        var (drivers, problems) = fitted.Pairing(from, to);
        return new PairTable(
            [.. to.Names.Select(bone => new PairRow(bone, drivers.TryGetValue(bone, out var driver) ? driver : null, HowOf(fitted, bone)))],
            fitted,
            map.Stale(from, to),
            problems);
    }

    private static PairHow HowOf(BoneMap fitted, string bone) =>
        fitted.Bones.ContainsKey(bone) ? PairHow.Hand : fitted.Unpaired.Contains(bone) ? PairHow.None : PairHow.Auto;
}

/// <summary>What the retarget dialog is set to: the two models and the pairing.</summary>
/// <param name="From">Project path of the model the clip was made for, or null before it is chosen.</param>
/// <param name="To">Project path of the model the clip is made to move, or null before it is chosen.</param>
/// <param name="Map">How the bones pair.</param>
public sealed record RetargetDraft(string? From, string? To, BoneMap Map);

/// <summary>What the retarget dialog calls things.</summary>
public static class RetargetWords
{
    /// <summary>The name for <paramref name="clip"/> once it moves <paramref name="model"/>, such as <c>Walk (dwarf)</c>.</summary>
    public static string NameFor(string clip, string model)
    {
        // Either separator, because a project path may be written on another platform.
        var file = model[(model.LastIndexOfAny(['/', '\\']) + 1)..];
        var dot = file.LastIndexOf('.');
        return $"{clip} ({(dot > 0 ? file[..dot] : file)})";
    }
}
