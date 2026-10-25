namespace Orblit.Editor.Core;

/// <summary>
/// What the command palette can find: the commands the menus and the keyboard already have, and the
/// objects in the scene.
/// </summary>
/// <remarks>
/// shortcut: only the commands the session can do so far. The add, delete, copy and frame commands
/// join them with the panels that need them (task 16) and the viewport (task 15).
/// </remarks>
public static class PaletteEntries
{
    public static IReadOnlyList<PaletteEntry> For(EditorSession session, HostPlatform platform) =>
    [
        .. Files(session, platform),
        .. Edits(session, platform),
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
        Command("Save", "save_outlined", session.Save, platform.CommandShortcutLabel("S")),
    ];

    private static PaletteEntry[] Edits(EditorSession session, HostPlatform platform) =>
    [
        Command("Undo", "undo", session.Undo, platform.CommandShortcutLabel("Z"), session.History.UndoLabel ?? ""),
        Command("Redo", "redo", session.Redo, platform.CommandShortcutLabel("Z", shift: true), session.History.RedoLabel ?? ""),
    ];

    private static PaletteEntry[] Views(EditorSession session) =>
    [
        .. session.Modes.All.Select(mode => Command($"Go to {mode.Label}", mode.Icon, () => session.EnterMode(mode), meta: "Workspace")),
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
                OnRun = () => session.Select([one.Id]),
            }),
        ];
    }
}
