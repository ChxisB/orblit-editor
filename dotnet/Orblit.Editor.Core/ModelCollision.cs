namespace Orblit.Editor.Core;

using Orblit.Asset;
using Orblit.Mesh;

/// <summary>Reads the geometry an object draws, in its own frame, as something a body can collide with.</summary>
/// <param name="root">The project directory a model's path is relative to.</param>
public sealed class ModelCollision(string root)
{
    private readonly DirectoryAssetSource _source = new(root);

    /// <summary>
    /// The triangles of what the object builds itself, or of the model file it points at. Throws
    /// <see cref="FormatException"/> for an object with neither, and what the readers throw for a file
    /// that is missing or is not a model.
    /// </summary>
    public async Task<CollisionMesh> ReadAsync(SceneObject item)
    {
        if (item.CurrentMesh is { IsEmpty: false } mesh)
        {
            var triangles = mesh.Triangulate();
            return new CollisionMesh([.. triangles.Positions.Select(value => (double)value)], [.. triangles.Indices.Select(index => (int)index)]);
        }

        if (item.MeshAsset is not { } path)
        {
            throw new FormatException("This object has no model geometry.");
        }

        var id = AssetId.Parse(path);
        var bytes = await _source.ReadAsync(id).ConfigureAwait(false);
        return id.Extension == "obj"
            ? CollisionMesh.FromObj(bytes)
            : await CollisionMesh.FromGltfAsync(id, bytes, _source).ConfigureAwait(false);
    }
}
