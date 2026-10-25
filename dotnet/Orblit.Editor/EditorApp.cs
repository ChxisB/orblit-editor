using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Orblit.Editor;

/// <summary>The application: the dark theme with the editor's accent, and the window it opens with.</summary>
public sealed class EditorApp(Func<Window> mainWindow) : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
        Resources["SystemAccentColor"] = OrblitColors.EmberColor;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = mainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
