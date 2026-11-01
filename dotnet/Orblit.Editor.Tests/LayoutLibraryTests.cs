namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// The shell tests reach the library through the View menu. These reach it without a window.
public sealed class LayoutLibraryTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("orblit_layouts").FullName;

    private string File => Path.Combine(_directory, "project", "layouts.json");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private LayoutLibrary Open(IEnumerable<PanelKind>? kinds = null) => new(File, kinds ?? PanelKind.BuiltIn);

    [Fact]
    public void ASavedArrangementIsFoundAgainUnderItsWorkspaceAndName()
    {
        var library = Open();
        library.Save("animation", "Four cameras", DockLayout.FourViews());

        Assert.Equal(4, library.Find("animation", "Four cameras")?.Panels.Count(one => one.Kind.Equals(PanelKind.Viewport)));
        Assert.Null(library.Find("scene", "Four cameras"));
        Assert.Null(library.Find("animation", "Nothing"));
    }

    [Fact]
    public void ASavedArrangementIsStillThereAfterReopening()
    {
        Open().Save("animation", "Four cameras", DockLayout.FourViews().CopyWith(locked: true));

        var again = Open().Find("animation", "Four cameras");

        Assert.True(again?.Locked);
        Assert.Equal(["Four cameras"], Open().Names("animation"));
    }

    [Fact]
    public void NamesAreListedInOrder()
    {
        var library = Open();
        library.Save("scene", "b", DockLayout.Standard());
        library.Save("scene", "a", DockLayout.Standard());
        library.Save("scene", "C", DockLayout.Standard());

        Assert.Equal(["C", "a", "b"], library.Names("scene"));
        Assert.Empty(library.Names("terrain"));
    }

    [Fact]
    public void RemovingOneLeavesTheOthersAndTheOtherWorkspaces()
    {
        var library = Open();
        library.Save("scene", "a", DockLayout.Standard());
        library.Save("scene", "b", DockLayout.Standard());
        library.Save("animation", "a", DockLayout.Animation());

        library.Remove("scene", "a");

        Assert.Equal(["b"], Open().Names("scene"));
        Assert.Equal(["a"], Open().Names("animation"));
    }

    [Fact]
    public void ADamagedFileDoesNotStopTheProjectOpening()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!); // The test just named the directory.
        System.IO.File.WriteAllText(File, "{ not json");

        var library = Open();
        Assert.Empty(library.Names("scene"));

        library.Save("scene", "a", DockLayout.Standard());
        Assert.Equal(["a"], Open().Names("scene"));
    }

    [Fact]
    public void AFileInTheStoredShapeIsRead()
    {
        // A library with one saved arrangement, compact and with no revision field.
        Directory.CreateDirectory(Path.GetDirectoryName(File)!); // The test just named the directory.
        System.IO.File.WriteAllText(
            File,
            """{"modelling":{"mine":{"kind":"orblit.layout","formatVersion":1,"locked":false,"root":{"id":"g","active":0,"panels":[{"id":"a","kind":"outliner"}]}}}}""");

        var found = Open().Find("modelling", "mine");

        Assert.Equal(["a"], found?.Panels.Select(one => one.Id));
        Assert.Equal(0, found?.Revision);
    }

    [Fact]
    public void ANewWriteLeavesNoTemporaryFileBehind()
    {
        Open().Save("scene", "a", DockLayout.Standard());

        Assert.Equal([File], Directory.GetFiles(Path.GetDirectoryName(File)!));
    }
}
