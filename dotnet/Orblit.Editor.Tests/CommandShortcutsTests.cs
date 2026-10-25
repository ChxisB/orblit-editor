namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

public sealed class CommandShortcutsTests
{
    [Fact]
    public void CommandIsMetaTrueOnMacOs() => Assert.True(HostPlatform.MacOS.CommandIsMeta());

    [Theory]
    [InlineData(HostPlatform.Windows)]
    [InlineData(HostPlatform.Linux)]
    [InlineData(HostPlatform.Android)]
    [InlineData(HostPlatform.IOS)]
    [InlineData(HostPlatform.Fuchsia)]
    public void CommandIsMetaFalseOnPlatform(HostPlatform platform) => Assert.False(platform.CommandIsMeta());

    [Fact]
    public void CommandShortcutBindsMetaOnMacOsNotControl()
    {
        var chord = HostPlatform.MacOS.CommandShortcut(EditorKey.KeyS);

        Assert.Equal(EditorKey.KeyS, chord.Key);
        Assert.Equal(Modifiers.Meta, chord.Modifiers);
    }

    [Fact]
    public void CommandShortcutBindsControlOnWindowsNotMeta()
    {
        var chord = HostPlatform.Windows.CommandShortcut(EditorKey.KeyS);

        Assert.Equal(EditorKey.KeyS, chord.Key);
        Assert.Equal(Modifiers.Control, chord.Modifiers);
    }

    [Fact]
    public void CommandShortcutBindsControlOnLinux() =>
        Assert.Equal(Modifiers.Control, HostPlatform.Linux.CommandShortcut(EditorKey.KeyS).Modifiers);

    [Fact]
    public void CommandShortcutCarriesShiftAndAltThroughOnTopOfThePlatformModifier() =>
        Assert.Equal(
            Modifiers.Control | Modifiers.Shift | Modifiers.Alt,
            HostPlatform.Windows.CommandShortcut(EditorKey.KeyS, shift: true, alt: true).Modifiers);

    [Fact]
    public void CommandKeyLabelTheCommandGlyphOnMacOs() => Assert.Equal("⌘", HostPlatform.MacOS.CommandKeyLabel());

    [Fact]
    public void CommandKeyLabelCtrlOnWindowsAndLinux()
    {
        Assert.Equal("Ctrl", HostPlatform.Windows.CommandKeyLabel());
        Assert.Equal("Ctrl", HostPlatform.Linux.CommandKeyLabel());
    }

    [Fact]
    public void CommandShortcutLabelMacOsTheKeyAloneOrShiftImmediatelyBeforeIt()
    {
        Assert.Equal("⌘S", HostPlatform.MacOS.CommandShortcutLabel("S"));
        Assert.Equal("⇧⌘S", HostPlatform.MacOS.CommandShortcutLabel("S", shift: true));
    }

    [Fact]
    public void CommandShortcutLabelOffMacOsCtrlAndWithShiftCtrlShift()
    {
        Assert.Equal("Ctrl+S", HostPlatform.Windows.CommandShortcutLabel("S"));
        Assert.Equal("Ctrl+Shift+S", HostPlatform.Windows.CommandShortcutLabel("S", shift: true));
    }

    // The Dart tests hold the key down in a live keyboard binding. The model only needs to know which
    // keys are down, so these pass them in.
    [Fact]
    public void IsCommandModifierPressedReadsMetaOnMacOs()
    {
        Assert.False(HostPlatform.MacOS.IsCommandModifierPressed(Modifiers.None));
        Assert.True(HostPlatform.MacOS.IsCommandModifierPressed(Modifiers.Meta));

        // Control is not Command on macOS.
        Assert.False(HostPlatform.MacOS.IsCommandModifierPressed(Modifiers.Control));
    }

    [Fact]
    public void IsCommandModifierPressedReadsControlOffMacOs()
    {
        Assert.False(HostPlatform.Windows.IsCommandModifierPressed(Modifiers.None));
        Assert.True(HostPlatform.Windows.IsCommandModifierPressed(Modifiers.Control));

        // Meta is not Command on Windows.
        Assert.False(HostPlatform.Windows.IsCommandModifierPressed(Modifiers.Meta));
    }

    [Fact]
    public void IsCommandModifierPressedStillSeesCommandWhenShiftIsHeldToo() =>
        Assert.True(HostPlatform.MacOS.IsCommandModifierPressed(Modifiers.Meta | Modifiers.Shift));
}
