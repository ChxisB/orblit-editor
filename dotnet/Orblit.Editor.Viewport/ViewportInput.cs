namespace Orblit.Editor.Viewport;

using System.Diagnostics;

/// <summary>Which part of a pointer's movement a <see cref="ViewportGesture"/> is.</summary>
public enum ViewportPhase
{
    /// <summary>Moving over the view with nothing held down.</summary>
    Hover,

    /// <summary>
    /// The pointer is no longer over whatever a stage last showed as under it: it left the view,
    /// or a stage ahead of this one took the hover.
    /// </summary>
    Leave,

    /// <summary>A click.</summary>
    Tap,

    /// <summary>
    /// A drag beginning. Whichever stage takes it is handed the rest of the drag, and no other
    /// stage sees any of it.
    /// </summary>
    DragStart,

    DragUpdate,

    DragEnd,

    /// <summary>A drag that stopped without finishing.</summary>
    DragCancel,
}

/// <summary>One thing the pointer did in a scene view.</summary>
/// <param name="Phase">Which part of the movement this is.</param>
/// <param name="At">Where, in the view's own pixels.</param>
/// <param name="Add">
/// Whether a modifier that adds to a selection, rather than replacing it, was held.
/// </param>
/// <param name="Projection">
/// How the view's pixels map into the world, or null before the view has been laid out. A brush
/// turns <paramref name="At"/> into a ray through this.
/// </param>
public sealed record ViewportGesture(
    ViewportPhase Phase,
    Offset At,
    bool Add = false,
    ViewportProjection? Projection = null)
{
    internal ViewportGesture As(ViewportPhase phase) => this with { Phase = phase };
}

/// <summary>One place in a <see cref="ViewportInputOrder"/>: who it is, and what it does.</summary>
/// <param name="Name">Who the stage is.</param>
/// <param name="Input">Takes a gesture, and says whether it did.</param>
public sealed record ViewportStage(string Name, Func<ViewportGesture, bool> Input);

/// <summary>
/// Who gets a gesture in a scene view, and in what order.
///
/// The stages are asked in turn and the first that takes a gesture is the only one that sees it.
/// The order is the whole point: a brush that cannot take a drag before the handles do is a brush
/// that moves objects when somebody meant to sculpt, so the order is written down here as a list
/// and tested, rather than being whatever order a run of <c>if</c>s happened to be in.
///
/// The stages are read again for each gesture, because what is in the order depends on what is
/// selected. A drag is the exception: the stage that took its start keeps it to the end, even if
/// the list has changed under it.
/// </summary>
/// <param name="stages">The stages, first to last.</param>
public sealed class ViewportInputOrder(Func<IEnumerable<ViewportStage>> stages)
{
    private ViewportStage? _holder;

    private Offset _last = Offset.Zero;

    /// <summary>The stage that has the drag in progress, if one does.</summary>
    public string? Holding => _holder?.Name;

    /// <summary>
    /// Hands <paramref name="gesture"/> down the order, and says which stage took it.
    ///
    /// Null when nothing did. A hover taken by one stage is a <see cref="ViewportPhase.Leave"/>
    /// for every stage behind it, so a highlight further down the order goes out rather than
    /// staying lit under the one that won.
    /// </summary>
    public string? Handle(ViewportGesture gesture) => gesture.Phase switch
    {
        ViewportPhase.Leave => Left(gesture.At),
        ViewportPhase.DragUpdate => UpdateDrag(gesture),
        ViewportPhase.DragEnd or ViewportPhase.DragCancel => EndDrag(gesture),
        ViewportPhase.DragStart => StartDrag(gesture),
        ViewportPhase.Hover => Hover(gesture),
        ViewportPhase.Tap => First(gesture, stages())?.Name,
        _ => throw new UnreachableException(),
    };

    /// <summary>Tells every stage the pointer has gone.</summary>
    public void Leave(Offset at = default)
    {
        foreach (var stage in stages())
        {
            stage.Input(new ViewportGesture(ViewportPhase.Leave, at));
        }
    }

    /// <summary>Ends the drag in progress without finishing it, where it last was.</summary>
    public void Cancel() => Handle(new ViewportGesture(ViewportPhase.DragCancel, _last));

    private string? Left(Offset at)
    {
        Leave(at);
        return null;
    }

    private string? UpdateDrag(ViewportGesture gesture)
    {
        _last = gesture.At;
        _holder?.Input(gesture);
        return _holder?.Name;
    }

    private string? EndDrag(ViewportGesture gesture)
    {
        var holder = _holder;
        _holder = null;
        holder?.Input(gesture);
        return holder?.Name;
    }

    private string? StartDrag(ViewportGesture gesture)
    {
        // A start with a drag still held means the end went missing. The stage holding it is told
        // it is over, rather than being left mid-drag for ever.
        if (_holder is not null)
        {
            Cancel();
        }

        _last = gesture.At;
        var taker = First(gesture, stages());
        _holder = taker;
        return taker?.Name;
    }

    private string? Hover(ViewportGesture gesture)
    {
        var all = stages().ToList();
        var taker = First(gesture, all);
        if (taker is not null)
        {
            foreach (var stage in all.Skip(all.IndexOf(taker) + 1))
            {
                stage.Input(gesture.As(ViewportPhase.Leave));
            }
        }

        return taker?.Name;
    }

    private static ViewportStage? First(ViewportGesture gesture, IEnumerable<ViewportStage> stages)
    {
        foreach (var stage in stages)
        {
            if (stage.Input(gesture))
            {
                return stage;
            }
        }

        return null;
    }
}
