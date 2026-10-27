namespace Orblit.Editor.Core;

using System.Diagnostics;
using Orblit.Mesh;
using Orblit.VectorMath;
using MeshEdge = (int, int);

/// <summary>
/// What the editor is pointed at.
/// </summary>
/// <remarks>
/// Two contexts rather than four peer modes, which is how ProBuilder has it and is right: moving a
/// whole object and moving one of its corners are different jobs with different handles, and the
/// escape key between them is the one thing somebody presses without thinking.
/// </remarks>
public enum EditContext
{
    /// <summary>The whole object: move it, rotate it, scale it, put it somewhere.</summary>
    Whole,

    /// <summary>Its parts.</summary>
    Element,
}

public static class EditContexts
{
    public static string Label(this EditContext context) => context switch
    {
        EditContext.Whole => "Object",
        EditContext.Element => "Edit",
        _ => throw new UnreachableException(),
    };
}

/// <summary>Which part of a mesh is being selected.</summary>
public enum ElementMode
{
    Vertex,
    Edge,
    Face,
}

public static class ElementModes
{
    public static string Label(this ElementMode mode) => mode switch
    {
        ElementMode.Vertex => "Vertices",
        ElementMode.Edge => "Edges",
        ElementMode.Face => "Faces",
        _ => throw new UnreachableException(),
    };

    /// <summary>The next one round, for the key that cycles them.</summary>
    public static ElementMode Next(this ElementMode mode) => (ElementMode)(((int)mode + 1) % Enum.GetValues<ElementMode>().Length);
}

/// <summary>How a click changes what is selected.</summary>
public enum SelectHow
{
    /// <summary>Replace what was there.</summary>
    Replace,

    /// <summary>Add to it, or take away what is already in it.</summary>
    Toggle,
}

/// <summary>
/// What is selected inside one mesh.
/// </summary>
/// <remarks>
/// Indices and edges rather than objects, because faces are objects and a selection that held
/// them would go stale the moment somebody extruded: every operation here makes new faces and
/// drops old ones. Positions in the list survive that. A face that has gone is a number that no
/// longer resolves, which is checked rather than crashed on.
/// </remarks>
public sealed class ElementSelection
{
    public ElementSelection(
        IEnumerable<int>? vertices = null,
        IEnumerable<MeshEdge>? edges = null,
        IEnumerable<int>? faces = null)
    {
        Vertices = new OrderedSet<int>(vertices ?? []);
        Edges = new OrderedSet<MeshEdge>(edges ?? []);
        Faces = new OrderedSet<int>(faces ?? []);
    }

    // Ordered, because the order somebody picked things in is the order a bridge or a hole fill
    // is wound in.
    public OrderedSet<int> Vertices { get; private set; }

    public OrderedSet<MeshEdge> Edges { get; private set; }

    /// <summary>Faces by position in the mesh's list.</summary>
    public OrderedSet<int> Faces { get; private set; }

    public bool IsEmpty => Vertices.Count == 0 && Edges.Count == 0 && Faces.Count == 0;

    public int CountIn(ElementMode mode) => mode switch
    {
        ElementMode.Vertex => Vertices.Count,
        ElementMode.Edge => Edges.Count,
        ElementMode.Face => Faces.Count,
        _ => throw new UnreachableException(),
    };

    // The engine's ordered set has no Clear, so the sets are replaced. Nothing holds one across a
    // clear: the session reads them from here each time.
    public void Clear()
    {
        Vertices = [];
        Edges = [];
        Faces = [];
    }

    /// <summary>The faces themselves, skipping any the mesh no longer has.</summary>
    public List<Face> FacesIn(Mesh mesh) => [.. Faces.Where(at => at >= 0 && at < mesh.Faces.Count).Select(at => mesh.Faces[at])];

    /// <summary>
    /// Everything the selection touches, whatever mode made it, which is what a transform handle
    /// needs.
    /// </summary>
    public HashSet<int> PointsIn(Mesh mesh)
    {
        var points = Vertices.Where(index => index >= 0 && index < mesh.Positions.Count).ToHashSet();
        foreach (var (a, b) in Edges)
        {
            points.Add(a);
            points.Add(b);
        }

        foreach (var face in FacesIn(mesh))
        {
            points.UnionWith(face.Vertices);
        }

        return points;
    }

    /// <summary>Where the handles should sit, or null when nothing is selected.</summary>
    public Vector3? PivotIn(Mesh mesh)
    {
        var points = PointsIn(mesh);
        if (points.Count == 0)
        {
            return null;
        }

        var sum = Vector3.Zero();
        foreach (var index in points.Where(index => index >= 0 && index < mesh.Positions.Count))
        {
            sum.Add(mesh.Positions[index]);
        }

        sum.Scale(1.0 / points.Count);
        return sum;
    }

    public ElementSelection Copy() => new(Vertices, Edges, Faces);
}
