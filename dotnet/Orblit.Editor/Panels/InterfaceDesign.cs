using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Ui;

/// <summary>
/// What to add, the selected element's properties, the languages the canvas speaks, and the canvas
/// and its grid.
/// </summary>
/// <remarks>
/// The device, the way it is held and the language are views, so a press on one is no step to undo.
/// Everything else here changes the file.
/// </remarks>
internal sealed class InterfaceDesign : UserControl
{
    private readonly InterfaceBench _bench;
    private readonly FieldHost _host;
    private readonly Fields _fields;
    private readonly StackPanel _stack = new() { Name = "interface_design_fields", Margin = new Thickness(0, Space.Sm) };

    // What the fields were last built from. Pointing at an element changes the bench as well, and
    // building the fields again for that would take the caret out of a box somebody had clicked into.
    private object? _seen;

    public InterfaceDesign(InterfaceBench bench)
    {
        _bench = bench;
        _host = new FieldHost(bench.History, Rebuild);
        _fields = new Fields(_host);
        Name = "interface_design";
        Background = OrblitColors.Surface;
        Content = new ScrollViewer { Content = _stack };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _bench.Changed += Follow;
        _seen = null;
        Follow();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _bench.Changed -= Follow;
        // A box being typed in gets no LostFocus once it is off the tree, and a hold left over would stop every rebuild.
        _host.Settle();
        base.OnDetachedFromVisualTree(e);
    }

    private void Follow()
    {
        var now = (_bench.Document, _bench.Selected, _bench.Device, _bench.Landscape, _bench.Strings.Locale, _bench.StringProblems);
        if (now.Equals(_seen))
        {
            return;
        }

        _seen = now;
        _host.Changed();
    }

    private void Rebuild()
    {
        _host.Reset();
        _stack.Children.Clear();
        if (_bench.Document is not { } document)
        {
            var none = Fields.Note(InterfaceSideModel.NoDesign);
            none.Name = "interface_design_none";
            none.Margin = new Thickness(Space.Lg);
            _stack.Children.Add(none);
            return;
        }

        _stack.Children.Add(_fields.Section("Add", Wrap(UiElement.All.Select(AddButton))));
        if (_bench.Element is { } selected)
        {
            _stack.Children.Add(Properties(selected));
        }

        if (InterfaceSideModel.SpeaksMore(_bench))
        {
            _stack.Children.Add(Languages());
        }

        _stack.Children.Add(CanvasSection(document.Canvas));
        _stack.Children.Add(GridSection(document.Canvas));
    }

    // One step that is over as soon as it is made. Held, so the press that made it is not taken out
    // from under itself by the rebuild it causes.
    private Action Tap(Action change) => () => _host.Apply(change);

    private Button AddButton(UiElement what)
    {
        var button = Fields.Button(what.Label, $"Add {what.Label.ToLowerInvariant()} to the selected container.", Tap(() => _bench.Add(what)));
        button.Name = $"add_{what.Type}";
        return button;
    }

    private Control Properties(UiNode selected)
    {
        var rows = new List<Control>();
        if (selected.Text is not null || selected.Type is "text" or "button")
        {
            rows.Add(Typing(new Typed("Words", "Edit words"), selected.Text ?? "", static (node, text) => node with { Text = text }));
        }

        if (selected.Type is "text" or "button")
        {
            rows.Add(Typing(new Typed("Key", "Edit key", "play.label"), selected.TextKey ?? "", static (node, text) => InterfaceSideModel.WithProp(node, "textKey", text.Trim())));
        }

        rows.Add(Typing(new Typed("Classes", "Edit classes", "p-4 flex-1 md:row lg:text-2xl"), selected.Classes, static (node, text) => node with { Classes = text }));
        rows.Add(Typing(new Typed("CSS", "Edit CSS", "padding: 8px 12px"), selected.Css, static (node, text) => node with { Css = text }));
        if (InterfaceSideModel.IsContainer(selected))
        {
            rows.Add(Narrow(selected));
            rows.Add(Split());
        }

        rows.Add(Typing(new Typed("Handler", "Edit handler", "what script calls"), selected.HandlerFor("onPressed") ?? "", static (node, text) => InterfaceSideModel.WithProp(node, "onPressed", text.Trim())));
        return _fields.Section(selected.Type, Fields.Column(rows));
    }

    // What is typed into one box is one step to undo, which ends when the box is left.
    private Control Typing(Typed field, string value, Func<UiNode, string, UiNode> change) =>
        _fields.Entry(field.Label, value, text => _bench.EditElement(field.Step, node => change(node, text), gesture: field.Step), field.Hint);

    private Grid Narrow(UiNode selected)
    {
        var stacks = InterfaceSideModel.Stacks(selected);
        var chip = new ToolbarChip(
            "narrow",
            stacks ? "Stacks" : "Row",
            InterfaceSideModel.NarrowTip,
            Tap(() => _bench.EditElement("Change narrow layout", static node => node with { Classes = InterfaceSideModel.Narrowed(node) })))
        {
            On = stacks,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        return Fields.Row("Narrow", chip);
    }

    private Grid Split()
    {
        var counts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Xs };
        foreach (var count in (int[])[2, 3, 4])
        {
            var button = Fields.Button(
                $"{count}",
                $"Make this a row of {count} equal columns. What is in it goes into the first one.",
                Tap(() => _bench.Split(count)));
            button.Name = $"split_{count}";
            counts.Children.Add(button);
        }

        return Fields.Row("Split", counts);
    }

