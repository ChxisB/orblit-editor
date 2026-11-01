using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;

/// <summary>
/// What the inspector says above its fields: which object it is, where it came from and what it is
/// linked to.
/// </summary>
internal static class InspectorBands
{
    private const string PrefabApplyTip =
        "Save what was changed here into the prefab. Every other instance keeps its own changes and takes the rest.";

    private const string PrefabRevertTip =
        "Throw away what was changed here and take the prefab again. It stays where it stands.";

    private const string PrefabUnpackTip =
        "Break the link. The parts become ordinary objects and stop following the prefab.";

    /// <summary>
    /// Says this object is, or is part of, an instance of a prefab, and offers the three things anybody
    /// wants to do about it. Above the fields because it changes what editing a field means: a change
    /// here is a change to one lamp post until it is applied, and then it is a change to every lamp post.
    /// </summary>
    /// <param name="instance">The instance the selection is, or is inside.</param>
    /// <param name="selected">What is selected, which the buttons act on.</param>
    /// <param name="links">What the buttons call.</param>
    public static Control Prefab(SceneObject instance, SceneObject selected, InspectorLinks links)
    {
        var source = instance.Prefab!.Asset ?? "";
        var readable = instance.Prefab.State == Orblit.Scene.PrefabState.Open;
        var partOf = ReferenceEquals(instance, selected) ? null : instance.Name;
        var name = new TextBlock
        {
            Name = "prefab_name",
            Text = Path.GetFileName(source),
            FontSize = OrblitText.Label,
            Foreground = OrblitColors.Ember,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ToolTip.SetTip(name, source);

        var column = new StackPanel { Children = { name } };
        if (InspectorModel.PrefabNote(source, readable, partOf) is { } note)
        {
            var line = Fields.Note(note);
            line.Name = "prefab_note";
            column.Children.Add(line);
        }

        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), Margin = new Thickness(0, Space.Sm, 0, 0) };

