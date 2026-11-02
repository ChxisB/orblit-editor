namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

/// <summary>The device a project's game view shows, kept in the project's editor folder.</summary>
public sealed class DevicePreferenceTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("orblit_device_preference");

    public void Dispose() => _folder.Delete(recursive: true);

    private string File => Path.Combine(_folder.FullName, ".orblit", "device.json");

    [Fact]
    public void ANewProjectStartsOnTheDesktop()
    {
        Assert.Equal(DeviceFrame.Desktop, new DevicePreference(File).Frame);
    }

    [Fact]
    public void AChosenPhoneComesBackHeldUpright()
    {
        new DevicePreference(File).Choose(DeviceKind.Mobile);

        Assert.Equal(new DeviceFrame(DeviceKind.Mobile, FrameOrientation.Portrait), new DevicePreference(File).Frame);
    }

    [Fact]
    public void ATurnIsRemembered()
    {
        var first = new DevicePreference(File);
        first.Choose(DeviceKind.Tablet);
        first.Turn();

        Assert.Equal(new DeviceFrame(DeviceKind.Tablet, FrameOrientation.Landscape), new DevicePreference(File).Frame);
    }

    [Fact]
    public void HoldingAPhoneSidewaysIsRememberedAndHoldingItAgainChangesNothing()
    {
        var first = new DevicePreference(File);
        first.Choose(DeviceKind.Mobile);
        first.Hold(FrameOrientation.Landscape);
        var raised = 0;
        first.Changed += () => raised++;

        first.Hold(FrameOrientation.Landscape);

        Assert.Equal(0, raised);
        Assert.Equal(new DeviceFrame(DeviceKind.Mobile, FrameOrientation.Landscape), new DevicePreference(File).Frame);
    }

    [Fact]
    public void ADesktopTurnChangesNothing()
    {
        var preference = new DevicePreference(File);
        var raised = 0;
        preference.Changed += () => raised++;

        preference.Turn();

        Assert.Equal((DeviceFrame.Desktop, 0), (preference.Frame, raised));
        Assert.False(System.IO.File.Exists(File));
    }

    [Fact]
    public void AGoThroughTheDesktopKeepsTheWayThePhoneWasHeld()
    {
        var preference = new DevicePreference(File);
        preference.Choose(DeviceKind.Mobile);
        preference.Turn();

        preference.Choose(DeviceKind.Desktop);
        preference.Choose(DeviceKind.Tablet);

        Assert.Equal(new DeviceFrame(DeviceKind.Tablet, FrameOrientation.Landscape), preference.Frame);
    }

    [Fact]
    public void TheWayAPhoneWasHeldSurvivesAReopenOnTheDesktop()
    {
        var first = new DevicePreference(File);
        first.Choose(DeviceKind.Mobile);
        first.Turn();
        first.Choose(DeviceKind.Desktop);

        var reopened = new DevicePreference(File);
        reopened.Choose(DeviceKind.Mobile);

        Assert.Equal(new DeviceFrame(DeviceKind.Mobile, FrameOrientation.Landscape), reopened.Frame);
    }

    [Fact]
    public void ChoosingTheDeviceAlreadyShownRaisesNothing()
    {
        var preference = new DevicePreference(File);
        preference.Choose(DeviceKind.Mobile);
        var raised = 0;
        preference.Changed += () => raised++;

        preference.Choose(DeviceKind.Mobile);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void AChangeRaisesOnce()
    {
        var preference = new DevicePreference(File);
        var raised = 0;
        preference.Changed += () => raised++;

        preference.Choose(DeviceKind.Mobile);
        preference.Turn();

        Assert.Equal(2, raised);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"device":"watch","orientation":"portrait"}""")]
    [InlineData("""{"device":"mobile","orientation":"sideways"}""")]
    public void AFileThatMakesNoSenseLeavesTheDesktop(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, text);

        Assert.Equal(DeviceFrame.Desktop, new DevicePreference(File).Frame);
    }

    [Fact]
    public void AChoiceThatCannotBeWrittenStillHoldsForTheRun()
    {
        // A file where the editor's folder should be makes the write fail.
        System.IO.File.WriteAllText(Path.Combine(_folder.FullName, ".orblit"), "in the way");
        var preference = new DevicePreference(File);

        preference.Choose(DeviceKind.Mobile);

        Assert.Equal(DeviceKind.Mobile, preference.Frame.Kind);
    }
}