    // A view like the device, so none of it is a step to undo.
    private Control Languages()
    {
        var strings = _bench.Strings;
        var gaps = _bench.Missing;
        var reload = Fields.Button("Reload", InterfaceSideModel.ReloadTip, Tap(_bench.LoadStrings));
        reload.Name = "strings_reload";
        var rows = new List<Control>
        {
            Wrap([
                .. strings.Locales.Select(locale => new ToolbarChip(
                    $"locale_{locale}",
                    locale,
                    $"{gaps.GetValueOrDefault(locale)?.Count ?? 0} keys missing",
                    Tap(() => strings.Use(locale))) { On = strings.Locale == locale }),
                reload,
            ]),
        };
        if (strings.Locales.Count == 0)
        {
            rows.Add(Fields.Note(InterfaceSideModel.NoLanguages));
        }

        rows.AddRange(_bench.StringProblems.Select(problem => Fields.Note(problem)));
        if (gaps.GetValueOrDefault(strings.Locale) is { Count: > 0 } lacking)
        {
            rows.Add(Caption($"Missing in {strings.Locale}"));
            rows.AddRange(lacking.Select(key => (Control)new TextBlock { Text = key, FontSize = 10.5, FontFamily = OrblitText.Mono, Foreground = OrblitColors.InkDim }));
        }

        return _fields.Section("Languages", Fields.Column(rows));
    }

    private Control CanvasSection(UiCanvas canvas) => _fields.Section("Canvas", Fields.Column(
    [
        Wrap(InterfaceSideModel.Sizes.Select(size => new ToolbarChip(
            $"size_{(int)size.Width}_{(int)size.Height}",
            InterfaceSideModel.Size(size),
            null,
            Tap(() => _bench.SetCanvas("Change canvas size", canvas with { Width = size.Width, Height = size.Height })))
        {
            On = canvas.Width == size.Width && canvas.Height == size.Height,
        })),
        Caption("On screen"),
        // Chips that wrap rather than five segments sharing a row: "Match height" does not fit in a
        // fifth of a narrow panel.
        Wrap(Enum.GetValues<CanvasFit>().Select(fit => new ToolbarChip(
            $"fit_{fit.Name()}",
            fit.Label(),
            InterfaceSideModel.Explain(fit),
            Tap(() => _bench.SetCanvas("Change how it fits", canvas with { Fit = fit })))
        {
            On = canvas.Fit == fit,
        })),
        CanvasSlider("Safe area", canvas.SafeArea * 100, value => canvas with { SafeArea = value / 100 }, new SliderOptions { Min = 0, Max = 20, Unit = "%" }),
        Caption("Preview on"),
        Wrap(InterfaceSideModel.Devices.Select(device => new ToolbarChip(
            $"device_{device.Label.ToLowerInvariant()}",
            device.Label,
            InterfaceSideModel.DeviceTip(device.Size),
            Tap(() => _bench.Device = device.Size))
        {
            // Against the device and not the size shown, since a phone held sideways is still the phone.
            On = _bench.Device == device.Size,
        })),
        // Its own control rather than more devices, because every device can be held either way.
        Wrap(((bool[])[false, true]).Select(sideways => new ToolbarChip(
            sideways ? "held_landscape" : "held_portrait",
            sideways ? "Landscape" : "Portrait",
            null,
            Tap(() => _bench.Landscape = sideways))
        {
            On = _bench.Landscape == sideways,
        })),
    ]));

    private Control GridSection(UiCanvas canvas)
    {
        var count = Fields.Note(InterfaceSideModel.GridCount(canvas, _bench.LaidOut?.Width ?? canvas.Width));
        count.Name = "grid_count";
        return _fields.Section("Grid", Fields.Column(
        [
            CanvasSlider("Columns", canvas.Columns, value => canvas with { Columns = (int)Math.Round(value) }, new SliderOptions { Min = 1, Max = 24 }),
            CanvasSlider("Gutter", canvas.Gutter, value => canvas with { Gutter = value }, new SliderOptions { Min = 0, Max = 80, Unit = "px" }),
            count,
            Caption("Fluid range"),
            CanvasSlider("Smallest", canvas.MinScale * 100, value => canvas with { MinScale = value / 100 }, new SliderOptions { Min = 40, Max = 100, Unit = "%" }),
            CanvasSlider("Largest", canvas.MaxScale * 100, value => canvas with { MaxScale = value / 100 }, new SliderOptions { Min = 100, Max = 300, Unit = "%" }),
        ]));
    }

    // A drag is one step to undo.
    private Control CanvasSlider(string label, double value, Func<double, UiCanvas> change, SliderOptions options)
    {
        var step = $"Change {label.ToLowerInvariant()}";
        return _fields.Slider(label, value, moved => _bench.SetCanvas(step, change(moved), gesture: step), options);
    }

    private static WrapPanel Wrap(IEnumerable<Control> chips)
    {
        var wrap = new WrapPanel();
        foreach (var chip in chips)
        {
            chip.Margin = new Thickness(0, 0, Space.Xs, Space.Xs);
            wrap.Children.Add(chip);
        }

        return wrap;
    }

    private static TextBlock Caption(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = 9.5,
        LetterSpacing = 0.6,
        Foreground = OrblitColors.InkDim,
        Margin = new Thickness(0, Space.Sm, 0, 3),
    };

    // A box typed into: what it is called, the step its typing makes, and what it shows while empty.
    private sealed record Typed(string Label, string Step, string? Hint = null);
}
