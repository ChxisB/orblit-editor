namespace Orblit.Editor.Panels;

using Orblit.Editor.Core;
using Orblit.Sequence;

/// <summary>
/// What the mark strip does to the shown clip. A mark is a named moment, and each change is an edit
/// through the undo stack.
/// </summary>
/// <remarks>
/// A mark is told by its place in the clip's list, which is in time order. The index a flag was drawn
/// with is the one to pass back, because the next edit redraws every flag.
/// </remarks>
public sealed class MarkModel(ClipBench bench)
{
    /// <summary>Adds a mark at the playhead's frame. A blank name is ignored.</summary>
    public void Add(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var at = bench.Frame;
        bench.Edit($"Add mark {name}", clip => clip.CopyWith(marks: [.. clip.Marks, new Mark(at, name)]));
    }

    /// <summary>Renames a mark and keeps where it is and what it carries. A blank or unchanged name is ignored.</summary>
    public void Rename(int index, string name)
    {
        if (!string.IsNullOrWhiteSpace(name) && MarkAt(index) is { } mark && mark.Name != name)
        {
            Replace(index, $"Rename mark {mark.Name}", new Mark(mark.At, name, mark.Payload));
        }
    }

    /// <summary>Moves a mark to the playhead's frame.</summary>
    public void MoveToPlayhead(int index)
    {
        if (MarkAt(index) is { } mark)
        {
            Replace(index, $"Move mark {mark.Name}", new Mark(bench.Frame, mark.Name, mark.Payload));
        }
    }

    /// <summary>Takes a mark away.</summary>
    public void Delete(int index)
    {
        if (MarkAt(index) is { } mark)
        {
            Replace(index, $"Delete mark {mark.Name}", null);
        }
    }

    private Mark? MarkAt(int index) =>
        bench.Clip is { } clip && index >= 0 && index < clip.Marks.Count ? clip.Marks[index] : null;

    // A null mark takes the one at the index out. The clip sorts the marks again when it is made.
    private void Replace(int index, string label, Mark? to) => bench.Edit(label, clip =>
    {
        var marks = clip.Marks.ToList();
        if (to is null)
        {
            marks.RemoveAt(index);
        }
        else
        {
            marks[index] = to;
        }

        return clip.CopyWith(marks: marks);
    });
}
