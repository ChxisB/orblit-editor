namespace Orblit.Editor.Core;

using Orblit.Scene;

/// <summary>A prefab found on disk, or why there is not one.</summary>
public readonly record struct PrefabRead(PrefabDocument? Prefab, string? Problem);

/// <summary>
/// A project's prefabs, as the scenes open in this editor were opened against them.
/// </summary>
/// <remarks>
/// The file is the engine's <see cref="PrefabDocument"/>, and so is everything done with one. What is
/// here is the part only an editor has: a folder on disk, and a memory of what was in it.
/// <para>
/// Remembered rather than read afresh each time, because an instance is saved as what is different
/// about it from its prefab, and that difference has to be worked out against the prefab it was
/// opened from. Read the file again after somebody has changed it in another program and the gap
/// between the old lamp and the new one would be saved as a change to every lamp in the scene,
/// pinning the old one in place for good. Kept, the scene saves what was changed in it and the new
/// lamp arrives the next time it is opened.
/// </para>
/// <para>
/// A prefab that is not there, or cannot be read, is not remembered: its instances stay folded,
/// exactly as they were saved, and the next look may find the file.
/// </para>
/// </remarks>
public sealed class PrefabFiles(string root)
{
    /// <summary>The extension a prefab file carries.</summary>
    public const string Extension = PrefabDocument.Extension;

    private readonly Dictionary<string, PrefabDocument> _known = [];

    /// <summary>The project directory, which prefab paths are relative to.</summary>
    public string Root { get; } = root;

    /// <summary>
    /// The prefab at <paramref name="asset"/>, or null when there is not a readable one. This is the
    /// <see cref="PrefabSource"/> handed to everything in the engine that opens or folds an instance.
    /// </summary>
    public PrefabDocument? Find(string asset) => Read(asset).Prefab;

    /// <summary>The prefab at <paramref name="asset"/>, or why there is not one.</summary>
    public PrefabRead Read(string asset)
    {
        if (_known.TryGetValue(asset, out var known))
        {
            return new PrefabRead(known, null);
        }

        var path = Path.Combine(Root, asset);
        var name = Path.GetFileName(asset);
        if (!File.Exists(path))
        {
            return new PrefabRead(null, $"{name} is not in the project any more.");
        }

        try
        {
            var prefab = PrefabDocument.Decode(File.ReadAllText(path)).Prefab;
            _known[asset] = prefab;
            return new PrefabRead(prefab, null);
        }
        catch (SceneFormatException error)
        {
            return new PrefabRead(null, $"{name} is not a readable prefab: {error.Message}");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new PrefabRead(null, $"Could not read {name}: {error.Message}");
        }
    }

    /// <summary>
    /// Writes <paramref name="prefab"/> to <paramref name="asset"/> and remembers it as what that file
    /// now is. Returns why it could not, or null.
    /// </summary>
    public string? Write(string asset, PrefabDocument prefab)
    {
        var path = Path.Combine(Root, asset);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, prefab.Encode());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return $"Could not write {Path.GetFileName(path)}: {error.Message}";
        }

        _known[asset] = prefab;
        return null;
    }

    /// <summary>
    /// Forgets every prefab but <paramref name="assets"/>, so each is read again the next time it is
    /// wanted. Only safe for a prefab no open scene has an instance of: those were opened against what
    /// is remembered, and have to be saved against it.
    /// </summary>
    public void KeepOnly(IReadOnlySet<string> assets)
    {
        foreach (var asset in _known.Keys.Where(asset => !assets.Contains(asset)).ToList())
        {
            _known.Remove(asset);
        }
    }

    /// <summary>
    /// A source that hands back <paramref name="prefab"/> for <paramref name="asset"/> and this
    /// project's own for everything else. What a change to one prefab is worked out with: the
    /// instances of it that are open now were opened against the prefab as it was, and are folded
    /// against that before being opened against the new one.
    /// </summary>
    public PrefabSource WithOne(string asset, PrefabDocument? prefab) =>
        wanted => wanted == asset ? prefab : Find(wanted);
}
