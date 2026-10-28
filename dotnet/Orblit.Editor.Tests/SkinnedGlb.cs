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

    /// <summary>
    /// A person standing with the hips a metre up, facing +Z with the left side towards +X, as a
    /// <c>.glb</c> file.
    /// </summary>
    /// <remarks>
    /// With <paramref name="forearmTwist"/> each forearm has a bone between the elbow and the wrist that
    /// only twists it.
    /// </remarks>
    public static byte[] PersonGlb(bool forearmTwist = false)
    {
        var nodes = new List<Dictionary<string, object>>();

        int Add(string name, int parent, double x, double y, double z)
        {
            nodes.Add(new() { ["name"] = name, ["translation"] = new[] { x, y, z } });
            if (parent >= 0)
            {
                nodes[parent].TryAdd("children", new List<int>());
                ((List<int>)nodes[parent]["children"]).Add(nodes.Count - 1);
            }

            return nodes.Count - 1;
        }

        var hips = Add("hips", -1, 0, 1, 0);
        var spine = Add("spine", hips, 0, 0.15, 0);
        var chest = Add("chest", spine, 0, 0.2, 0);
        var neck = Add("neck", chest, 0, 0.25, 0);
        Add("head", neck, 0, 0.12, 0);
        foreach (var (side, sign) in new[] { ("left", 1.0), ("right", -1.0) })
        {
            var shoulder = Add($"{side} shoulder", chest, sign * 0.06, 0.2, 0);
            var upper = Add($"{side} upper arm", shoulder, sign * 0.1, 0, 0);
            var lower = Add($"{side} lower arm", upper, sign * 0.28, 0, 0.02);
            var wrist = forearmTwist ? Add($"{side} forearm twist", lower, sign * 0.12, 0, -0.01) : lower;
            Add($"{side} hand", wrist, sign * (forearmTwist ? 0.13 : 0.25), 0, -0.02);
            var thigh = Add($"{side} thigh", hips, sign * 0.1, -0.05, 0);
            var shin = Add($"{side} shin", thigh, 0, -0.45, 0.02);
            var foot = Add($"{side} foot", shin, 0, -0.42, -0.02);
            Add($"{side} toes", foot, 0, -0.05, 0.12);
        }

        return Glb(new
        {
            asset = new { version = "2.0" },
            scene = 0,
            scenes = new[] { new { nodes = new[] { 0 } } },
            nodes,
            skins = new[] { new { joints = Enumerable.Range(0, nodes.Count).ToArray() } },
        });
    }
}
