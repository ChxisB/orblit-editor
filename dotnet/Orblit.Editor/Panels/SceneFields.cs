using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Scene;

/// <summary>
/// The scene's own settings. A scene is a thing with properties and not only a container: the sky and
/// the light it casts belong to it rather than to anything in it, and there was nowhere to put them
/// until it had a row of its own.
/// </summary>
internal sealed class SceneFields(Fields fields)
{
    private const string LoadTip = "Open this scene for editing.";

    // Thirty-two fields down a side panel would bury the ones somebody uses, so it is asked for.
    private bool _allLayers;

    /// <summary>The panel for <paramref name="entry"/>: its settings if it is open, and where it is if it is not.</summary>
    /// <param name="onLoad">Opens an entry that is not open.</param>
    public Control Build(FieldHost host, SceneEntry entry, Action<SceneEntry> onLoad)
    {
        if (entry.Scene is not { } scene)
        {
            return Unloaded(host, entry, onLoad);
        }

        // What every scene has is a scene, and almost nothing about a scene applies to it: it has no sky
        // of its own and no weather, because it is in whatever sky the open scene has.
        return entry.Id == Workspace.SharedSceneId ? Shared(host, scene) : Loaded(host, entry, scene);
    }

    private StackPanel Loaded(FieldHost host, SceneEntry entry, EditorScene scene) => Fields.Column(
    [
        InspectorBands.Header(host, new HeaderOptions
        {
            Name = scene.Name,
            Caption = "Scene",
            Rename = now => now == scene.Name ? null : new RenameScene(entry.Id, scene.Name, now),
        }),
        Where(SceneFieldsModel.FileLine(entry)),
        Sky(entry, scene),
        Environment(entry, scene),
        Layers(entry, scene),
        Contents(scene),
    ]);

    // An unloaded scene has no document to show settings from. Saying where it is and offering to open
    // it beats a panel of fields that would edit nothing.
    private static StackPanel Unloaded(FieldHost host, SceneEntry entry, Action<SceneEntry> onLoad)
    {
        var load = Fields.Button("Load scene", LoadTip, () => onLoad(entry));
        load.Margin = new Thickness(Space.Lg, 0);
        return Fields.Column(
        [
            InspectorBands.Header(host, new HeaderOptions { Name = entry.Title }),
            Where(SceneFieldsModel.WhereLine(entry)),
            load,
            Padded(Fields.Note("Loading a scene replaces the one open. Only one scene is in the viewport at a time.")),
        ]);
    }

    private StackPanel Shared(FieldHost host, EditorScene scene) => Fields.Column(
    [
        InspectorBands.Header(host, new HeaderOptions { Name = "Shared" }),
        Padded(Fields.Note("What every scene in this project has in it. Objects here are drawn and lit alongside whichever scene is open, and saved beside it.")),
        Contents(scene),
        Padded(Fields.Note("A scene of its own overrules it: a sun or a Weather object in the open scene is used instead of the one here, so a level can have its own without the shared one being in the way.")),
    ]);

    private Control Sky(SceneEntry entry, EditorScene scene)
    {
        var history = fields.Host.History;
        void Set(Func<SceneTime, SceneTime> change)
        {
            var now = SceneFieldsModel.TimeOf(scene);
            history.Run(new SetSceneTime(entry.Id, now, change(now)));
        }

        var rows = new List<Control>
        {
            fields.Choice(
                "Day cycle",
                ["Off", "On"],
                scene.DayCycle ? "On" : "Off",
                chosen =>
                {
                    if ((chosen == "On") != scene.DayCycle)
                    {
                        Set(time => time with { Cycle = chosen == "On" });
                    }
                }),
            fields.Slider("Time", scene.TimeOfDay, value => Set(time => time with { Hour = value }), new SliderOptions { Min = 0, Max = 24, Decimals = 2 }),
            Fields.Text(scene.DayCycle ? "Now" : "Set to", SceneFieldsModel.Now(scene)),
        };
        if (scene.DayCycle)
        {
            rows.Add(fields.Slider(
                "Speed",
                scene.HoursPerSecond,
                value => Set(time => time with { Speed = value }),
                new SliderOptions { Min = 0.05, Max = 6, Decimals = 2, Unit = " h/s" }));
        }

        rows.Add(Fields.Note(SceneFieldsModel.TimeNote(scene.DayCycle)));
        return fields.Section("Sky", Fields.Column(rows));
    }

    private Control Environment(SceneEntry entry, EditorScene scene)
    {
        if (scene.DayCycle)
        {
            return fields.Section(
                "Environment",
                Fields.Column(
                [
                    Fields.Text("Sky", "From the time of day"),
                    Fields.Text("Ambient", InspectorModel.Lux(scene.SkyState.Ambient)),
                ]));
        }

        var history = fields.Host.History;
        return fields.Section(
            "Environment",
            Fields.Column(
            [
                fields.Palette(
                    "Sky",
                    scene.SkyColour,
                    colour => history.Run(new SetSceneSky(entry.Id, scene.SkyColour, colour, scene.Ambient, scene.Ambient))),
                fields.Slider(
                    "Ambient",
                    scene.Ambient,
                    lux => history.Run(new SetSceneSky(entry.Id, scene.SkyColour, scene.SkyColour, scene.Ambient, lux)),
                    new SliderOptions { Min = 0, Max = 120000, Unit = " lx" }),
            ]));
    }

    private Control Layers(SceneEntry entry, EditorScene scene)
    {
        var rows = new List<Control>();
        var count = InspectorModel.LayerRows(scene.LayerNames, _allLayers);
        for (var index = 0; index < count; index++)
        {
            rows.Add(LayerName(entry, scene, index));
        }

        if (count < SceneSettings.LayerCount)
        {
            rows.Add(Fields.Button(
                $"Show all {SceneSettings.LayerCount}",
                "Show a field for every layer.",
                () => fields.Host.Apply(() => _allLayers = true)));
        }

        rows.Add(Fields.Note("Names show on the layer toggles of a physics body."));
        return fields.Section("Layers", Fields.Column(rows));
    }

    private Control LayerName(SceneEntry entry, EditorScene scene, int index) => fields.Entry(
        $"Layer {index + 1}",
        index < scene.LayerNames.Count ? scene.LayerNames[index] : "",
        value =>
        {
            var next = InspectorModel.RenamedLayers(scene.LayerNames, index, value);
            if (!next.SequenceEqual(scene.LayerNames))
            {
                fields.Host.History.Run(new SetLayerNames(entry.Id, scene.LayerNames, next));
            }
        },
        "Unnamed");

    private Control Contents(EditorScene scene)
    {
        var (objects, drawn, lights) = SceneFieldsModel.Counts(scene);
        return fields.Section(
            "Contents",
            Fields.Column(
            [
                Fields.Text("Objects", $"{objects}"),
                Fields.Text("Drawn", $"{drawn}"),
                Fields.Text("Lights", $"{lights}"),
            ]));
    }

    private static TextBlock Where(string text) => new()
    {
        Text = text,
        FontSize = 11,
        FontFamily = OrblitText.Mono,
        TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
        Margin = new Thickness(Space.Lg, 0, Space.Lg, Space.Sm),
    };

    private static Control Padded(Control child)
    {
        child.Margin = new Thickness(Space.Lg);
        return child;
    }
}
