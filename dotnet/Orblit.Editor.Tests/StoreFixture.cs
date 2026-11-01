namespace Orblit.Editor.Tests;

using Orblit.Asset;

/// <summary>
/// A store with one kit in it, on a fake network, built the way a publisher builds one: pack the kit, list
/// it in an entry, run the store builder.
/// </summary>
internal sealed class StoreFixture
{
    private StoreFixture(Dictionary<WebUrl, TransportPage> pages, byte[] pack, ContentHash hash)
    {
        Pages = pages;
        Pack = pack;
        Hash = hash;
    }

    /// <summary>The editor's default store, so a session that is not told where to read finds this one.</summary>
    public static WebUrl StoreBase { get; } = WebUrl.Parse("https://orblitengine.com/store/");

    public static WebUrl CrateUrl { get; } = WebUrl.Parse("https://example.com/crates.opack");

    public static byte[] PngBytes { get; } = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 1];

    /// <summary>What the kit holds, by the name it gives each file.</summary>
    public static IReadOnlyDictionary<string, string> CrateFiles { get; } = new Dictionary<string, string>
    {
        ["models/crate.obj"] = "a crate",
        ["scripts/spin.js"] = "spin();",
    };

    /// <summary>Everything the fake network serves: the store's files and the kit file.</summary>
    public Dictionary<WebUrl, TransportPage> Pages { get; }

    /// <summary>The bytes of <c>brass-lamp/crates</c> 1.0.0.</summary>
    public byte[] Pack { get; }

    /// <summary>The hash the entry gives the kit: the hash of the pack's directory.</summary>
    public ContentHash Hash { get; }

    /// <summary>Packs the kit and builds the store under <paramref name="scratch"/>.</summary>
    public static async Task<StoreFixture> BuildAsync(string scratch)
    {
        var source = Path.Combine(scratch, "source");
        foreach (var (name, text) in CrateFiles)
        {
            Write(Path.Combine(source, name), System.Text.Encoding.UTF8.GetBytes(text));
        }

        Write(Path.Combine(source, "shots", "a.png"), PngBytes);
        // One compact line of JSON, so the kit packs to the same bytes as the frozen lock file.
        Write(
            Path.Combine(source, "kit.json"),
            System.Text.Encoding.UTF8.GetBytes(
                """{"slug":"crates","publisher":"brass-lamp","name":"Crates","summary":"Some crates.","version":"1.0.0","licence":"""
                + """{"spdx":"MIT","url":"https://example.com/licence"},"engine":{"from":"0.30.0","to":"0.40.0"},"tags":"""
                + """["props"],"previews":["shots/a.png"]}"""));
        var output = Path.Combine(scratch, "crates.opack");
        var summary = await KitPacking.PackAsync(source, output);
        var pack = await File.ReadAllBytesAsync(output);
        Write(
            Path.Combine(scratch, "entries", "brass-lamp", "crates.json"),
            System.Text.Encoding.UTF8.GetBytes(
                $$"""
                {"releases":[{"version":"1.0.0","url":"{{CrateUrl}}","hash":"{{summary.Hash.Hex}}",
                "bytes":{{pack.Length}},"released":"2026-10-19","notes":"The first release."}]}
                """));
        var pages = new Dictionary<WebUrl, TransportPage> { [CrateUrl] = new(pack) };
        var store = Path.Combine(scratch, "store");
        using (var transport = new MapTransport(pages))
        {
            await KitStoreBuilder.BuildAsync(Path.Combine(scratch, "entries"), store, transport);
        }

        foreach (var file in Directory.EnumerateFiles(store, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetRelativePath(store, file).Replace(Path.DirectorySeparatorChar, '/');
            pages[StoreBase.Resolve(WebUrl.Parse(name))] = new(await File.ReadAllBytesAsync(file));
        }

        return new StoreFixture(pages, pack, summary.Hash);
    }

    /// <summary>
    /// A network that has not been used. A transport is closed by whoever ends up holding it, so every reader
    /// gets its own.
    /// </summary>
    public MapTransport Transport() => new(Pages);

    /// <summary>The store's one row and its release, read back the way the editor reads them.</summary>
    public async Task<(KitRow Row, KitRelease Release)> ListingAsync()
    {
        using var transport = Transport();
        var client = new KitStoreClient(StoreBase, transport);
        var row = Assert.Single((await client.IndexAsync()).Rows);
        return (row, Assert.Single((await client.ReleasesAsync(row)).Releases).Release);
    }

    private static void Write(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }
}
