using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// The workspaces as a row of tabs. A click enters the mode, and the left and right arrows move focus
/// along the row.
/// </summary>
/// <remarks>
/// shortcut: every tab is its own tab stop, where a roving one would take one press of Tab to cross the
/// row. Change it when the title bar grows more stops than this one.
/// </remarks>
internal sealed class ModeTabs : StackPanel
{
    private readonly List<(string Name, Border Shape, Button Tab)> _tabs = [];

    public ModeTabs(IEnumerable<EditorMode> modes, Action<EditorMode> enter)
    {
        Name = "mode_tabs";
        Orientation = Orientation.Horizontal;
        Spacing = Space.Xs;
        VerticalAlignment = VerticalAlignment.Center;
        foreach (var mode in modes)
        {
            var tab = new Button
            {
                Name = $"mode_{mode.Name}",
                Content = mode.Label,
                FontSize = OrblitText.Label,
                Padding = new Thickness(DockStyle.TabPadX, DockStyle.TabPadY),
                Background = Brushes.Transparent,
            };
            AutomationProperties.SetName(tab, mode.Label);
            tab.Click += (_, _) => enter(mode);
            // The shape is what is drawn, and the button inside it only takes the click.
            var shape = new Border { Name = $"modeshape_{mode.Name}", CornerRadius = new CornerRadius(DockStyle.Radius), Child = tab };
            _tabs.Add((mode.Name, shape, tab));
            Children.Add(shape);
        }

        AddHandler(KeyDownEvent, OnKeyDown);
    }

    public void Refresh(string active)
    {
        foreach (var (name, shape, tab) in _tabs)
        {
            var on = name == active;
            shape.Background = on ? OrblitColors.Surface : Brushes.Transparent;
            tab.Foreground = on ? OrblitColors.Ink : OrblitColors.InkDim;
        }
    }

    // The ends stop the key and keep it from the window, whose own arrow keys nudge the selection.
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var step = e.Key switch { Key.Left => -1, Key.Right => 1, _ => 0 };
        var from = _tabs.FindIndex(one => ReferenceEquals(one.Tab, e.Source));
        if (step == 0 || from < 0)
        {
            return;
        }

        _tabs[Math.Clamp(from + step, 0, _tabs.Count - 1)].Tab.Focus(NavigationMethod.Directional);
        e.Handled = true;
    }
}
