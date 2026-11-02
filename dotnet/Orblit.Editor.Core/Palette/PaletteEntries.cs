namespace Orblit.Editor.Core;

using Orblit.Mesh;

/// <summary>
/// What the command palette can find: the commands the menus and the keyboard already have, and the
/// objects in the scene.
/// </summary>
/// <remarks>
/// shortcut: only the commands the session can do so far. Export and the kit store join them with
/// the panels that need them (task 16).
/// </remarks>
public static class PaletteEntries
{
    public static IReadOnlyList<PaletteEntry> For(EditorSession session, HostPlatform platform) =>
    [
        .. Files(session, platform),
        .. Edits(session, platform),
        .. Adds(session),
        .. Views(session),
        .. Objects(session),
    ];

    private static PaletteEntry Command(string label, string icon, Action run, string? keys = null, string meta = "") => new()
    {
        Group = PaletteGroup.Commands,
        Label = label,
        Icon = icon,
        OnRun = run,
        Keys = keys,
        Meta = meta,
    };

    private static PaletteEntry[] Files(EditorSession session, HostPlatform platform) =>
    [
        Command("New scene", "note_add_outlined", session.NewScene, platform.CommandShortcutLabel("N")),
        Command("Save", "save_outlined", session.Save, platform.CommandShortcutLabel("S")),
        Command("Save as", "save_as_outlined", session.AskForName, platform.CommandShortcutLabel("S", shift: true)),
    ];

    private static PaletteEntry[] Edits(EditorSession session, HostPlatform platform) =>
    [
        Command("Undo", "undo", session.Undo, platform.CommandShortcutLabel("Z"), session.History.UndoLabel ?? ""),
        Command("Redo", "redo", session.Redo, platform.CommandShortcutLabel("Z", shift: true), session.History.RedoLabel ?? ""),
        Command("Copy", "copy", session.Copy, platform.CommandShortcutLabel("C")),
        Command("Cut", "content_cut", session.Cut, platform.CommandShortcutLabel("X")),
        Command("Paste", "content_paste", session.Paste, platform.CommandShortcutLabel("V")),
        Command("Duplicate", "control_point_duplicate", session.Duplicate, platform.CommandShortcutLabel("D")),
        Command("Delete", "delete_outline", session.DeleteSelection, "⌫"),
        Command("Frame selection", "center_focus_strong", session.Frame, "F"),
    ];

    private static PaletteEntry[] Adds(EditorSession session) =>
    [
        .. Enum.GetValues<ShapeKind>().Select(shape => Command($"Add {shape.Label}", "category_outlined", () => session.AddShape(shape), meta: "Shape")),
        .. AddMenu.Items.Select(item => Command($"Add {item.Label}", item.Icon, () => session.Add(item.Kind))),
        Command("Add Terrain", "landscape_outlined", session.NewTerrain),
    ];

    private static PaletteEntry[] Views(EditorSession session) =>
    [
        .. session.Modes.All.Select(mode => Command($"Go to {mode.Label}", mode.Icon, () => session.EnterMode(mode), meta: "Workspace")),
        Command(session.Playing ? "Pause" : "Play", session.Playing ? "pause" : "play_arrow", () => _ = session.TogglePlayback()),
        Command("Stop", "stop", session.StopPlayback),
        Command(session.IsFocused ? "Show panels" : "Focus view", "fullscreen", session.ToggleFocus),
        Command("Reset panels", "view_quilt_outlined", session.ResetPanels),
        Command("Toggle stats", "speed", session.ToggleStats),
    ];

    private static PaletteEntry[] Objects(EditorSession session)
    {
        if (session.Workspace.Loaded?.Scene is not { } scene)
        {
            return [];
        }

        return
        [
            .. scene.Objects.Select(one => new PaletteEntry
            {
                Group = PaletteGroup.Objects,
                Label = one.Name,
                Icon = one.Kind.ToString().ToLowerInvariant(),
                // Where it sits, since a scene of forty crates has forty of the same word and the group
                // is what tells them apart.
                Meta = one.ParentId is { } parent ? scene[parent]?.Name ?? "" : "",
                // Framed as well, since an object found by name may be anywhere, off screen included.
                OnRun = () =>
                {
                    session.Select([one.Id]);
                    session.Frame();
                },
            }),
        ];
    }
}
