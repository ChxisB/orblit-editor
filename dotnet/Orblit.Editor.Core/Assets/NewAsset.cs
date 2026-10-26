namespace Orblit.Editor.Core;

using Orblit.Motion;
using Orblit.Ui;
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

    public static readonly NewAsset Script = new("TypeScript", ".ts", "behaviour", Starter(
        """
        // A behaviour, in TypeScript.
        //
        // Everything this puts in the world it puts through `spawn`. The engine asks
        // for a frame, this runs, and what it says is what is drawn.

        import { spawn, onFrame } from "orblit/scene";

        spawn({
          id: "thing",
          at: [0, 0, 0],
          size: 1,
          colour: "#D9634F",
        });

        onFrame((seconds) => {
          spawn({
            id: "thing",
            at: [Math.sin(seconds) * 3, 0, 0],
            size: 1,
            colour: "#D9634F",
          });
        });
        """));

    public static readonly NewAsset Interface = new("Interface", ".tsx", "panel", Starter(
        """
        // An interface, in TypeScript.
        //
        // A component is a function from props to elements. There is no state hook
        // and no lifecycle: the reconciler is on the other side of this, and the
        // interface is described again after every event.

        import { mount } from "orblit";

        const state = { count: 0 };

        function Panel() {
          return (
            <column class="p-4 gap-2 bg-slate-900 rounded-lg border border-slate-700">
              <text class="text-lg font-semibold text-slate-100">Panel</text>
              <text class="text-slate-300">Pressed {state.count} times</text>
              <button
                class="px-3 py-2 rounded bg-ember-500 text-white"
                key="press"
                onPressed={() => { state.count += 1; }}
              >
                Press me
              </button>
            </column>
          );
        }

        mount(() => <Panel />);
        """));

    public static readonly NewAsset Stylesheet = new("Stylesheet", ".css", "theme", Starter(
        """
        /* Declarations an interface can wear over its class list.
         *
         * A subset, and an honest one: what is here is what maps onto a widget tree
         * without lying. Floats, grid areas, selectors and the cascade need a
         * document, and there is not one.
         */

        .panel {
          padding: 16px;
          gap: 8px;
          background: #111418;
          border-radius: 10px;
          border: 1px solid #2A313A;
        }

        .heading {
          font-size: 18px;
          font-weight: 600;
          color: #E9EDF2;
        }
        """));

    public static readonly NewAsset Native = new("C++", ".cpp", "system", Starter(
        """
        // A script, in C++.
        //
        // The same four questions every script answers, whatever it is written in:
        // what contract it was built against, what to do when it starts, what to do
        // each frame, and what to do when it stops. ORBLIT_SCRIPT answers the first and
        // hands you the host.
        //
        // Worth writing in C++ when the loop touches everything every frame. Anything
        // else is quicker to write in TypeScript and fast enough there.

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

    public static readonly NewAsset Theme = new("Theme", ".css", "theme", Starter(
        """
        /* The values an interface is built out of, in one place.
         *
         * Separate from a stylesheet on purpose: a stylesheet says how one thing
         * looks, and this says what the whole project's colours and spacing are. Two
         * files because they change for different reasons and at different rates.
         */

        :root {
          --ink: #E9EDF2;
          --ink-dim: #8A94A3;
          --surface: #111418;
          --raised: #191E25;
          --line: #2A313A;
          --accent: #E58A3F;

          --space: 8px;
          --radius: 10px;
        }
        """));

    // Written by DataFile.Blank rather than as text here, so there is one definition of what a new one contains.
    public static readonly NewAsset DataObject = new("Data object", ".odata", "settings", stem => DataFile.Blank(stem).ToText());

    public static readonly NewAsset Json = new("JSON", ".json", "data", Starter("{}"));

    // Written by UiDocument.Blank, for the same reason as a data object.
    public static readonly NewAsset Canvas = new("Canvas", ".oui", "screen", stem => UiDocument.Blank(stem).ToText());

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
        Folder, Script, Interface, Stylesheet, Native, Header, Theme, DataObject, Json, Canvas, Scene, Clip, Blend, Cutscene, Shader,
        UnlitShader,
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

    // The C++ pair refer to each other by name, and a placeholder somebody has to find and replace
    // is a placeholder that ships. Line endings are normalised because a raw string takes the
    // source file's, and the Dart editor writes a bare newline on every host.
    private static Func<string, string> Starter(string text) =>
        stem => text.ReplaceLineEndings("\n").Replace("NAME", stem, StringComparison.Ordinal) + "\n";
}
