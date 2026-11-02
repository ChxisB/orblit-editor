using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;

namespace Orblit.Editor.Tests;

/// <summary>The question put before a project's code runs: the careful answer is the one that Enter gives.</summary>
public sealed class TrustDialogTests
{
    private const string Folder = "/projects/crates";

    private static (TrustDialog Dialog, Task<bool> Answer) Open()
    {
        var owner = new Window { Width = 800, Height = 600 };
        owner.Show();
        var answer = TrustDialog.Ask(owner, Folder);
        Headless.Render();
        return (Assert.IsType<TrustDialog>(Assert.Single(owner.OwnedWindows)), answer);
    }

    private static string[] Words(TrustDialog dialog) =>
        [.. Headless.All<TextBlock>(dialog).Select(block => block.Text ?? "")];

    [Fact]
    public Task CancelIsTheDefaultButtonAndTrustIsNot() => Headless.Run(() =>
    {
        var (dialog, _) = Open();

        var cancel = Headless.Find<Button>(dialog, "cancel");
        Assert.True(cancel.IsDefault);
        Assert.True(cancel.IsCancel);
        Assert.False(Headless.Find<Button>(dialog, "trust").IsDefault);
        Assert.Equal("Trust and Play", Headless.Find<Button>(dialog, "trust").Content);
        dialog.Close();
    });

    [Fact]
    public Task EnterAnswersNo() => Headless.Run(() =>
    {
        var (dialog, answer) = Open();

        // The key going down answers, and the dialog is gone before the key can come back up.
        dialog.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Headless.Render();

        Assert.True(answer.IsCompleted);
        Assert.False(answer.Result);
    });

    [Fact]
    public Task CancelAnswersNo() => Headless.Run(() =>
    {
        var (dialog, answer) = Open();

        Headless.Click(dialog, "cancel");
        Headless.Render();

        Assert.True(answer.IsCompleted);
        Assert.False(answer.Result);
    });

    [Fact]
    public Task TrustAndPlayAnswersYes() => Headless.Run(() =>
    {
        var (dialog, answer) = Open();

        Headless.Click(dialog, "trust");
        Headless.Render();

        Assert.True(answer.IsCompleted);
        Assert.True(answer.Result);
    });

    [Fact]
    public Task ClosingTheDialogAnyOtherWayAnswersNo() => Headless.Run(() =>
    {
        var (dialog, answer) = Open();

        dialog.Close();
        Headless.Render();

        Assert.True(answer.IsCompleted);
        Assert.False(answer.Result);
    });

    [Fact]
    public Task ItNamesTheFolderAndSaysTheCodeRunsOnThisComputer() => Headless.Run(() =>
    {
        var (dialog, _) = Open();

        var words = Words(dialog);

        Assert.Contains(Folder, words);
        Assert.Contains(words, word => word.Contains("runs on this computer", StringComparison.Ordinal));
        Assert.Contains(words, word => word.Contains("this folder", StringComparison.Ordinal) && word.Contains("Stop trusting this project", StringComparison.Ordinal));
        dialog.Close();
    });
}
