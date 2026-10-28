namespace Orblit.Editor.Tests;

using Orblit.Asset;
using Orblit.Editor.Core;

public sealed class StoreQueryTests
{
    private static readonly IReadOnlyList<KitRow> Rows =
    [
        Row("crates", name: "Crates", tags: ["props"], updated: "2026-10-03"),
        Row("rain", summary: "Rain and fog.", licence: "CC0-1.0", scripts: true),
        Row("zebra", name: "A zebra", publisher: "stripes", updated: "2026-10-03"),
        Row("old", from: "0.10.0", to: "0.20.0", updated: "2026-10-09"),
    ];

    // F2: the fields a case sets, each named at the call, as the Dart helper has them.
    private static KitRow Row(
        string slug,
        string name = "",
        string summary = "A kit.",
        string publisher = "brass-lamp",
        string licence = "MIT",
        string updated = "2026-10-01",
        IReadOnlyList<string>? tags = null,
        bool scripts = false,
        string from = "0.30.0",
        string to = "0.40.0") =>
        new(
            new KitManifest(
                slug: slug,
                publisher: publisher,
                name: name.Length == 0 ? slug : name,
                summary: summary,
                version: "1.0.0",
                licence: new KitLicence(licence, "https://example.com/l"),
                engine: new KitEngine(from, to),
                containsScripts: scripts,
                tags: tags),
            updated);

    private static string[] Ask(StoreQuery query, string engine = "0.35.0", bool showIncompatible = false) =>
        [.. query.Apply(Rows, engine, showIncompatible).Select(row => row.Manifest.Slug)];

    [Fact]
    public void ListsTheNewestFirstAndByNameWhenAsked()
    {
        Assert.Equal(new[] { "zebra", "crates", "rain" }, Ask(new StoreQuery()));
        Assert.Equal(new[] { "zebra", "crates", "rain" }, Ask(new StoreQuery { Sort = StoreSort.Name }));

        // Added in C#. With the old kit shown the two orders differ, so each is seen to be its own.
        Assert.Equal(new[] { "old", "zebra", "crates", "rain" }, Ask(new StoreQuery(), showIncompatible: true));
        Assert.Equal(
            new[] { "zebra", "crates", "old", "rain" },
            Ask(new StoreQuery { Sort = StoreSort.Name }, showIncompatible: true));
    }

    [Fact]
    public void LeavesOutAKitMadeForOtherEnginesUnlessAsked()
    {
        Assert.DoesNotContain("old", Ask(new StoreQuery()));
        Assert.Contains("old", Ask(new StoreQuery(), showIncompatible: true));
    }

    [Fact]
    public void KeepsAKitWhenTheEngineVersionIsNotKnown() =>
        Assert.Contains("old", Ask(new StoreQuery(), engine: "unknown"));

    [Fact]
    public void NeedsEveryWordInTheNameSummaryPublisherSlugOrTags()
    {
        Assert.Equal(new[] { "crates" }, Ask(new StoreQuery { Text = "crat PROPS" }));
        Assert.Equal(new[] { "rain" }, Ask(new StoreQuery { Text = "fog" }));
        Assert.Equal(new[] { "zebra" }, Ask(new StoreQuery { Text = "stripes" }));
        Assert.Empty(Ask(new StoreQuery { Text = "crates fog" }));
    }

    [Fact]
    public void NarrowsByLicenceByTagAndByScripts()
    {
        Assert.Equal(new[] { "rain" }, Ask(new StoreQuery { Licence = "CC0-1.0" }));
        Assert.Equal(new[] { "crates" }, Ask(new StoreQuery { Tag = "props" }));
        Assert.Equal(new[] { "zebra", "crates" }, Ask(new StoreQuery { HideScripts = true }));
    }

    [Fact]
    public void CopiesWithOneFieldChanged()
    {
        var query = new StoreQuery { Text = "a" };

        var copy = query with { Tag = "props" };

        Assert.Equal(("a", "props"), (copy.Text, copy.Tag));
    }

    [Fact]
    public void ListsTheLicencesAndTagsInUseSorted()
    {
        Assert.Equal(new[] { "CC0-1.0", "MIT" }, StoreQuery.Licences(Rows));
        Assert.Equal(new[] { "props" }, StoreQuery.Tags(Rows));
    }
}
