using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Orblit.Editor.Tests;

/// <summary>A model with a skeleton and nothing else, as a test can write it to disk.</summary>
internal static class SkinnedGlb
{
    /// <summary><paramref name="document"/> as the bytes of a <c>.glb</c> file.</summary>
    public static byte[] Glb(object document)
    {
        var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(document));
        var padded = new byte[json.Length + ((4 - (json.Length % 4)) % 4)];
        json.CopyTo(padded, 0);
        Array.Fill(padded, (byte)0x20, json.Length, padded.Length - json.Length);

        var file = new byte[20 + padded.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(8), (uint)file.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(12), (uint)padded.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(16), 0x4E4F534A);
        padded.CopyTo(file, 20);
        return file;
    }

    /// <summary>The document of a skeleton of bones <paramref name="names"/>, each the child of the one before and a metre above it.</summary>
    public static object ChainDocument(params string[] names) => new
    {
        asset = new { version = "2.0" },
        scene = 0,
        scenes = new[] { new { nodes = new[] { 0 } } },
        nodes = names.Select((name, index) => new Dictionary<string, object>
        {
            ["name"] = name,
            ["translation"] = new[] { 0, index == 0 ? 0 : 1, 0 },
            ["children"] = index + 1 < names.Length ? new[] { index + 1 } : [],
        }).ToArray(),
        skins = new[] { new { joints = Enumerable.Range(0, names.Length).ToArray() } },
    };

    /// <summary>A skeleton of bones <paramref name="names"/>, as a <c>.glb</c> file.</summary>
    public static byte[] ChainGlb(params string[] names) => Glb(ChainDocument(names));
}
