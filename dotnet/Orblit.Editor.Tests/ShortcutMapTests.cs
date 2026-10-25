namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

public sealed class ShortcutMapTests
{
    private static ShortcutAction At(HostPlatform platform, EditorKey key, Modifiers modifiers = Modifiers.None) =>
        ShortcutMap.For(platform)[new KeyChord(key, modifiers)];

    [Fact]
    public void CommandKeysUseMetaOnMacOsAndControlElsewhere()
    {
        Assert.Equal(EditorVerb.Undo, At(HostPlatform.MacOS, EditorKey.KeyZ, Modifiers.Meta).Verb);
        Assert.Equal(EditorVerb.Undo, At(HostPlatform.Windows, EditorKey.KeyZ, Modifiers.Control).Verb);
        Assert.False(ShortcutMap.For(HostPlatform.MacOS).ContainsKey(new KeyChord(EditorKey.KeyZ, Modifiers.Control)));
        Assert.False(ShortcutMap.For(HostPlatform.Linux).ContainsKey(new KeyChord(EditorKey.KeyZ, Modifiers.Meta)));
    }

    [Fact]
    public void ShiftAddsRedoAndSaveAs()
    {
        var shift = Modifiers.Meta | Modifiers.Shift;

        Assert.Equal(EditorVerb.Redo, At(HostPlatform.MacOS, EditorKey.KeyZ, shift).Verb);
        Assert.Equal(EditorVerb.SaveAs, At(HostPlatform.MacOS, EditorKey.KeyS, shift).Verb);
    }

    [Fact]
    public void ControlYIsRedoOffMacOsOnly()
    {
        var chord = new KeyChord(EditorKey.KeyY, Modifiers.Control);

        Assert.Equal(EditorVerb.Redo, ShortcutMap.For(HostPlatform.Windows)[chord].Verb);
        Assert.Equal(EditorVerb.Redo, ShortcutMap.For(HostPlatform.Linux)[chord].Verb);
        Assert.False(ShortcutMap.For(HostPlatform.MacOS).ContainsKey(chord));
    }

    [Fact]
    public void DeleteAndBackspaceBothDelete()
    {
        Assert.Equal(EditorVerb.Delete, At(HostPlatform.Linux, EditorKey.Delete).Verb);
        Assert.Equal(EditorVerb.Delete, At(HostPlatform.Linux, EditorKey.Backspace).Verb);
    }

    [Fact]
    public void BothEnterKeysFinishADrawing()
    {
        Assert.Equal(EditorVerb.FinishDraw, At(HostPlatform.Linux, EditorKey.Enter).Verb);
        Assert.Equal(EditorVerb.FinishDraw, At(HostPlatform.Linux, EditorKey.NumpadEnter).Verb);
    }

    [Fact]
    public void TheBracketsMakeTheGridCoarserAndFiner()
    {
        Assert.Equal(EditorVerb.GridCoarser, At(HostPlatform.Linux, EditorKey.BracketRight).Verb);
        Assert.Equal(EditorVerb.GridFiner, At(HostPlatform.Linux, EditorKey.BracketLeft).Verb);
    }

    [Fact]
    public void TheArrowsWorkTheFloor()
    {
        Assert.Equal(new ShortcutAction(EditorVerb.NudgeX, -1), At(HostPlatform.Linux, EditorKey.ArrowLeft));
        Assert.Equal(new ShortcutAction(EditorVerb.NudgeX, 1), At(HostPlatform.Linux, EditorKey.ArrowRight));
        Assert.Equal(new ShortcutAction(EditorVerb.NudgeZ, -1), At(HostPlatform.Linux, EditorKey.ArrowUp));
        Assert.Equal(new ShortcutAction(EditorVerb.NudgeZ, 1), At(HostPlatform.Linux, EditorKey.ArrowDown));
    }

    [Fact]
    public void AltDoesTenSquaresAtOnce()
    {
        Assert.Equal(new ShortcutAction(EditorVerb.NudgeX, -10), At(HostPlatform.Linux, EditorKey.ArrowLeft, Modifiers.Alt));
        Assert.Equal(new ShortcutAction(EditorVerb.NudgeZ, 10), At(HostPlatform.Linux, EditorKey.ArrowDown, Modifiers.Alt));
    }

    [Fact]
    public void ShiftTakesUpAndDownAsHighAndLow()
    {
        Assert.Equal(new ShortcutAction(EditorVerb.NudgeY, 1), At(HostPlatform.Linux, EditorKey.ArrowUp, Modifiers.Shift));
        Assert.Equal(new ShortcutAction(EditorVerb.NudgeY, -1), At(HostPlatform.Linux, EditorKey.ArrowDown, Modifiers.Shift));
        Assert.Equal(
            new ShortcutAction(EditorVerb.NudgeY, 10),
            At(HostPlatform.Linux, EditorKey.ArrowUp, Modifiers.Shift | Modifiers.Alt));
        Assert.Equal(
            new ShortcutAction(EditorVerb.NudgeY, -10),
            At(HostPlatform.Linux, EditorKey.ArrowDown, Modifiers.Shift | Modifiers.Alt));
    }

    [Fact]
    public void OnlyANudgeCarriesSquares()
    {
        var nudges = new[] { EditorVerb.NudgeX, EditorVerb.NudgeY, EditorVerb.NudgeZ };

        foreach (var action in ShortcutMap.For(HostPlatform.MacOS).Values.Where(one => !nudges.Contains(one.Verb)))
        {
            Assert.Equal(0, action.Squares);
        }
    }

    [Fact]
    public void EveryVerbHasAKey()
    {
        var bound = ShortcutMap.For(HostPlatform.MacOS).Values.Select(action => action.Verb).ToHashSet();

        Assert.Equal(Enum.GetValues<EditorVerb>().ToHashSet(), bound);
    }

    [Fact]
    public void KeepsThirtyOneChordsOnMacOsAndThirtyTwoElsewhere()
    {
        Assert.Equal(31, ShortcutMap.For(HostPlatform.MacOS).Count);
        Assert.Equal(32, ShortcutMap.For(HostPlatform.Windows).Count);
    }
}
