namespace Orblit.Editor.Panels;

using Orblit.Editor.Core;
using Orblit.VectorMath;

/// <summary>
/// A drag in the coordinate view, turned into what the session is told. The coordinates are the
/// view's own, so nothing here has to know how big it is drawn.
/// </summary>
public sealed class UvDrag(UvGesture gesture, Vector2 centre, Vector2 grabbed)
{
    // The pointer as the last frame left it, so each frame says how far it went since.
    private Vector2 _held = grabbed.Clone();

    public void Move(Vector2 now, EditorSession session)
    {
        switch (gesture)
        {
            case UvGesture.Move:
                session.NudgeUvs(now - _held);
                break;
            case UvGesture.Scale:
                session.ScaleUvs(ScaleBy(now));
                break;
            default:
                session.TurnUvs(TurnBy(now));
                break;
        }

        _held = now.Clone();
    }

    // How much further from the middle the pointer is than it was, along both axes together, because
    // a corner handle is what this gesture is: dragging out makes it bigger.
    private Vector2 ScaleBy(Vector2 now)
    {
        var was = _held - centre;
        var is_ = now - centre;
        return new Vector2(
            Math.Abs(was.X) < 1e-6 ? 1 : is_.X / was.X,
            Math.Abs(was.Y) < 1e-6 ? 1 : is_.Y / was.Y);
    }

    private double TurnBy(Vector2 now)
    {
        var was = Math.Atan2(_held.Y - centre.Y, _held.X - centre.X);
        var is_ = Math.Atan2(now.Y - centre.Y, now.X - centre.X);
        return (is_ - was) * 180 / Math.PI;
    }
}
