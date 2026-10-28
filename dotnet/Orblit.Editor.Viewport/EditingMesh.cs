namespace Orblit.Editor.Viewport;

using Orblit.Mesh;
using Orblit.VectorMath;

/// <summary>The mesh being edited part by part, and where its object stands in the world.</summary>
/// <param name="Mesh">The geometry as it is now.</param>
/// <param name="Transform">Takes a point of the mesh into the world, where the handles and the pointer are.</param>
public sealed record EditingMesh(Mesh Mesh, Matrix4 Transform);
