using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Scene;

/// <summary>
/// Rows that read and write the component of type <typeparamref name="T"/> on one object.
/// </summary>
/// <remarks>
/// A drag reads the component as it is at every step, because the drag is what is changing it.
/// </remarks>
/// <param name="fields">What the rows are built with.</param>
/// <param name="target">The object, and where the changes go.</param>
/// <param name="type">The key the component has on the object.</param>
internal sealed class ComponentRows<T>(Fields fields, InspectorTarget target, string type)
    where T : SceneComponent
{
    private static readonly string[] NoYes = ["No", "Yes"];

    public T? Current => PhysicsComponents.Of<T>(target.Item, type);

    public Control Drag(string label, Func<T, double> read, Func<T, double, T> change, DragOptions? options = null) =>
        Drags(label, component => [read(component)], (component, values) => change(component, values[0]), options);

    /// <summary>A few numbers that belong together, such as a direction.</summary>
    public Control Drags(string label, Func<T, double[]> read, Func<T, double[], T> change, DragOptions? options = null)
    {
        var shown = Current is { } first ? read(first) : [0.0];
        return fields.Drag(
            label,
            () =>
            {
                // Taking the component off refreshes the rows once before they are rebuilt, and they keep their width.
                if (Current is { } component)
                {
                    shown = read(component);
                }

                return shown;
            },
            values => Change(component => change(component, values)),
            options);
    }

    public Control Toggle(string label, Func<T, bool> read, Func<T, bool, T> change)
    {
        var on = Current is { } component && read(component);
        return fields.Choice(
            label,
            NoYes,
            on ? "Yes" : "No",
            chosen =>
            {
                if ((chosen == "Yes") != on)
                {
                    Change(component => change(component, chosen == "Yes"));
                }
            });
    }

    /// <summary>A text field, which settles when it loses focus.</summary>
    public Control Text(string label, Func<T, string> read, Func<T, string, T> change, string? hint = null) =>
        fields.Entry(label, Current is { } component ? read(component) : "", text => Change(component => change(component, text)), hint);

    /// <summary>One of <paramref name="options"/>, by the name <paramref name="read"/> gives it and <paramref name="change"/> takes.</summary>
    public Control Choice(string label, IReadOnlyList<string> options, Func<T, string> read, Func<T, string, T> change)
    {
        var selected = Current is { } component ? read(component) : options[0];
        return fields.Choice(
            label,
            options,
            selected,
            chosen =>
            {
                if (chosen != selected)
                {
                    Change(component => change(component, chosen));
                }
            });
    }

    public Control Add(string label, string tooltip, T made, string undo) =>
        Fields.Button(label, tooltip, () => fields.Host.Apply(() => Put(made, undo)));

    public Control Remove(string label, string tooltip, string undo) =>
        Fields.Button(label, tooltip, () => fields.Host.Apply(() => Put(null, undo)));

    /// <summary>Replaces the component with <paramref name="next"/>, or takes it off for null.</summary>
    public void Put(T? next, string? label = null)
    {
        // Taking off what is not there would leave a step in the undo list that does nothing.
        if (next is null && Current is null)
        {
            return;
        }

        var item = target.Item;
        target.History.Run(new SetObjectComponent(target.SceneId, item.Id, label ?? $"Set {item.Name} {type}", type, Current, next));
    }

    /// <summary>Replaces the component with what <paramref name="change"/> makes of it as it is now, if it is there.</summary>
    public void Change(Func<T, T> change, string? label = null)
    {
        if (Current is { } component)
        {
            Put(change(component), label);
        }
    }
}
