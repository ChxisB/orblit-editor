using Avalonia.Controls;
using Avalonia.Input;

namespace Orblit.Editor;

/// <summary>A project file dragged out of the browser, and the places that take one.</summary>
internal static class AssetDrops
{
    /// <summary>What the drag carries: the file's full path.</summary>
    public static readonly DataFormat<string> Format = DataFormat.CreateStringApplicationFormat("orblit.asset");

    /// <summary>Starts dragging the file at <paramref name="path"/> from the press that picked it up.</summary>
    public static async void Drag(PointerPressedEventArgs press, string path)
    {
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(Format, path));
        await DragDrop.DoDragDropAsync(press, data, DragDropEffects.Copy);
    }

    /// <summary>
    /// Makes <paramref name="target"/> take a dragged file that <paramref name="takes"/> says yes to.
    /// </summary>
    /// <param name="target">What the file is dropped on.</param>
    /// <param name="takes">Whether a file at this path is one the target can use.</param>
    /// <param name="dropped">Told the file's path when it lands.</param>
    /// <param name="over">Told whether a file it takes is held over the target, so it can say so.</param>
    public static void Accept(Control target, Func<string, bool> takes, Action<string> dropped, Action<bool> over)
    {
        DragDrop.SetAllowDrop(target, true);
        target.AddHandler(DragDrop.DragOverEvent, (_, drag) =>
        {
            var carrying = drag.DataTransfer.TryGetValue(Format) is { } path && takes(path);
            drag.DragEffects = carrying ? DragDropEffects.Copy : DragDropEffects.None;
            drag.Handled = true;
            over(carrying);
        });
        target.AddHandler(DragDrop.DragLeaveEvent, (_, _) => over(false));
        target.AddHandler(DragDrop.DropEvent, (_, drop) =>
        {
            over(false);
            if (drop.DataTransfer.TryGetValue(Format) is { } path && takes(path))
            {
                drop.Handled = true;
                dropped(path);
            }
        });
    }
}
