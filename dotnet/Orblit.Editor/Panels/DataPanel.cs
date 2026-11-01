using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.VectorMath;

/// <summary>
/// A data object, edited in the inspector in place of the selection. Values are a thing somebody edits,
/// not a thing somebody codes, and a text editor puts them out of reach of whoever most wants to change
/// them.
/// </summary>
/// <remarks>
/// Writes shortly after the last change rather than on a Save button. The value is shared, and one
/// nobody has saved is one that is not shared yet. Not on every keystroke either: a name being typed
/// would be eight writes and eight notices to everything reading it.
/// </remarks>
internal sealed class DataPanel : UserControl
{
    private const string NumberIcon = "M10 4L8 20M16 4l-2 16M5 9h15M4 15h15";
    private const string TextIcon = "M5 7V5h14v2M12 5v14M9 19h6";
    private const string ToggleIcon = "M8 7h8a5 5 0 0 1 0 10H8A5 5 0 0 1 8 7zM16 10a2 2 0 1 0 0 4a2 2 0 1 0 0-4z";
    private const string ColourIcon = "M12 3a9 9 0 0 0 0 18c1.4 0 2-.9 2-2s-1-1.6-1-2.6s1-1.4 2-1.4h2a4 4 0 0 0 4-4c0-4.4-4-8-9-8zM7.5 12v.01M9.5 7.5v.01M14.5 7.5v.01";
    private const string VectorIcon = "M12 3v18M3 12h18M9 6l3-3 3 3M9 18l3 3 3-3M6 9l-3 3 3 3M18 9l3 3-3 3";
    private const string AssetIcon = "M15 7l-6.5 6.5a2 2 0 0 0 3 3L18 10a4 4 0 0 0-6-6l-6.5 6.5a6 6 0 0 0 9 9L20 14";

    private static readonly string[] AxisNames = ["X", "Y", "Z"];
    private static readonly TimeSpan WriteDelay = TimeSpan.FromMilliseconds(300);

    private readonly EditorSession _session;
    private readonly FieldHost _host;
    private readonly Fields _fields;
    private readonly StackPanel _stack = new() { Name = "data_panel" };

    // The panel's own copy. Whatever else reads the file hears of an edit when it is written.
    private DataObject? _data;
    private bool _waiting;
    private int _changes;

    /// <param name="session">Where the file is read from and written to.</param>
    /// <param name="relative">Where the file is, relative to the project.</param>
    public DataPanel(EditorSession session, string relative)
    {
        _session = session;
        Relative = relative;
        _data = session.Data[relative];
        _host = new FieldHost(session.History, Rebuild);
        _fields = new Fields(_host);
        Content = _stack;
        Rebuild();
    }

