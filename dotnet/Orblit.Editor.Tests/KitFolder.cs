namespace Orblit.Editor.Tests;

/// <summary>A folder a person would publish as a kit.</summary>
internal static class KitFolder
{
    /// <summary>The address a kit made by <see cref="Write"/> is hosted at.</summary>
    public const string HostedAt = "https://example.com/kits/crates-1.0.0.opack";

    /// <summary>Writes a manifest for <c>brass-lamp/crates</c> and one model into <paramref name="folder"/>.</summary>
    public static void Write(string folder, string version = "1.0.0")
    {
        Directory.CreateDirectory(Path.Combine(folder, "models"));
        File.WriteAllText(
            Path.Combine(folder, "kit.json"),
            $$"""
            {"slug":"crates","publisher":"brass-lamp","name":"Crates","summary":"Some crates.","version":"{{version}}",
            "licence":{"spdx":"MIT","url":"https://example.com/licence"},"engine":{"from":"0.30.0","to":"0.40.0"},
            "tags":[],"previews":[]}
            """);
        File.WriteAllText(Path.Combine(folder, "models", "crate.obj"), "v 0 0 0");
    }
}
