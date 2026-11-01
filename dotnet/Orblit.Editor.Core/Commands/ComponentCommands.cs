namespace Orblit.Editor.Core;

using Doc = Orblit.Scene;

// The components a row carries without fields of its own for them.

/// <summary>
/// Puts a component on an object, replaces the one it has, or takes it off.
/// </summary>
/// <remarks>
/// One command for every component in <see cref="SceneObject.Components"/> rather than one per
/// field, because those components are replaced whole: a body with a heavier mass is a new body.
/// That is also what makes undo cheap. From is the component that was there, not a copy of it.
/// <para>
/// Null on either side means none: From null is adding one, To null is taking it off.
/// </para>
/// </remarks>
public sealed class SetObjectComponent(
    string sceneId,
    string id,
    string label,
    string type,
    Doc.SceneComponent? from,
    Doc.SceneComponent? to) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public override string Label { get; } = label;

    /// <summary>Which component, as it is keyed on the entity.</summary>
    public string Type { get; } = type;

    public Doc.SceneComponent? From { get; } = from;

    /// <summary>Not fixed: a merged run of drags rewrites where it ends up.</summary>
    public Doc.SceneComponent? To { get; private set; } = to;

    /// <summary>
    /// A drag through one component's fields is one step. Adding one or taking it off is a step of
    /// its own, so undoing a slider never takes the component with it.
    /// </summary>
    public override object? MergeKey => From is null || To is null ? null : (Id, "component", Type);

    public override void Absorb(EditorCommand later)
    {
        if (later is SetObjectComponent other)
        {
            To = other.To;
        }
    }

    public override void Apply(ISceneHost host) => Put(host, To);

    public override void Revert(ISceneHost host) => Put(host, From);

    private void Put(ISceneHost host, Doc.SceneComponent? component)
    {
        if (host.SceneFor(SceneId)?[Id]?.Components is not { } components)
        {
            return;
        }

        if (component is null)
        {
            components.Remove(Type);
        }
        else
        {
            components[Type] = component;
        }
    }
}
