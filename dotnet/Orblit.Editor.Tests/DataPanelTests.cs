using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// The shell's data objects group is EditorWindowDataTests.
public sealed class DataPanelTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_data_panel");

    public void Dispose() => _project.Delete(recursive: true);

    private string PathOf(string name) => Path.Combine(_project.FullName, name);

    // The blank one: a single number called value, at 0.
    private DataObject Read() => DataObject.Read(File.ReadAllText(PathOf("ball.odata")))!;

    private Window Show(string text)
    {
        File.WriteAllText(PathOf("ball.odata"), text);
        var opened = ProjectWorkspace.Open(_project.FullName, null);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new Window { Width = 400, Height = 800, Content = new DataPanel(session, "ball.odata") };
        window.Show();
        Headless.Render();
        return window;
    }

    private Window Show() => Show(DataObject.Blank("Ball").ToText());

    private static void Type(Window window, string name, string text)
    {
        Headless.Find<TextBox>(window, name).Text = text;
        Headless.Render();
    }

    private static void Add(Window window, string kind)
    {
        var menu = Assert.IsType<MenuFlyout>(Headless.Find<Button>(window, "button_add_a_value").Flyout);
        menu.Items.OfType<MenuItem>().First(one => one.Name == $"add_{kind}").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Headless.Render();
    }

    [Fact]
    public Task ATypedValueWaitsForTheTypingToStop() => Headless.Run(() =>
    {
        var window = Show();

        Type(window, "field_value_0", "3");

        Assert.Equal(0, Read().Fields[0].AsNumber);
        Assert.True(Headless.Until(() => Read().Fields[0].AsNumber == 3));
    });

    [Theory]
    [InlineData("abc")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public Task TextThatIsNotANumberKeepsTheLastOne(string text) => Headless.Run(() =>
    {
        var window = Show();

        Type(window, "field_value_0", text);
        Type(window, "field_key_0", "speed");

        // The change after it still lands. A number JSON cannot hold would make every later write fail.
        Assert.True(Headless.Until(() => Read().Fields[0].Key == "speed"));
        Assert.Equal(0, Read().Fields[0].AsNumber);
    });

    [Fact]
    public Task AnEmptyKeyIsNotWritten() => Headless.Run(() =>
    {
        var window = Show();

        Type(window, "field_key_0", "  ");
        Headless.Wait(400);

        Assert.Equal("value", Read().Fields[0].Key);
    });

    [Fact]
    public Task AValueAddedGetsTheKindPickedAndANameOfItsOwn() => Headless.Run(() =>
    {
        var window = Show();

        Add(window, "toggle");

        // Its own card at once, and the file once the typing would have stopped.
        Headless.Find<Control>(window, "field_key_1");
        Assert.True(Headless.Until(() => Read().Fields.Count == 2));
        Assert.Equal(new DataField("toggle", DataType.Toggle, false), Read().Fields[1]);
    });

    [Fact]
    public Task OneAxisOfAVectorChangesAlone() => Headless.Run(() =>
    {
        var window = Show();
        Add(window, "vector");

        Type(window, "field_y_1", "2");

        Assert.True(Headless.Until(() => Read().Fields is [_, { AsVector.Y: 2 }]));
        Assert.Equal(0, Read().Fields[1].AsVector.X);
    });

    [Fact]
    public Task RemovingAValueTakesItOut() => Headless.Run(() =>
    {
        var window = Show();

        Headless.Click(window, "field_remove_0");

        Assert.True(Headless.Until(() => Read().Fields.Count == 0));
    });

    [Fact]
    public Task WhatIsStillWaitingIsWrittenWhenThePanelCloses() => Headless.Run(() =>
    {
        var window = Show();
        Type(window, "field_value_0", "7");

        window.Close();

        Assert.Equal(7, Read().Fields[0].AsNumber);
    });

    [Fact]
    public Task TheBindingsAreOfWhatWasJustTyped() => Headless.Run(() =>
    {
        var window = Show();
        Type(window, "field_key_0", "speed");

        Headless.Click(window, "button_write_script_bindings");

        Assert.Contains("inline double speed(", File.ReadAllText(PathOf("ball.h")), StringComparison.Ordinal);
    });

    [Fact]
    public Task AFileThatCannotBeReadSaysSo() => Headless.Run(() =>
    {
        var window = Show("nonsense");

        Assert.Equal("ball.odata could not be read as a data object.", Headless.Find<TextBlock>(window, "data_unreadable").Text);
    });
}
