namespace Orblit.Editor.Tests;

using Orblit.Editor.Viewport;

public sealed class ViewportInputTests
{
    private readonly List<string> _seen = [];

    // A stage that notes everything it is shown and takes what it is told.
    private ViewportStage Stage(string name, params ViewportPhase[] takes) =>
        new(name, gesture =>
        {
            _seen.Add($"{name} {PhaseName(gesture.Phase)}");
            return takes.Contains(gesture.Phase);
        });

    private static string PhaseName(ViewportPhase phase) => phase switch
    {
        ViewportPhase.Hover => "hover",
        ViewportPhase.Leave => "leave",
        ViewportPhase.Tap => "tap",
        ViewportPhase.DragStart => "dragStart",
        ViewportPhase.DragUpdate => "dragUpdate",
        ViewportPhase.DragEnd => "dragEnd",
        ViewportPhase.DragCancel => "dragCancel",
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };

    private static ViewportGesture At(ViewportPhase phase) => new(phase, Offset.Zero);

    [Fact]
    public void TheFirstStageToTakeAGestureIsTheOnlyOneToSeeIt()
    {
        var order = new ViewportInputOrder(() =>
        [
            Stage("tool"),
            Stage("gizmo", ViewportPhase.Tap),
            Stage("selection", ViewportPhase.Tap),
        ]);

        Assert.Equal("gizmo", order.Handle(At(ViewportPhase.Tap)));
        Assert.Equal(["tool tap", "gizmo tap"], _seen);
    }

    [Fact]
    public void NothingTakingAGestureSaysSo()
    {
        var order = new ViewportInputOrder(() => [Stage("tool")]);
        Assert.Null(order.Handle(At(ViewportPhase.Tap)));
    }

    [Fact]
    public void ADragStaysWithTheStageThatTookItsStart()
    {
        List<ViewportStage> stages =
        [
            Stage("tool", ViewportPhase.DragStart),
            Stage("gizmo", ViewportPhase.DragStart),
        ];
        var order = new ViewportInputOrder(() => stages);

        Assert.Equal("tool", order.Handle(At(ViewportPhase.DragStart)));
        Assert.Equal("tool", order.Holding);

        // Whatever the order has become since, the rest of the drag is the tool's: half a brush
        // stroke is not something to hand to the handles.
        stages = [stages[^1]];
        order.Handle(At(ViewportPhase.DragUpdate));
        order.Handle(At(ViewportPhase.DragEnd));

        Assert.Equal(["tool dragStart", "tool dragUpdate", "tool dragEnd"], _seen);
        Assert.Null(order.Holding);
    }

    [Fact]
    public void ADragNobodyTookGoesNowhere()
    {
        var order = new ViewportInputOrder(() => [Stage("selection")]);
        order.Handle(At(ViewportPhase.DragStart));
        _seen.Clear();

        Assert.Null(order.Handle(At(ViewportPhase.DragUpdate)));
        Assert.Null(order.Handle(At(ViewportPhase.DragEnd)));
        Assert.Empty(_seen);
    }

    [Fact]
    public void AHoverTakenAheadIsALeaveBehind()
    {
        var order = new ViewportInputOrder(() =>
        [
            Stage("tool"),
            Stage("gizmo", ViewportPhase.Hover),
            Stage("selection", ViewportPhase.Hover),
        ]);

        Assert.Equal("gizmo", order.Handle(At(ViewportPhase.Hover)));
        // The stage ahead looked and passed; the one behind is told the pointer is not over
        // anything of its own, so its highlight goes out.
        Assert.Equal(["tool hover", "gizmo hover", "selection leave"], _seen);
    }

    [Fact]
    public void LeavingTheViewIsALeaveForEverybody()
    {
        var order = new ViewportInputOrder(() => [Stage("tool"), Stage("gizmo"), Stage("selection")]);
        order.Leave();
        Assert.Equal(["tool leave", "gizmo leave", "selection leave"], _seen);
    }

    [Fact]
    public void ACancelledDragIsCancelledWhereItWas()
    {
        Offset? where = null;
        var order = new ViewportInputOrder(() =>
        [
            new ViewportStage("tool", gesture =>
            {
                where = gesture.At;
                return true;
            }),
        ]);
        order.Handle(new ViewportGesture(ViewportPhase.DragStart, new Offset(1, 1)));
        order.Handle(new ViewportGesture(ViewportPhase.DragUpdate, new Offset(4, 5)));
        order.Cancel();

        Assert.Equal(new Offset(4, 5), where);
        Assert.Null(order.Holding);
    }

    [Fact]
    public void ADragWhoseEndWentMissingIsCancelledByTheNext()
    {
        var order = new ViewportInputOrder(() => [Stage("tool", ViewportPhase.DragStart)]);
        order.Handle(At(ViewportPhase.DragStart));
        order.Handle(At(ViewportPhase.DragStart));

        Assert.Equal(["tool dragStart", "tool dragCancel", "tool dragStart"], _seen);
    }
}
