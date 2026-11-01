namespace Orblit.Editor.Core;

using Orblit.Motion;
using DataFile = DataObject;

/// <summary>The things the project browser knows how to make.</summary>
/// <remarks>
/// A starter rather than an empty file. Somebody who asks for a new script wants to write the
/// second line of it, not to look up which import the first one takes, and a file that runs the
/// moment it is made says more about what the engine expects than any amount of documentation.
/// <para>
/// A kind with a format of its own is written by that format, so a new one is whatever the format
/// says an empty one is. Text starters have the file's own name written in where they say NAME.
/// </para>
/// </remarks>
public sealed class NewAsset
{
    public static readonly NewAsset Folder = new("Folder", "", "New folder", _ => "");

    public static readonly NewAsset Native = new("C++", ".cpp", "system", Starter(
        """
        // A script, in C++.
        //
        // The four questions every script answers: what contract it was built
        // against, what to do when it starts, what to do each frame, and what to do
        // when it stops. ORBLIT_SCRIPT answers the first and hands you the host.

        #include "NAME.h"

        namespace {
        OrblitComponent drift;
        OrblitTransforms transforms;
        double elapsed = 0;
        }  // namespace

        ORBLIT_SCRIPT {
          transforms = orblit::host()->transform_register(orblit::world());
          drift = orblit::component<Drift>("Drift");
          orblit::log("started");
        }

        extern "C" void orblit_step(double delta) {
          elapsed += delta;

          const OrblitComponent wanted[] = {transforms.local, drift};
          OrblitQuery *query = orblit::host()->query_create(orblit::world(), wanted, 2);

          const uint32_t chunks = orblit::host()->query_chunk_count(query);
          for (uint32_t chunk = 0; chunk < chunks; ++chunk) {
            const uint32_t length = orblit::host()->query_chunk_length(query, chunk);
            auto *local = static_cast<float *>(
                orblit::host()->query_chunk_column(query, chunk, 0));
            auto *drifts = static_cast<Drift *>(
                orblit::host()->query_chunk_column(query, chunk, 1));

            // One crossing for the whole run, then plain C++ over the engine's own
            // memory. Calling in once per entity is what the column layout exists to
            // avoid.
            for (uint32_t i = 0; i < length; ++i) {
              local[i * 10] += static_cast<float>(drifts[i].speed * delta);
            }
          }

          orblit::host()->query_destroy(query);
        }

        extern "C" void orblit_stop(void) { orblit::log("stopped"); }
        """));

    public static readonly NewAsset Header = new("C++ header", ".h", "system", Starter(
        """
        // What NAME offers to whatever else is compiled with it.
        //
        // Declarations only. The engine calls orblit_start, orblit_step and orblit_stop
        // through their C names. This is for the code either side of that boundary,
        // such as a component layout two scripts share or a helper the source keeps
        // out of itself.

        #pragma once

        #include "orblit_script.h"

        /// A component's layout, declared once so two files reading the same column
        /// cannot disagree about what is in it.
        struct Drift {
          double speed;
        };
        """));

    // Written by DataFile.Blank rather than as text here, so there is one definition of what a new one contains.
    public static readonly NewAsset DataObject = new("Data object", ".odata", "settings", stem => DataFile.Blank(stem).ToText());

    public static readonly NewAsset Json = new("JSON", ".json", "data", Starter("{}"));

    public static readonly NewAsset Scene = new("Scene", ".oscene", "untitled", Starter("""{"version":3,"objects":[]}"""));

    // Written by ClipDocument rather than as text here, so a new clip is whatever the format says an empty one is.
    public static readonly NewAsset Clip = new("Clip", ".oclip", "clip", stem => new ClipDocument(stem, 1).Encode());

    // Written by BlendDocument, for the same reason as a clip.
    public static readonly NewAsset Blend = new("Blend", ".oblend", "blend", stem =>
        new BlendDocument(stem, [new BlendState("idle", new BlendClip("idle"))]).Encode());

    // Written by CutsceneDocument, for the same reason as a clip.
    public static readonly NewAsset Cutscene = new("Cutscene", ".ocutscene", "cutscene", stem =>
        new CutsceneDocument(new ClipDocument(stem, 4)).Encode());

    public static readonly NewAsset Shader = new("Lit shader", ".oshader", "surface", Starter(
        """
        // A surface the scene lights.
        //
        // A uniform marked `instance` is set for each object that wears this shader,
        // in the inspector. `source_color` gives a colour a row of swatches and
        // `hint_range` gives a number a slider.
        shader_type spatial;

        instance uniform vec4 tint : source_color = vec4(0.85, 0.39, 0.31, 1.0);
        instance uniform float rough : hint_range(0.0, 1.0, 0.01) = 0.5;
        instance uniform float metal : hint_range(0.0, 1.0, 0.01) = 0.0;

        void fragment() {
          ALBEDO = tint.rgb;
          ALPHA = tint.a;
          ROUGHNESS = rough;
          METALLIC = metal;
        }
        """));

    public static readonly NewAsset UnlitShader = new("Unlit shader", ".oshader", "glow", Starter(
        """
        // A surface that ignores light, for anything that carries its own brightness.
        //
        // `TIME` is seconds since the renderer started.
        shader_type spatial;
        render_mode unshaded;

        instance uniform vec4 tint : source_color = vec4(0.85, 0.39, 0.31, 1.0);
        instance uniform float speed : hint_range(0.0, 10.0, 0.1) = 2.0;

        void fragment() {
          float pulse = 0.6 + 0.4 * sin(TIME * speed);
          ALBEDO = tint.rgb * pulse;
          ALPHA = tint.a;
        }
        """));

    /// <summary>Every kind, in the order the browser lists them.</summary>
    public static readonly IReadOnlyList<NewAsset> Values =
    [
        Folder, Native, Header, DataObject, Json, Scene, Clip, Blend, Cutscene, Shader, UnlitShader,
    ];

    private readonly Func<string, string> _contents;

    private NewAsset(string label, string extension, string suggested, Func<string, string> contents)
    {
        Label = label;
        Extension = extension;
        Suggested = suggested;
        _contents = contents;
    }

    public string Label { get; }

    /// <summary>Empty for a folder, which is the one of these that is not a file.</summary>
    public string Extension { get; }

    /// <summary>What the name box starts with. The browser makes it unique.</summary>
    public string Suggested { get; }

    public bool IsFolder => this == Folder;

    /// <summary>
    /// A second file written alongside, or null. C++ arrives as a pair. A source file with nowhere to
    /// declare what it shares is a source file that will grow a header the moment a second one needs
    /// to read the same component, and by then the layout has been typed twice.
    /// </summary>
    public NewAsset? Companion => this == Native ? Header : null;

    /// <summary>What a new file of this kind called <paramref name="stem"/> says the moment it is made.</summary>
    public string Contents(string stem) => _contents(stem);

    // The C++ pair refer to each other by name, and a placeholder somebody has to find and replace is a
    // placeholder that ships. Line endings are normalised because a raw string takes the source file's, and
    // the file holds a bare newline on every host.
    private static Func<string, string> Starter(string text) =>
        stem => text.ReplaceLineEndings("\n").Replace("NAME", stem, StringComparison.Ordinal) + "\n";
}