        // A prefab that cannot be read has nothing to apply, revert or unpack.
        Action? On(Action<string>? link) => readable && link is not null ? () => link(selected.Id) : null;
        Action(actions, "Apply", PrefabApplyTip, On(links.OnApplyPrefab));
        Action(actions, "Revert", PrefabRevertTip, On(links.OnRevertPrefab));
        Action(actions, "Unpack", PrefabUnpackTip, On(links.OnUnpackPrefab));
        column.Children.Add(actions);
        return new Border
        {
            Name = "prefab_band",
            Padding = new Thickness(Space.Lg, Space.Sm),
            Background = OrblitColors.Raised,
            BorderBrush = OrblitColors.LineSoft,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = column,
        };
    }

    /// <summary>Says that the fields below belong to one of several selected things.</summary>
    public static Control Multiple(int count, string name) => new Border
    {
        Name = "inspector_multiple",
        Padding = new Thickness(Space.Lg, Space.Sm),
        Background = OrblitColors.EmberWash,
        Child = new TextBlock
        {
            Text = $"{count} selected · editing {name}",
            FontSize = OrblitText.Caption,
            Foreground = OrblitColors.Ember,
            TextTrimming = TextTrimming.CharacterEllipsis,
        },
    };

    /// <summary>
    /// The object's name, which is editable in place, with what kind of thing it is under it and the
    /// switch that shows or hides it.
    /// </summary>
    public static Control Header(FieldHost host, InspectorTarget target)
    {
        var item = target.Item;
        Control? visible = null;
        if (item.Kind != ObjectKind.Scene)
        {
            visible = new OrblitSwitch(
                "Visible",
                item.Visible,
                wanted => host.Apply(() => host.History.Run(new SetVisible(target.SceneId, item.Id, item.Name, wanted))))
            {
                Name = "inspector_visible",
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        return Header(host, new HeaderOptions
        {
            Name = target.Scene.DisplayNameOf(item),
            Caption = item.KindLabel,
            Rename = now => now == item.Name ? null : new Rename(target.SceneId, item.Id, item.Name, now),
            Trailing = visible,
        });
    }

    /// <summary>A name, and under it what it names, with room on the right for a control.</summary>
    /// <remarks>
    /// Renaming merges into one undo step while the box has focus and seals when it lets go, so undo
    /// returns to the old name and not to a prefix of the new one.
    /// </remarks>
    public static Control Header(FieldHost host, HeaderOptions options)
    {
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(Space.Lg, Space.Xs, Space.Lg, Space.Lg) };
        var names = new StackPanel { Children = { NameBox(host, options) }, VerticalAlignment = VerticalAlignment.Center };
        if (options.Caption is { } caption)
        {
            names.Children.Add(new TextBlock { Text = caption, FontSize = OrblitText.Caption, Foreground = OrblitColors.InkDim });
        }

        head.Children.Add(names);
        if (options.Trailing is { } trailing)
        {
            Grid.SetColumn(trailing, 1);
            head.Children.Add(trailing);
        }

        return head;
    }

    private static Control NameBox(FieldHost host, HeaderOptions options)
    {
        if (options.Rename is not { } rename)
        {
            return new TextBlock
            {
                Name = "inspector_title",
                Text = options.Name,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
        }

        var box = new TextBox
        {
            Name = "inspector_name",
            Text = options.Name,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
        };
        // A box that has just been built reports its starting text once it is shown. That is not an edit.
        var last = options.Name;
        box.TextChanged += (_, _) =>
        {
            var now = (box.Text ?? "").Trim();
            if (now == last || now.Length == 0)
            {
                return;
            }

            last = now;
            if (rename(now) is { } command)
            {
                host.Hold();
                host.History.Run(command);
            }
        };
        box.LostFocus += (_, _) => host.Settle();
        box.KeyDown += (_, key) =>
        {
            if (key.Key == Key.Enter)
            {
                host.Settle();
            }
        };
        return box;
    }

    /// <summary>One data object an object points at.</summary>
    public static Control DataLink(int index, string path, Action? onOpen, Action? onRemove)
    {
        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 2) };
        line.Children.Add(Link($"data_open_{index}", Path.GetFileName(path), path, onOpen));
        if (onRemove is not null)
        {
            var remove = new Button { Name = $"data_remove_{index}", Content = "Remove", FontSize = OrblitText.Caption };
            ToolTip.SetTip(remove, "Stop using this here");
            remove.Click += (_, _) => onRemove();
            Grid.SetColumn(remove, 1);
            line.Children.Add(remove);
        }

        return line;
    }

    /// <summary>A name that opens what it names, with the whole path in its tooltip.</summary>
    public static TextBlock Link(string name, string text, string path, Action? onOpen)
    {
        var link = new TextBlock
        {
            Name = name,
            Text = text,
            FontSize = OrblitText.Label,
            Foreground = OrblitColors.Ink,
            TextTrimming = TextTrimming.CharacterEllipsis,
            // Left, so the part that answers a click is the name and not the empty width after it.
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(link, path);
        if (onOpen is not null)
        {
            link.Cursor = new Cursor(StandardCursorType.Hand);
            link.PointerPressed += (_, _) => onOpen();
        }

        return link;
    }

    // Placed in the next column, so the three read left to right in the order they are made.
    private static void Action(Grid row, string label, string tooltip, Action? onPressed)
    {
        var column = row.Children.Count;
        var button = new Button
        {
            Name = $"prefab_{label.ToLowerInvariant()}",
            Content = label,
            IsEnabled = onPressed is not null,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            FontSize = OrblitText.Label,
            Margin = new Thickness(column == 0 ? 0 : Space.Xs, 0, 0, 0),
        };
        ToolTip.SetTip(button, tooltip);
        button.Click += (_, _) => onPressed?.Invoke();
        Grid.SetColumn(button, column);
        row.Children.Add(button);
    }
}

/// <summary>What a header names, and whether the name can be changed from there.</summary>
internal sealed record HeaderOptions
{
    public required string Name { get; init; }

    /// <summary>What kind of thing it is, under the name. Null where the name says enough.</summary>
    public string? Caption { get; init; }

    /// <summary>The command a typed name makes, or null when it is no change. Absent where the name is not editable.</summary>
    public Func<string, EditorCommand?>? Rename { get; init; }

    public Control? Trailing { get; init; }
}
