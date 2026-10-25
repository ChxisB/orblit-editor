using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

public sealed class LauncherWindowTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 25, 9, 0, 0);

    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_test");
    private readonly FixedClock _clock = new(Now);
    private readonly ProjectStore _store;
    private readonly List<Project> _opened = [];
    private string? _picked;

    public LauncherWindowTests() => _store = new ProjectStore(Path.Combine(_temp.FullName, "support", "recent.json"), _clock);

    public void Dispose() => _temp.Delete(recursive: true);

    private static IEnumerable<T> All<T>(Control root) => root.GetLogicalDescendants().OfType<T>();

    private static T Find<T>(Control root, string name)
        where T : Control => All<T>(root).First(one => one.Name == name);

    private static bool Has(Control root, string name) => All<Control>(root).Any(one => one.Name == name);

    private static string[] Words(Control root) => [.. All<TextBlock>(root).Select(one => one.Text ?? "")];

    private static string[] Cards(Control root) =>
        [.. All<Card>(root).Where(one => one.Name!.StartsWith("project_", StringComparison.Ordinal)).Select(one => one.Name!)];

    // TextChanged is raised on a later turn of the dispatcher, so a test lets it run before looking.
    private static void Type(TextBox box, string text)
    {
        box.Text = text;
        Headless.Render();
    }

    // A splash waits on a timer, which finishes on another thread and hands back through the
    // dispatcher. Awaiting it from the UI thread would wait for a dispatcher that is not running.
    private static bool Until(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!done() && DateTime.UtcNow < deadline)
        {
            Headless.Render();
            Thread.Sleep(5);
        }

        return done();
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static Point Centre(Control control, Visual to) => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), to)!.Value;

    private static void Tap(Window window, Control control)
    {
        Headless.Render();
        var at = Centre(control, window);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
    }

    // A project with a folder and a project file, as one the editor has opened before, remembered
    // as of some time ago.
    private Project Remembered(string name, TimeSpan? ago = null)
    {
        var directory = Directory.CreateDirectory(Path.Combine(_temp.FullName, name)).FullName;
        File.WriteAllText(Path.Combine(directory, Project.FileName), "{}");
        _clock.At = Now - (ago ?? TimeSpan.Zero);
        var project = new Project(name, directory, _clock.At);
        _store.Remember(project);
        _clock.At = Now;
        return project;
    }

    private (LauncherSession Session, LauncherWindow Window) Open()
    {
        var session = new LauncherSession(_store, home: null, _clock);
        session.Opened += _opened.Add;
        var window = new LauncherWindow(session, _ => Task.FromResult(_picked));
        window.Show();
        return (session, window);
    }

    [Fact]
    public Task WithNoProjectsInvitesYouToMakeOne() => Headless.Run(() =>
    {
        var (session, window) = Open();

        Assert.Contains("No projects yet", Words(window));
        Click(Find<Button>(window, "new_project"));

        Assert.Equal(LauncherView.Create, session.View);
        window.Close();
    });

    [Fact]
    public Task ShowsAProjectWithItsPathAndWhenItWasOpened() => Headless.Run(() =>
    {
        var project = Remembered("Sunset Valley", TimeSpan.FromHours(2));
        var (_, window) = Open();

        var words = Words(window);
        Assert.Contains("Sunset Valley", words);
        Assert.Contains(project.Directory, words);
        Assert.Contains("Opened 2h ago", words);
        window.Close();
    });

    [Fact]
    public Task MarksAProjectWhoseFolderHasGone() => Headless.Run(() =>
    {
        // Shown rather than hidden, so somebody who moved a folder sees why it will not open
        // instead of wondering where their work went.
        _store.Remember(new Project("Gone", Path.Combine(_temp.FullName, "definitely-not-here"), Now));
        var (_, window) = Open();

        Assert.True(Has(window, "moved"));
        window.Close();
    });

    [Fact]
    public Task NarrowsToTheProjectsWhoseNameHasWhatWasTyped() => Headless.Run(() =>
    {
        Remembered("Sunset Valley");
        Remembered("Runner");
        var (_, window) = Open();
        var search = Find<TextBox>(window, "search");

        Type(search, "  SUN ");
        Assert.Single(Cards(window));
        Assert.Contains("Sunset Valley", Words(window));
        Assert.DoesNotContain("Runner", Words(window));

        Type(search, "zzz");
        Assert.Contains("Nothing matches", Words(window));
        Assert.DoesNotContain("No projects yet", Words(window));

        Type(search, "");
        Assert.Equal(2, Cards(window).Length);
        window.Close();
    });

    [Fact]
    public Task OpensAProjectFromItsCard() => Headless.Run(() =>
    {
        Remembered("Runner");
        var (_, window) = Open();

        Tap(window, Find<Card>(window, "project_0"));

        Assert.Equal("Runner", Assert.Single(_opened).Name);
        window.Close();
    });

    [Fact]
    public Task ForgetsAProjectFromTheButtonOnItsCardNotOpensIt() => Headless.Run(() =>
    {
        Remembered("Runner");
        var (session, window) = Open();
        var card = Find<Card>(window, "project_0");
        var forget = Find<Button>(card, "forget");

        // The button is only there while the pointer is over the card.
        Assert.False(forget.IsVisible);
        Headless.Render();
        window.MouseMove(Centre(card, window));
        Assert.True(forget.IsVisible);

        Tap(window, forget);

        Assert.Empty(session.Recents);
        Assert.Empty(_opened);
        window.Close();
    });

    [Fact]
    public Task TheForgetButtonCanBeReachedFromTheKeyboardToo() => Headless.Run(() =>
    {
        Remembered("Runner");
        var (_, window) = Open();
        var card = Find<Card>(window, "project_0");

        card.Focus();

        Assert.True(Find<Button>(card, "forget").IsVisible);
        window.Close();
    });

    [Theory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public Task AFocusedCardIsChosenFromTheKeyboard(Key key) => Headless.Run(() =>
    {
        Remembered("Runner");
        var (_, window) = Open();
        var card = Find<Card>(window, "project_0");
        card.Focus();

        card.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = card });

        Assert.Equal("Runner", Assert.Single(_opened).Name);
        window.Close();
    });

    [Fact]
    public Task AMovedProjectComplainsWhenItsCardIsChosen() => Headless.Run(() =>
    {
        _store.Remember(new Project("Gone", Path.Combine(_temp.FullName, "definitely-not-here"), Now));
        var (_, window) = Open();

        Tap(window, Find<Card>(window, "project_0"));

        Assert.Empty(_opened);
        Assert.Equal("That folder has moved", Find<TextBlock>(Assert.Single(window.OwnedWindows), "complaint_title").Text);
        window.Close();
    });

    [Fact]
    public Task HasACardForEveryTemplateAndATapNamesIt() => Headless.Run(() =>
    {
        var (session, window) = Open();

        foreach (var template in Enum.GetValues<ProjectTemplate>())
        {
            Assert.Contains(template.Label(), Words(Find<Card>(window, $"template_{template}")));
        }

        Tap(window, Find<Card>(window, $"template_{ProjectTemplate.ThirdPerson}"));

        Assert.Equal(ProjectTemplate.ThirdPerson, session.Template);
        Assert.Equal(LauncherView.Create, session.View);
        window.Close();
    });

    [Fact]
    public Task CanOpenAFolderThatAlreadyHoldsAProject() => Headless.Run(() =>
    {
        _picked = Remembered("Runner").Directory;
        var (_, window) = Open();

        Click(Find<Button>(window, "open_folder"));

        Assert.Equal("Runner", Assert.Single(_opened).Name);
        window.Close();
    });

    [Fact]
    public Task OpeningAFolderThatHoldsNoProjectComplains() => Headless.Run(() =>
    {
        _picked = _temp.FullName;
        var (_, window) = Open();

        Click(Find<Button>(window, "open_folder"));

        Assert.Empty(_opened);
        Assert.Equal("No Orblit project there", Find<TextBlock>(Assert.Single(window.OwnedWindows), "complaint_title").Text);
        window.Close();
    });

    [Fact]
    public Task AskingForAFolderAndChoosingNoneDoesNothing() => Headless.Run(() =>
    {
        var (_, window) = Open();

        Click(Find<Button>(window, "open_folder"));

        Assert.Empty(_opened);
        Assert.Empty(window.OwnedWindows);
        window.Close();
    });

    [Fact]
    public Task ShowsWhereTheFolderWillLandBeforeMakingIt() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.Show(LauncherView.Create);

        // The default name, turned into the folder name it will become.
        var destination = Find<TextBlock>(window, "destination");
        Assert.Equal(Path.Combine(session.DefaultLocation, "untitled-project"), destination.Text);

        Type(Find<TextBox>(window, "name"), "My Game!");
        Assert.Equal(Path.Combine(session.DefaultLocation, "my-game"), destination.Text);
        window.Close();
    });

    [Fact]
    public Task OffersEveryTemplateWithOneChosen() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.Show(LauncherView.Create);

        foreach (var template in Enum.GetValues<ProjectTemplate>())
        {
            Assert.Contains(template.Label(), Words(Find<RadioButton>(window, $"radio_{template}")));
        }

        Assert.Equal([ProjectTemplate.Scene], ChosenTemplates(window));
        window.Close();
    });

    [Fact]
    public Task StartsWithTheTemplateThatWasAskedFor() => Headless.Run(() =>
    {
        var (session, window) = Open();

        session.StartFrom(ProjectTemplate.Empty);

        Assert.Equal([ProjectTemplate.Empty], ChosenTemplates(window));
        window.Close();
    });

    [Fact]
    public Task ChoosingATemplateOnThePageChangesWhatTheProjectStartsFrom() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.Show(LauncherView.Create);

        Find<RadioButton>(window, $"radio_{ProjectTemplate.ThirdPerson}").IsChecked = true;

        Assert.Equal(ProjectTemplate.ThirdPerson, session.Template);
        Assert.Equal([ProjectTemplate.ThirdPerson], ChosenTemplates(window));
        window.Close();
    });

    [Fact]
    public Task RefusesANamelessProjectRatherThanMakingOne() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.Show(LauncherView.Create);
        Type(Find<TextBox>(window, "name"), "   ");

        Click(Find<Button>(window, "create"));

        Assert.Empty(_opened);
        Assert.Equal("A project needs a name", Find<TextBlock>(Assert.Single(window.OwnedWindows), "complaint_title").Text);
        window.Close();
    });

    [Fact]
    public Task CreatesTheProjectInTheChosenPlace() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.Show(LauncherView.Create);
        Type(Find<TextBox>(window, "name"), "My Game");
        Type(Find<TextBox>(window, "location"), _temp.FullName);

        Click(Find<Button>(window, "create"));

        Assert.Equal(Path.Combine(_temp.FullName, "my-game"), Assert.Single(_opened).Directory);
        window.Close();
    });

    [Fact]
    public Task EnterInTheNameFieldCreatesToo() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.Show(LauncherView.Create);
        Type(Find<TextBox>(window, "location"), _temp.FullName);
        var name = Find<TextBox>(window, "name");

        name.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = name });

        Assert.Single(_opened);
        window.Close();
    });

    [Fact]
    public Task BrowseFillsTheLocationFromTheFolderChosen() => Headless.Run(() =>
    {
        _picked = _temp.FullName;
        var (session, window) = Open();
        session.Show(LauncherView.Create);

        Click(Find<Button>(window, "browse"));

        Assert.Equal(_temp.FullName, Find<TextBox>(window, "location").Text);
        window.Close();
    });

    [Fact]
    public Task TheNewProjectButtonOpensTheFormAndCancelLeavesIt() => Headless.Run(() =>
    {
        var (session, window) = Open();

        Click(Find<Button>(window, "new_project"));
        Assert.Equal(LauncherView.Create, session.View);
        Assert.True(Has(window, "create"));

        Click(Find<Button>(window, "cancel"));
        Assert.Equal(LauncherView.Projects, session.View);
        Assert.True(Has(window, "search"));
        window.Close();
    });

    [Fact]
    public Task TheProjectsItemInTheRailLeavesTheForm() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.Show(LauncherView.Create);

        Click(Find<Button>(window, "rail_projects"));

        Assert.Equal(LauncherView.Projects, session.View);
        Assert.True(Has(window, "search"));
        window.Close();
    });

    [Fact]
    public Task ThePageStartsFromItsDefaultsEachTimeItIsOpened() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.Show(LauncherView.Create);
        Type(Find<TextBox>(window, "name"), "Something else");

        session.Show(LauncherView.Projects);
        session.Show(LauncherView.Create);

        Assert.Equal("Untitled Project", Find<TextBox>(window, "name").Text);
        window.Close();
    });

    [Fact]
    public Task TheSearchIsStillThereAfterVisitingTheCreatePage() => Headless.Run(() =>
    {
        Remembered("Sunset Valley");
        Remembered("Runner");
        var (session, window) = Open();
        Type(Find<TextBox>(window, "search"), "sun");

        session.Show(LauncherView.Create);
        session.Show(LauncherView.Projects);

        Assert.Equal("sun", Find<TextBox>(window, "search").Text);
        Assert.Single(Cards(window));
        window.Close();
    });

    [Fact]
    public Task TheSplashCoversTheLauncherThenFadesAway() => Headless.Run(() =>
    {
        var session = new LauncherSession(_store, home: null, _clock);
        var window = new LauncherWindow(session, splash: TimeSpan.FromMilliseconds(20));
        window.Show();
        var splash = Assert.IsType<Splash>(window.Content);

        Assert.True(Find<Grid>(splash, "splash").IsVisible);
        Assert.True(Until(() => splash.IsDone));

        Assert.False(Find<Grid>(splash, "splash").IsVisible);
        window.Close();
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task AnyKeyOrAClickEndsTheSplashEarly(bool byKey) => Headless.Run(() =>
    {
        var splash = new Splash(new Border(), TimeSpan.FromMinutes(5), "v");
        var window = new Window { Content = splash };
        window.Show();

        if (byKey)
        {
            window.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.None);
        }
        else
        {
            window.MouseDown(new Point(10, 10), MouseButton.Left);
        }

        Assert.True(Until(() => splash.IsDone));
        window.Close();
    });

    [Fact]
    public Task TheComplaintSaysWhatWentWrongAndCloses() => Headless.Run(() =>
    {
        var complaint = new Complaint("Could not write there", "The disk is full.");
        complaint.Show();

        Assert.Equal("Could not write there", Find<TextBlock>(complaint, "complaint_title").Text);
        Assert.Equal("The disk is full.", Find<TextBlock>(complaint, "complaint_detail").Text);
        Click(Find<Button>(complaint, "close"));

        Assert.False(complaint.IsVisible);
    });

    [Theory]
    [InlineData(100, 1)]
    [InlineData(280, 1)]
    [InlineData(575, 1)]
    [InlineData(576, 2)]
    [InlineData(871, 2)]
    [InlineData(872, 3)]
    [InlineData(4000, 3)]
    public void TheGridFitsAsManyColumnsAsTheWidthHoldsUpToThree(double width, int columns) =>
        Assert.Equal(columns, CardGrid.ColumnsFor(width));

    [Fact]
    public Task TheGridMakesEveryCardInARowAsTallAsTheTallest() => Headless.Run(() =>
    {
        var short1 = new Border { Child = new Border { Height = 40 } };
        var tall = new Border { Child = new Border { Height = 90 } };
        var grid = new CardGrid { Children = { short1, tall } };
        var window = new Window { Width = 700, Content = grid };
        window.Show();
        Headless.Render();

        Assert.Equal(2, CardGrid.ColumnsFor(grid.Bounds.Width));
        Assert.Equal(90, short1.Bounds.Height);
        Assert.Equal(short1.Bounds.Width, tall.Bounds.Width);
        window.Close();
    });

    [Theory]
    [InlineData("A", ProjectTemplate.Empty)]
    [InlineData("B", ProjectTemplate.Scene)]
    [InlineData("D", ProjectTemplate.ThirdPerson)]
    [InlineData("Runner", ProjectTemplate.ThirdPerson)]
    [InlineData("The Quick Brown Fox Jumps Over", ProjectTemplate.Scene)]
    public void ANameDrawsTheSamePictureDartDrawsForIt(string name, ProjectTemplate same) =>
        Assert.Equal(SceneArt.ForTemplate(same), SceneArt.ForName(name));

    private static ProjectTemplate[] ChosenTemplates(Control root) =>
        [.. Enum.GetValues<ProjectTemplate>().Where(template => Find<RadioButton>(root, $"radio_{template}").IsChecked == true)];
}
