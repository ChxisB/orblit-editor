using Avalonia.Input;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>Turns the toolkit's keys into the core's, and back for the text a menu shows.</summary>
public static class KeyMapping
{
    private static readonly (EditorKey Editor, Key Toolkit)[] Keys =
    [
        (EditorKey.KeyC, Key.C),
        (EditorKey.KeyD, Key.D),
        (EditorKey.KeyF, Key.F),
        (EditorKey.KeyG, Key.G),
        (EditorKey.KeyK, Key.K),
        (EditorKey.KeyN, Key.N),
        (EditorKey.KeyS, Key.S),
        (EditorKey.KeyV, Key.V),
        (EditorKey.KeyX, Key.X),
        (EditorKey.KeyY, Key.Y),
        (EditorKey.KeyZ, Key.Z),
        (EditorKey.Delete, Key.Delete),
        (EditorKey.Backspace, Key.Back),
        (EditorKey.Escape, Key.Escape),
        (EditorKey.Enter, Key.Enter),
        (EditorKey.BracketLeft, Key.OemOpenBrackets),
        (EditorKey.BracketRight, Key.OemCloseBrackets),
        (EditorKey.ArrowLeft, Key.Left),
        (EditorKey.ArrowRight, Key.Right),
        (EditorKey.ArrowUp, Key.Up),
        (EditorKey.ArrowDown, Key.Down),
    ];

    private static readonly (Modifiers Editor, KeyModifiers Toolkit)[] Held =
    [
        (Modifiers.Control, KeyModifiers.Control),
        (Modifiers.Meta, KeyModifiers.Meta),
        (Modifiers.Shift, KeyModifiers.Shift),
        (Modifiers.Alt, KeyModifiers.Alt),
    ];

    /// <summary>The chord for a key press, or null when it is a key the editor binds nothing to.</summary>
    public static KeyChord? ChordOf(KeyEventArgs press)
    {
        // The toolkit reports the numeric keypad's Enter as Enter. The physical key tells them apart.
        EditorKey? key = press.PhysicalKey == PhysicalKey.NumPadEnter
            ? EditorKey.NumpadEnter
            : Keys.Where(pair => pair.Toolkit == press.Key).Select(pair => (EditorKey?)pair.Editor).FirstOrDefault();
        if (key is not { } found)
        {
            return null;
        }

        var modifiers = Held.Where(pair => press.KeyModifiers.HasFlag(pair.Toolkit)).Aggregate(
            Modifiers.None,
            (all, pair) => all | pair.Editor);
        return new KeyChord(found, modifiers);
    }

    /// <summary>The gesture a menu item shows beside its label.</summary>
    public static KeyGesture GestureOf(KeyChord chord)
    {
        var key = chord.Key == EditorKey.NumpadEnter ? Key.Enter : Keys.First(pair => pair.Editor == chord.Key).Toolkit;
        var modifiers = Held.Where(pair => chord.Modifiers.HasFlag(pair.Editor)).Aggregate(
            KeyModifiers.None,
            (all, pair) => all | pair.Toolkit);
        return new KeyGesture(key, modifiers);
    }
}
