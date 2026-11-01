namespace Orblit.Editor.Core;

/// <summary>What a keystroke means, before anything decides what to do about it.</summary>
public enum EditorVerb
{
    Undo,
    Redo,
    Delete,
    Frame,
    Save,
    SaveAs,
    NewScene,
    Palette,
    Copy,
    Cut,
    Paste,
    Duplicate,

    /// <summary>Back out of a mesh, to the object it belongs to.</summary>
    LeaveEdit,

    /// <summary>Finishes whatever is being drawn.</summary>
    FinishDraw,

    /// <summary>Goes round the three ways of selecting part of a mesh.</summary>
    CycleMode,

    GridCoarser,
    GridFiner,

    /// <summary>Moves the selection along an axis by whole squares.</summary>
    NudgeX,
    NudgeY,
    NudgeZ,
}

/// <param name="Verb">What to do.</param>
/// <param name="Squares">For a nudge, how many squares, signed. Zero for every other verb.</param>
public readonly record struct ShortcutAction(EditorVerb Verb, int Squares = 0);

public static class ShortcutMap
{
    private static readonly (EditorKey Key, EditorVerb Verb, int Sign)[] Arrows =
    [
        (EditorKey.ArrowLeft, EditorVerb.NudgeX, -1),
        (EditorKey.ArrowRight, EditorVerb.NudgeX, 1),
        (EditorKey.ArrowUp, EditorVerb.NudgeZ, -1),
        (EditorKey.ArrowDown, EditorVerb.NudgeZ, 1),
    ];

    /// <summary>Which keystroke means which verb on <paramref name="platform"/>.</summary>
    public static IReadOnlyDictionary<KeyChord, ShortcutAction> For(HostPlatform platform)
    {
        var map = new Dictionary<KeyChord, ShortcutAction>
        {
            [platform.CommandShortcut(EditorKey.KeyZ)] = new(EditorVerb.Undo),
            [platform.CommandShortcut(EditorKey.KeyZ, shift: true)] = new(EditorVerb.Redo),
            [new KeyChord(EditorKey.Delete)] = new(EditorVerb.Delete),
            [new KeyChord(EditorKey.Backspace)] = new(EditorVerb.Delete),
            [new KeyChord(EditorKey.KeyF)] = new(EditorVerb.Frame),

            // The two keys a modelling tool has. Escape comes back out of the geometry and G goes
            // round the three ways of selecting it, which is what somebody presses without thinking.
            [new KeyChord(EditorKey.Escape)] = new(EditorVerb.LeaveEdit),
            [new KeyChord(EditorKey.Enter)] = new(EditorVerb.FinishDraw),
            [new KeyChord(EditorKey.NumpadEnter)] = new(EditorVerb.FinishDraw),
            [new KeyChord(EditorKey.KeyG)] = new(EditorVerb.CycleMode),

            // The brackets, which is where every tool with a brush size puts them.
            [new KeyChord(EditorKey.BracketRight)] = new(EditorVerb.GridCoarser),
            [new KeyChord(EditorKey.BracketLeft)] = new(EditorVerb.GridFiner),

            [platform.CommandShortcut(EditorKey.KeyS)] = new(EditorVerb.Save),
            [platform.CommandShortcut(EditorKey.KeyS, shift: true)] = new(EditorVerb.SaveAs),
            [platform.CommandShortcut(EditorKey.KeyN)] = new(EditorVerb.NewScene),
            [platform.CommandShortcut(EditorKey.KeyK)] = new(EditorVerb.Palette),
            [platform.CommandShortcut(EditorKey.KeyC)] = new(EditorVerb.Copy),
            [platform.CommandShortcut(EditorKey.KeyX)] = new(EditorVerb.Cut),
            [platform.CommandShortcut(EditorKey.KeyV)] = new(EditorVerb.Paste),
            [platform.CommandShortcut(EditorKey.KeyD)] = new(EditorVerb.Duplicate),
        };

        // Windows and Linux both also expect Ctrl+Y for redo, beside the Ctrl+Shift+Z above. macOS
        // has no second convention to match.
        if (!platform.CommandIsMeta())
        {
            map[new KeyChord(EditorKey.KeyY, Modifiers.Control)] = new(EditorVerb.Redo);
        }

        AddNudges(map);
        return map;
    }

    // Whole squares at a time, which is the one way of placing something that needs no aim at all.
    // The arrows work the floor, because that is where things are arranged. Shift takes them up and
    // down, and holding alt does ten at once. Built rather than written out: four directions times
    // three modifiers is twelve lines that say the same thing, and one of them would be wrong.
    private static void AddNudges(Dictionary<KeyChord, ShortcutAction> map)
    {
        foreach (var (key, verb, sign) in Arrows)
        {
            map[new KeyChord(key)] = new(verb, sign);
            map[new KeyChord(key, Modifiers.Alt)] = new(verb, sign * 10);
        }

        // Up and down are the exception: there is no arrow for them, so shift turns the near and far
        // pair into a high and low one.
        map[new KeyChord(EditorKey.ArrowUp, Modifiers.Shift)] = new(EditorVerb.NudgeY, 1);
        map[new KeyChord(EditorKey.ArrowDown, Modifiers.Shift)] = new(EditorVerb.NudgeY, -1);
        map[new KeyChord(EditorKey.ArrowUp, Modifiers.Shift | Modifiers.Alt)] = new(EditorVerb.NudgeY, 10);
        map[new KeyChord(EditorKey.ArrowDown, Modifiers.Shift | Modifiers.Alt)] = new(EditorVerb.NudgeY, -10);
    }
}
