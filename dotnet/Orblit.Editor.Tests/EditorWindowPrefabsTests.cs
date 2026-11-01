using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Doc = Orblit.Scene;

// An object is dropped on the browser and on an outliner row as a real drop of what the outliner's drag
// carries, since the drag that starts it is the system's. A prefab is placed through the session: the scene
// view takes a dropped file in 13f. Keys are the session's verbs, which EditorWindowKeysTests binds. Counts
// are read off the scene, because this outliner names how many scenes are open and not how many objects. The
// browser's menu is the one its right click opens, read without opening it, and an answered prompt is
// MakeNew.
public sealed class EditorWindowPrefabsTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_window_prefabs");
    private readonly List<string> _said = [];

    public void Dispose() => _project.Delete(recursive: true);

    private string PathOf(string name) => TestPaths.In(_project, name);

    private EditorSession OpenSession(EditorScene? main = null)
    {
        Directory.CreateDirectory(PathOf("scenes"));
        File.WriteAllText(PathOf("scenes/main.oscene"), SceneDocument.Encode(main ?? EditorScene.Starter()));
        var prefabs = new PrefabFiles(_project.FullName);
        var opened = ProjectWorkspace.Open(_project.FullName, prefabs.Find);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn())
        {
            Prefabs = prefabs,
        };
        session.Said += (text, _) => _said.Add(text);
        return session;
    }

    private (EditorSession Session, EditorWindow Window) Open()
    {
        var session = OpenSession();
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        return (session, window);
    }

    private List<string> Prefabs() => [.. _project.EnumerateFiles("*" + PrefabFiles.Extension, SearchOption.AllDirectories).Select(file => file.FullName)];

    private static string IdOf(EditorSession session, string name) =>
        session.Workspace.Entries.Append(session.Workspace.SharedEntry).SelectMany(entry => entry.Scene?.Objects ?? []).Single(one => one.Name == name).Id;

    private static int ObjectsInScene(EditorSession session) => session.Workspace.Loaded!.Scene!.Objects.Count;

    private static OutlinerPanel Outliner(Window window) => Headless.All<OutlinerPanel>(window).Single();

    private static List<Border> Rows(Window window, string name) =>
    [
        .. Headless.All<Border>(Outliner(window))
            .Where(view => view.Name?.StartsWith("row_", StringComparison.Ordinal) == true && Headless.All<TextBlock>(view).Any(text => text.Text == name)),
    ];

    private static Border Row(Window window, string name) => Rows(window, name).Single();

    private static InspectorPanel Inspector(Window window) => Headless.All<InspectorPanel>(window).Single();

    private static bool HasBand(Window window) => Headless.All<Border>(Inspector(window)).Any(one => one.Name == "prefab_band");

    // What the outliner's drag carries, let go over the middle of a control.
    private static void Drop(Window window, string id, Control on)
    {
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(ObjectDrops.Format, id));
        var at = Headless.Centre(window, on);
        foreach (var step in new[] { RawDragEventType.DragEnter, RawDragEventType.DragOver, RawDragEventType.Drop })
        {
            window.DragDrop(at, step, data, DragDropEffects.Copy | DragDropEffects.Move, RawInputModifiers.None);
        }

        Headless.Render();
    }

    // Selected first, because a prefab is made of the thing in front of you and the inspector has to be
    // showing it for the band that appears afterwards to be worth looking at.
    private static void DropOnBrowser(Window window, EditorSession session, string name)
    {
        Headless.Click(window, Row(window, name));
        Drop(window, IdOf(session, name), Headless.All<AssetGrid>(window).Single());
    }

    private static void DropOnRow(Window window, EditorSession session, string name, string row) =>
        Drop(window, IdOf(session, name), Row(window, row));

    private static void DropInShared(Window window, EditorSession session, string name) =>
        DropOnRow(window, session, name, "In every scene");

    [Fact]
    public Task DroppingAnObjectOnTheProjectMakesOne() => Headless.Run(() =>
    {
        var (session, window) = Open();

        DropOnBrowser(window, session, "Cube");

        var made = Assert.Single(Prefabs());
        Assert.Equal("Cube.oprefab", Path.GetFileName(made));
        Assert.Contains("orblit.prefab", File.ReadAllText(made), StringComparison.Ordinal);
    });

    [Fact]
    public Task WhatItWasMadeFromBecomesAnInstance() => Headless.Run(() =>
    {
        var (session, window) = Open();

        DropOnBrowser(window, session, "Cube");

        Assert.Equal("Cube.oprefab", Headless.Find<TextBlock>(Inspector(window), "prefab_name").Text);
        Assert.True(Headless.Find<Button>(Inspector(window), "prefab_apply").IsEnabled);
        Assert.True(Headless.Find<Button>(Inspector(window), "prefab_unpack").IsEnabled);
        var mark = Assert.Single(Headless.All<Glyph>(Outliner(window)), one => one.Name?.StartsWith("instance_", StringComparison.Ordinal) == true);
        Assert.Equal("Instance of Cube.oprefab", ToolTip.GetTip(mark));
    });

    [Fact]
    public Task MakingOneIsUndoableLinkAndAll() => Headless.Run(() =>
    {
        var (session, window) = Open();
        DropOnBrowser(window, session, "Cube");

        session.Undo();
        Headless.Render();

        Assert.False(HasBand(window));
        // Undo covers the scene, not the project folder, and pretending otherwise would delete somebody's
        // asset behind their back.
        Assert.Single(Prefabs());
    });

    [Fact]
    public Task UnpackingTakesTheLinkOff() => Headless.Run(() =>
    {
        var (session, window) = Open();
        DropOnBrowser(window, session, "Cube");

        Headless.Click(window, Headless.Find<Button>(Inspector(window), "prefab_unpack"));

        Assert.False(HasBand(window));
    });

    [Fact]
    public Task APrefabPlacedComesBack() => Headless.Run(() =>
    {
        var (session, window) = Open();
        DropOnBrowser(window, session, "Cube");

        session.PlacePrefab(Assert.Single(Prefabs()));
        Headless.Render();

        Assert.Single(Rows(window, "Cube"));
        Assert.Single(Rows(window, "Cube 2"));
    });

    [Fact]
    public Task AChangeAppliedReachesTheOtherInstances() => Headless.Run(() =>
    {
        var (session, window) = Open();
        DropOnBrowser(window, session, "Cube");
        var source = Assert.Single(Prefabs());
        session.PlacePrefab(source);
        session.PlacePrefab(source);
        Headless.Render();

        Headless.Click(window, Row(window, "Cube"));
        Headless.Click(window, Headless.Find<Button>(Inspector(window), "prefab_apply"));

        Assert.Contains("orblit.prefab", File.ReadAllText(source), StringComparison.Ordinal);
        Assert.Contains(_said, text => text.Contains("updated 2 other instances", StringComparison.Ordinal));
    });

    [Fact]
    public Task APartStaysInItsInstance() => Headless.Run(() =>
    {
        var (session, window) = Open();
        DropOnBrowser(window, session, "Props");

        DropInShared(window, session, "Crate");

        // Moved out, it would be a change the instance could not be saved as.
        Assert.Contains(_said, text => text.Contains("Unpack that to move its parts out of it", StringComparison.Ordinal));
        Assert.Equal(7, ObjectsInScene(session));
    });

    [Fact]
    public Task TheSceneIsSavedWithTheLinkNotTheParts() => Headless.Run(() =>
    {
        var (session, window) = Open();
        DropOnBrowser(window, session, "Props");

        session.Save();

        var written = File.ReadAllText(PathOf("scenes/main.oscene"));
        Assert.Contains("Props.oprefab", written, StringComparison.Ordinal);
        // What the group holds is in the prefab, and nowhere in the scene.
        Assert.DoesNotContain("Crate", written, StringComparison.Ordinal);
        Assert.Contains("Crate", File.ReadAllText(Assert.Single(Prefabs())), StringComparison.Ordinal);
    });

    [Fact]
    public Task AnObjectCanBeDraggedIntoTheSharedSet() => Headless.Run(() =>
    {
        var (session, window) = Open();

        DropInShared(window, session, "Crate");

        // Still one row, but the scene is one object lighter.
        Assert.Equal("Move Crate", session.History.UndoLabel);
        Assert.Single(Rows(window, "Crate"));
        Assert.Equal(6, ObjectsInScene(session));
    });

    [Fact]
    public Task AndDraggedBackOutOfTheSharedSet() => Headless.Run(() =>
    {
        var (session, window) = Open();
        DropInShared(window, session, "Crate");
        Assert.Equal(6, ObjectsInScene(session));

        DropOnRow(window, session, "Crate", "Props");

        Assert.Single(Rows(window, "Crate"));
        Assert.Equal(7, ObjectsInScene(session));
    });

    [Fact]
    public Task MovingIntoTheSharedSetIsUndoable() => Headless.Run(() =>
    {
        var (session, window) = Open();
        DropInShared(window, session, "Crate");

        session.Undo();
        Headless.Render();

        Assert.Single(Rows(window, "Crate"));
        Assert.Equal(7, ObjectsInScene(session));
    });

    [Fact]
    public Task SomethingInTheSharedSetCanBeCopiedAndPasted() => Headless.Run(async () =>
    {
        var (session, window) = Open();
        DropInShared(window, session, "Crate");

        Headless.Click(window, Row(window, "Crate"));
        await session.CopyAsync();
        await session.PasteAsync();
        Headless.Render();

        // Both in the shared set, since that is where the selection was.
        Assert.Equal(2, Rows(window, "Crate").Count);
        Assert.Equal(2, session.Workspace.Shared.Objects.Count(one => one.Name == "Crate"));
    });

    [Fact]
    public Task SomethingInTheSharedSetCanBeDuplicated() => Headless.Run(() =>
    {
        var (session, window) = Open();
        DropInShared(window, session, "Crate");

        Headless.Click(window, Row(window, "Crate"));
        session.Duplicate();
        Headless.Render();

        Assert.Equal(2, Rows(window, "Crate").Count);
    });

    [Fact]
    public Task PastingStillGoesToTheScene() => Headless.Run(async () =>
    {
        var (session, window) = Open();

        Headless.Click(window, Row(window, "Crate"));
        await session.CopyAsync();
        await session.PasteAsync();
        Headless.Render();

        Assert.Equal(2, Rows(window, "Crate").Count);
    });

    [Fact]
    public Task AnObjectCopiedOutOfTheSharedSetLandsInTheOpenScene() => Headless.Run(async () =>
    {
        var (session, window) = Open();
        DropInShared(window, session, "Sun");

        Headless.Click(window, Row(window, "Sun"));
        await session.CopyAsync();
        // Drawn before the next click, as the copy changed what is on screen and a click lands on what was drawn.
        Headless.Render();
        Headless.Click(window, Row(window, "Props"));
        await session.PasteAsync();
        Headless.Render();

        Assert.Equal(2, Rows(window, "Sun").Count);
        Assert.Contains(session.Workspace.Loaded!.Scene!.Objects, one => one.Name == "Sun");
    });

    [Fact]
    public Task TheSceneItLeftReadsAsUnsavedToo() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.Save();
        Headless.Render();
        Assert.DoesNotContain("•", window.Title, StringComparison.Ordinal);

        DropInShared(window, session, "Crate");

        // Both documents changed, so the one it came out of is a whole object short of what is on disk.
        Assert.EndsWith(" •", window.Title, StringComparison.Ordinal);
    });

    [Fact]
    public Task AWholeSubtreeMovesIntoTheSharedSetAtOnce() => Headless.Run(() =>
    {
        var (session, window) = Open();

        DropInShared(window, session, "Props");

        Assert.Single(Rows(window, "Props"));
        Assert.Single(Rows(window, "Cube"));
        Assert.Single(Rows(window, "Crate"));
        // The group and both its children went together.
        Assert.Equal(4, ObjectsInScene(session));
    });

    private static List<MenuItem> Items(ItemsControl menu) => [.. menu.Items.OfType<MenuItem>()];

    private static MenuItem Group(ContextMenu menu, string label) => Items(menu).Single(item => item.Tag is NewAssetGroup group && group.Label == label);

    private static List<string?> Words(MenuItem item) => [.. Headless.All<TextBlock>((Control)item.Header!).Select(text => text.Text)];

    [Fact]
    public Task TheMenuGroupsWhatItCanMake() => Headless.Run(() =>
    {
        var menu = AssetMenu.Build(_ => { });
        var tags = Items(menu).Select(item => item.Tag).ToList();

        // The common ones stay in front, and the rest are behind a heading.
        Assert.Contains(NewAsset.Folder, tags);
        Assert.Contains(NewAsset.Scene, tags);
        Assert.NotEmpty(Group(menu, "Script").Items);
        Assert.NotEmpty(Group(menu, "Data").Items);
        // Nothing inside a group is on the menu itself.
        Assert.DoesNotContain(NewAsset.Native, tags);
        Assert.DoesNotContain(NewAsset.Json, tags);
    });

    [Fact]
    public Task AGroupOpensToWhatIsInIt() => Headless.Run(() =>
    {
        var script = Items(Group(AssetMenu.Build(_ => { }), "Script"));
        var words = script.SelectMany(Words).ToList();

        Assert.Contains("C++", words);
        Assert.Contains("C++ header", words);
        // And what each one would actually write.
        Assert.Contains(".cpp", words);
    });

    [Fact]
    public Task AFolderIsMadeFromTheMenu() => Headless.Run(() =>
    {
        var (_, window) = Open();

        Headless.All<AssetBrowserPanel>(window).Single().MakeNew(NewAsset.Folder, "props");

        Assert.True(Directory.Exists(PathOf("props")));
    });

    [Fact]
    public Task AScriptIsMadeInsideItsGroup() => Headless.Run(() =>
    {
        var (_, window) = Open();
        var picked = new List<NewAsset>();
        var item = Items(Group(AssetMenu.Build(picked.Add), "Script")).Single(one => Equals(one.Tag, NewAsset.Native));

        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Headless.All<AssetBrowserPanel>(window).Single().MakeNew(Assert.Single(picked), "walker");

        Assert.Contains("orblit_step", File.ReadAllText(PathOf("walker.cpp")), StringComparison.Ordinal);
    });

    [Fact]
    public Task RightClickingAFileOffersWhatToDoWithItToo() => Headless.Run(() =>
    {
        var menu = AssetMenu.Build(_ => { }, new AssetActions(() => { }, () => { }, () => { }, null));
        var tags = Items(menu).Select(item => item.Tag).ToList();

        Assert.Contains("Rename", tags);
        Assert.Contains("Delete", tags);
        // And still everything the empty space offers.
        Assert.NotEmpty(Group(menu, "Script").Items);
    });

    // What the cases above leave unseen: where things go, what is written, and ids that change.

    private string MadeFrom(EditorSession session, string name)
    {
        session.MakePrefab(IdOf(session, name), _project.FullName);
        return Assert.Single(Prefabs());
    }

    private static SceneObject Loaded(EditorSession session, string id) => session.Workspace.Loaded!.Scene![id]!;

    [Fact]
    public void MakingOneKeepsWhatWasSelectedUnderItsNewId()
    {
        var session = OpenSession();
        session.SelectObject("crate");

        session.MakePrefab("props", _project.FullName);

        var crate = IdOf(session, "Crate");
        Assert.NotEqual("crate", crate);
        Assert.Equal(crate, Assert.Single(session.Selected));
        Assert.Equal(crate, session.Primary);
    }

    [Fact]
    public void APrefabIsPlacedUnderTheSelectedGroupOrBesideTheSelectedObject()
    {
        var session = OpenSession();
        var cube = MadeFrom(session, "Cube");

        session.SelectObject("props");
        session.PlacePrefab(cube);
        var inGroup = Loaded(session, session.Primary!);
        session.SelectObject("crate");
        session.PlacePrefab(cube);
        var beside = Loaded(session, session.Primary!);

        Assert.Equal(("Cube 2", "props"), (inGroup.Name, inGroup.ParentId));
        Assert.Equal(("Cube 3", "props"), (beside.Name, beside.ParentId));
    }

    [Fact]
    public void ApplyingFromAPartWritesWhatChangedIntoTheOutermostPrefab()
    {
        var session = OpenSession();
        session.MakePrefab("props", _project.FullName);
        var crate = IdOf(session, "Crate");
        session.History.Run(new Rename(session.Workspace.Loaded!.Id, crate, "Crate", "Barrel"));

        session.ApplyPrefab(crate);

        Assert.Contains("Barrel", File.ReadAllText(Assert.Single(Prefabs())), StringComparison.Ordinal);
        Assert.Contains("Saved Props.oprefab.", _said);
    }

    [Fact]
    public void ApplyingUpdatesAnInstanceInTheSharedSet()
    {
        var session = OpenSession();
        var cube = MadeFrom(session, "Cube");
        session.Move("sun", new Drop(Workspace.SharedSceneId, null, 0));
        session.SelectObject("sun");
        session.PlacePrefab(cube);
        var shared = session.Primary!;
        var red = new Colour(0xFFFF0000);
        session.History.Run(new SetColour(session.Workspace.Loaded!.Id, "cube", "Cube", Loaded(session, "cube").Colour, red));

        session.ApplyPrefab("cube");

        Assert.Equal(red, session.Workspace.Shared[shared]!.Colour);
        Assert.Contains("Saved Cube.oprefab and updated 1 other instance.", _said);
    }

    [Fact]
    public void AnInstanceWhosePrefabCannotBeReadIsOnlyALink()
    {
        var instance = new SceneObject(id: "p", name: "P", kind: ObjectKind.Group, prefab: new Doc.PrefabComponent(asset: "props/gone.oprefab"));
        var session = OpenSession(new EditorScene([instance], name: "Main"));

        session.ApplyPrefab("p");

        Assert.Contains(_said, text => text.EndsWith("Until it is, P is only a link.", StringComparison.Ordinal));
        Assert.Empty(Prefabs());
    }

    [Fact]
    public void ApplyingWhatIsUnchangedAddsNoStep()
    {
        var session = OpenSession();
        MadeFrom(session, "Cube");

        session.ApplyPrefab("cube");

        Assert.Equal("Make prefab Cube", session.History.UndoLabel);
    }

    [Fact]
    public void UnpackingKeepsWhatWasSelectedUnderItsNewId()
    {
        var session = OpenSession();
        session.MakePrefab("props", _project.FullName);
        var part = IdOf(session, "Crate");
        session.SelectObject(part);

        session.UnpackPrefab("props");

        var crate = IdOf(session, "Crate");
        Assert.NotEqual(part, crate);
        Assert.Equal(crate, session.Primary);
    }

    [Fact]
    public Task TheMarkNamesTheFileAndNotItsFolder() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Directory.CreateDirectory(PathOf("prefabs"));

        session.MakePrefab("cube", PathOf("prefabs"));
        Headless.Render();

        var mark = Assert.Single(Headless.All<Glyph>(Outliner(window)), one => one.Name == "instance_cube");
        Assert.Equal("Instance of Cube.oprefab", ToolTip.GetTip(mark));
    });

    [Fact]
    public Task RevertTakesAnInstanceBackToItsPrefab() => Headless.Run(() =>
    {
        var (session, window) = Open();
        DropOnBrowser(window, session, "Cube");
        var was = Loaded(session, "cube").Colour;
        session.History.Run(new SetColour(session.Workspace.Loaded!.Id, "cube", "Cube", was, new Colour(0xFFFF0000)));
        Headless.Render();

        Headless.Click(window, Headless.Find<Button>(Inspector(window), "prefab_revert"));

        Assert.Equal(was, Loaded(session, "cube").Colour);
    });
}
