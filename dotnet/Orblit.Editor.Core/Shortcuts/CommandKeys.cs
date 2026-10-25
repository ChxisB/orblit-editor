namespace Orblit.Editor.Core;

/// <summary>The platforms the editor reasons about when it picks a shortcut.</summary>
public enum HostPlatform
{
    MacOS,
    Windows,
    Linux,
    Android,
    IOS,
    Fuchsia,
}

/// <summary>The modifier keys a shortcut can ask for.</summary>
[Flags]
public enum Modifiers
{
    None = 0,
    Control = 1,
    Meta = 2,
    Shift = 4,
    Alt = 8,
}

/// <summary>
/// The keys the editor binds, named as the keyboard names them. The shell turns its toolkit's key
/// into one of these, so the map of shortcuts needs no UI type.
/// </summary>
public enum EditorKey
{
    KeyC,
    KeyD,
    KeyF,
    KeyG,
    KeyK,
    KeyN,
    KeyS,
    KeyV,
    KeyX,
    KeyY,
    KeyZ,
    Delete,
    Backspace,
    Escape,
    Enter,
    NumpadEnter,
    BracketLeft,
    BracketRight,
    ArrowLeft,
    ArrowRight,
    ArrowUp,
    ArrowDown,
}

/// <summary>A key together with the modifiers that must be held with it.</summary>
public readonly record struct KeyChord(EditorKey Key, Modifiers Modifiers = Modifiers.None);

public static class HostPlatforms
{
    public static HostPlatform Current =>
        OperatingSystem.IsMacOS() ? HostPlatform.MacOS
        : OperatingSystem.IsWindows() ? HostPlatform.Windows
        : OperatingSystem.IsAndroid() ? HostPlatform.Android
        : OperatingSystem.IsIOS() ? HostPlatform.IOS
        : HostPlatform.Linux;

    /// <summary>
    /// Whether the platform's "Command" modifier is macOS's Meta key.
    /// <para>
    /// False means Control: Windows and Linux both bind what macOS puts on Command to Control
    /// instead, and there is no supported platform where the answer is anything else.
    /// </para>
    /// </summary>
    public static bool CommandIsMeta(this HostPlatform platform) => platform == HostPlatform.MacOS;

    /// <summary>The label for the Command modifier, for menus and tooltips.</summary>
    public static string CommandKeyLabel(this HostPlatform platform) => platform.CommandIsMeta() ? "⌘" : "Ctrl";

    /// <summary>
    /// A shortcut on the Command modifier, with whichever of <paramref name="shift"/> and
    /// <paramref name="alt"/> are asked for besides.
    /// <para>
    /// The one place that decides what "Command" means on a platform, so nothing else changes when
    /// another platform joins macOS, Windows and Linux.
    /// </para>
    /// </summary>
    public static KeyChord CommandShortcut(
        this HostPlatform platform,
        EditorKey trigger,
        bool shift = false,
        bool alt = false)
    {
        var modifiers = platform.CommandIsMeta() ? Modifiers.Meta : Modifiers.Control;
        if (shift)
        {
            modifiers |= Modifiers.Shift;
        }

        if (alt)
        {
            modifiers |= Modifiers.Alt;
        }

        return new KeyChord(trigger, modifiers);
    }

    /// <summary>
    /// The text for a Command shortcut, matched to what <see cref="CommandShortcut"/> binds: "⇧⌘S" on
    /// macOS, where Apple puts Shift immediately before Command, and "Ctrl+Shift+S" elsewhere.
    /// </summary>
    public static string CommandShortcutLabel(this HostPlatform platform, string key, bool shift = false) =>
        platform.CommandIsMeta()
            ? $"{(shift ? "⇧" : "")}{platform.CommandKeyLabel()}{key}"
            : $"{platform.CommandKeyLabel()}+{(shift ? "Shift+" : "")}{key}";

    /// <summary>
    /// Whether the Command modifier is among the keys <paramref name="held"/>.
    /// <para>
    /// For a click that adds to the selection rather than replacing it. Command-click on macOS is
    /// Control-click everywhere else, on the same reasoning as <see cref="CommandShortcut"/>.
    /// </para>
    /// </summary>
    public static bool IsCommandModifierPressed(this HostPlatform platform, Modifiers held) =>
        held.HasFlag(platform.CommandIsMeta() ? Modifiers.Meta : Modifiers.Control);
}