    /// <summary>Where the file is, relative to the project.</summary>
    public string Relative { get; }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // A change still waiting goes to disk rather than being lost to a click somewhere else.
        Flush();
        base.OnDetachedFromVisualTree(e);
    }

    private static string Icon(DataType type) => type switch
    {
        DataType.Number => NumberIcon,
        DataType.Text => TextIcon,
        DataType.Toggle => ToggleIcon,
        DataType.Colour => ColourIcon,
        DataType.Vector => VectorIcon,
        DataType.Asset => AssetIcon,
        _ => throw new UnreachableException(),
    };

    private static List<object?> Moved(Vector3 at, int axis, double value)
    {
        double[] axes = [at.X, at.Y, at.Z];
        axes[axis] = value;
        return [axes[0], axes[1], axes[2]];
    }

    private void Flush()
    {
        if (_waiting)
        {
            Write();
        }
    }

    // Only the wait that began at the last change writes, so the file is written once the changes stop.
    private async Task WriteLater(int change)
    {
        await Task.Delay(WriteDelay);
        if (change == _changes)
        {
            Flush();
        }
    }

    private void Write()
    {
        _waiting = false;
        if (_data is { } data && _session.Data.Write(Relative, data) is { } problem)
        {
            _session.Report([problem]);
        }
    }

    private void Change(Func<DataObject, DataObject> edit)
    {
        if (_data is not { } data)
        {
            return;
        }

        _data = edit(data);
        _waiting = true;
        _ = WriteLater(++_changes);
    }

    private void Edit(int index, Func<DataField, DataField> edit) =>
        Change(data => data with { Fields = [.. data.Fields.Select((field, at) => at == index ? edit(field) : field)] });

    private void Add(DataType type) =>
        Change(data => data with { Fields = [.. data.Fields, new DataField(data.Available(type.Name()), type, type.Blank())] });

    private void Remove(int index) => Change(data => data with { Fields = [.. data.Fields.Where((_, at) => at != index)] });

    // An empty key is a field no script can name, so the box keeps the last one until something is typed.
    private void Rename(int index, string typed)
    {
        var key = typed.Trim();
        if (key.Length > 0)
        {
            Edit(index, field => field with { Key = key });
        }
    }

    // The file has to be up to date first, since the bindings are read from it.
    private void WriteBindings()
    {
        Flush();
        _session.WriteBindings(Relative);
    }

    private void Rebuild()
    {
        _host.Reset();
        _stack.Children.Clear();
        if (_data is not { } data)
        {
            _stack.Children.Add(Unreadable());
            return;
        }

        _stack.Children.Add(Heading(data));
        for (var index = 0; index < data.Fields.Count; index++)
        {
            _stack.Children.Add(Card(data.Fields[index], index));
        }

        _stack.Children.Add(AddButton());
        var bindings = Fields.Button("Write script bindings", "Write the types scripts use to read this data.", WriteBindings);
        bindings.Margin = new Thickness(Space.Sm, Space.Xs, Space.Sm, 0);
        _stack.Children.Add(bindings);
    }

    private TextBlock Unreadable()
    {
        var note = Fields.Note($"{Path.GetFileName(Relative)} could not be read as a data object.");
        note.Name = "data_unreadable";
        note.TextAlignment = TextAlignment.Center;
        note.Margin = new Thickness(Space.Md);
        return note;
    }

    private StackPanel Heading(DataObject data) => new()
    {
        Margin = new Thickness(Space.Md, 0, Space.Md, Space.Sm),
        Spacing = Space.Xs,
        Children =
        {
            _fields.Box("data_name", data.Name, name => Change(now => now with { Name = name })),
            new TextBlock { Text = Relative, FontFamily = OrblitText.Mono, FontSize = 10.5, Foreground = OrblitColors.InkMid },
            _fields.Box("data_note", data.Note, note => Change(now => now with { Note = note }), "What this is for"),
        },
    };

    private Border Card(DataField field, int index)
    {
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = Space.Sm };
        top.Children.Add(new Glyph(Icon(field.Type), 13, OrblitColors.InkDim));
        // The key is what a script reads, so it stays editable after the field is made.
        var key = _fields.Box($"field_key_{index}", field.Key, typed => Rename(index, typed));
        Grid.SetColumn(key, 1);
        top.Children.Add(key);
        top.Children.Add(new ToolbarAction($"field_remove_{index}", TimelineIcons.Close, "Remove this value", () => _host.Apply(() => Remove(index)))
        {
            [Grid.ColumnProperty] = 2,
        });

        var note = Fields.Row(
            "Note",
            _fields.Box($"field_note_{index}", field.Note, typed => Edit(index, now => now with { Note = typed }), "What it means"),
            name: $"row_note_{index}");
        return new Border
        {
            Margin = new Thickness(Space.Sm, 0, Space.Sm, Space.Sm),
            Padding = new Thickness(Space.Md, Space.Sm, Space.Sm, Space.Sm),
            Background = OrblitColors.Ground,
            CornerRadius = Radii.Card,
            BorderBrush = OrblitColors.LineSoft,
            BorderThickness = new Thickness(1),
            Child = Fields.Column([top, Value(field, index), note]),
        };
    }

    private Control Value(DataField field, int index) => field.Type switch
    {
        DataType.Number => Fields.Row(
            "Value",
            Number($"field_value_{index}", field.AsNumber, value => Edit(index, now => now with { Value = value })),
            name: $"row_value_{index}"),
        DataType.Text or DataType.Asset => Fields.Row(
            "Value",
            _fields.Box($"field_value_{index}", field.AsText, text => Edit(index, now => now with { Value = text }), field.Type == DataType.Asset ? "assets/…" : null),
            name: $"row_value_{index}"),
        DataType.Toggle => _fields.Choice("Value", ["Off", "On"], field.AsToggle ? "On" : "Off", on => Edit(index, now => now with { Value = on == "On" })),
        DataType.Colour => _fields.Palette("Value", field.AsColour, colour => Edit(index, now => now with { Value = colour.ToHex() })),
        DataType.Vector => Axes(field.AsVector, index),
        _ => throw new UnreachableException(),
    };

    private StackPanel Axes(Vector3 at, int index)
    {
        double[] axes = [at.X, at.Y, at.Z];
        return Fields.Column(AxisNames.Select((axis, which) => Fields.Row(
            axis,
            Number($"field_{Fields.Key(axis)}_{index}", axes[which], value => Edit(index, now => now with { Value = Moved(now.AsVector, which, value) })),
            name: $"row_{Fields.Key(axis)}_{index}")));
    }

    // Text that is not a number leaves the last number in the file rather than writing nothing in its place.
    // NaN and infinity count as not a number, since JSON has no word for them and the write would throw.
    private TextBox Number(string name, double value, Action<double> onChanged) =>
        _fields.Box(name, value.ToString(CultureInfo.InvariantCulture), text =>
        {
            if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
            {
                onChanged(number);
            }
        });

    private Button AddButton()
    {
        var menu = new MenuFlyout();
        foreach (var type in Enum.GetValues<DataType>())
        {
            var item = new MenuItem { Name = $"add_{type.Name()}", Header = type.ToString(), Icon = new Glyph(Icon(type), 15) };
            item.Click += (_, _) => _host.Apply(() => Add(type));
            menu.Items.Add(item);
        }

        var button = new Button
        {
            Name = "button_add_a_value",
            Content = "Add a value",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            FontSize = OrblitText.Label,
            Margin = new Thickness(Space.Sm, Space.Xs, Space.Sm, 0),
            Flyout = menu,
        };
        ToolTip.SetTip(button, "Add a named value to this data file.");
        return button;
    }
}
