using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;

/// <summary>
/// The fields of whatever is chosen in the blend graph: the blend itself when nothing is, a state, or
/// an arrow.
/// </summary>
internal sealed class BlendInspector : UserControl
{
    private readonly BlendBench _bench;
    private readonly Func<IReadOnlyList<string>> _clips;
    private readonly FieldHost _host;
    private readonly Fields _fields;
    private readonly StackPanel _stack = new() { Name = "blend_fields", Margin = new Thickness(0, Space.Sm) };

    /// <param name="bench">What is chosen, and what a field changes.</param>
    /// <param name="history">What a field settles into one step.</param>
    /// <param name="clips">The clips a state could play, read when the fields are built.</param>
    public BlendInspector(BlendBench bench, History history, Func<IReadOnlyList<string>> clips)
    {
        _bench = bench;
        _clips = clips;
        _host = new FieldHost(history, Rebuild);
        _fields = new Fields(_host);
        Name = "blend_inspector";
        Background = OrblitColors.Surface;
        Content = new ScrollViewer { Name = "blend_scroll", Content = _stack };
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _bench.Changed += _host.Changed;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _bench.Changed -= _host.Changed;
        base.OnDetachedFromVisualTree(e);
    }

    private void Rebuild()
    {
        _host.Reset();
        _stack.Children.Clear();
        _stack.Children.Add(Shown());
    }

    private Control Shown()
    {
        if (_bench.Blend is not { } blend)
        {
            return Fields.Note("No blend is open.");
        }

        var rows = new BlendRows(_fields, _bench);
        var clips = _clips();
        return _bench.Pick switch
        {
            StatePicked picked when blend.WrittenStates.FirstOrDefault(one => one.Name == picked.Name) is { } state =>
                new BlendStateFields(rows, blend, state, clips).Build(),
            ChangePicked picked when picked.Index >= 0 && picked.Index < blend.WrittenChanges.Count =>
                new BlendChangeFields(rows, blend, picked.Index).Build(),
            _ => new BlendOverviewFields(rows, blend, clips).Build(),
        };
    }
}
