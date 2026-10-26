using Avalonia.Controls;
using Avalonia.Input;

namespace Orblit.Editor;

/// <summary>A scene object dragged out of the hierarchy, and the places that take one.</summary>
internal static class ObjectDrops
{
    /// <summary>What the drag carries: the id of the object.</summary>
    public static readonly DataFormat<string> Format = DataFormat.CreateStringApplicationFormat("orblit.object");

    /// <summary>
    /// Makes <paramref name="target"/> take a dragged object.
    /// </summary>
    /// <param name="target">What the object is dropped on.</param>
    /// <param name="dropped">Told the object's id when it lands.</param>
    /// <param name="over">Told whether an object is held over the target, so it can say so.</param>
    public static void Accept(Control target, Action<string> dropped, Action<bool> over)
    {
        DragDrop.SetAllowDrop(target, true);
        target.AddHandler(DragDrop.DragOverEvent, (_, drag) =>
        {
            var carrying = drag.DataTransfer.TryGetValue(Format) is not null;
            drag.DragEffects = carrying ? DragDropEffects.Copy : DragDropEffects.None;
            over(carrying);
        });
        target.AddHandler(DragDrop.DragLeaveEvent, (_, _) => over(false));
        target.AddHandler(DragDrop.DropEvent, (_, drop) =>
        {
            over(false);
            if (drop.DataTransfer.TryGetValue(Format) is { } id)
            {
                drop.Handled = true;
                dropped(id);
            }
        });
    }
}
