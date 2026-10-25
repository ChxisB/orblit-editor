namespace Orblit.Editor.Viewport;

using Orblit.Filament;
using Orblit.Mesh;
using Orblit.Sprite;
using Orblit.VectorMath;

/// <summary>Where the grid is and how big its squares are, for one frame.</summary>
/// <param name="Mesh">
/// The quad. Absolute, like the texture: the renderer is handed paths, not project-relative
/// references, and the grid does not go through the resolver an object's mesh does.
/// </param>
/// <param name="Texture">The lines, absolute, because that is what a material takes.</param>
/// <param name="Centre">Where it is centred, already snapped.</param>
/// <param name="Extent">How far across the whole thing is.</param>
/// <param name="Step">How big one square is.</param>
public sealed record GridPlan(string Mesh, string Texture, Vector3 Centre, double Extent, double Step)
{
    /// <summary>
    /// How many squares across. Fixed, which is what makes the grid tell somebody the size of the
    /// snap rather than the size of the world.
    /// </summary>
    public const int Across = 100;

    /// <summary>
    /// The key the renderer knows the grid by. Chosen far from anything an object gets, because
    /// the grid is not an object and never will be one.
    /// </summary>
    public const int RenderKey = -777;

    public const int MaterialKey = -777;
}

/// <summary>
/// The grid, as something in the scene rather than something drawn over it.
///
/// A grid painted on top of the viewport is a grid that shows through walls. This one is a surface
/// in the world: unlit, blended, writing no depth, and therefore hidden by whatever is in front of
/// it. That is what makes it read as a floor somebody is standing on rather than as an overlay.
///
/// It follows the camera, snapped to its own squares, so it never runs out and never appears to
/// slide.
/// </summary>
/// <param name="projectRoot">The project folder, where the quad and the lines are kept.</param>
public sealed class GridStore(string projectRoot)
{
    // The lines are ten squares by ten, so the tile is a whole number of pixels per square.
    private const int Size = 512;
    private const int Cells = 10;
    private const int Cell = Size / Cells;

    private string? _mesh;
    private string? _texture;
    private bool _building;

    /// <summary>
    /// The quad and the lines, made once.
    ///
    /// A unit square, scaled by the transform, so changing the snap size never rewrites a file.
    /// And one image of ten squares by ten, repeated ten times, which is where the heavier line
    /// every tenth comes from.
    /// </summary>
    public async Task PrepareAsync()
    {
        if (_building || _mesh is not null)
        {
            return;
        }

        _building = true;
        try
        {
            var folder = Directory.CreateDirectory(Path.Combine(projectRoot, ".orblit"));

            // One face. The material culls nothing, so it is there when the camera drops below
            // it. A second face turned the other way would only mean drawing the same pixels
            // twice, and this is a screen-filling blended surface where twice is expensive.
            var quad = new Mesh(
                positions:
                [
                    new Vector3(-0.5, 0, -0.5),
                    new Vector3(-0.5, 0, 0.5),
                    new Vector3(0.5, 0, 0.5),
                    new Vector3(0.5, 0, -0.5),
                ],
                faces: [new Face([0, 1, 2, 3])]);
            var mesh = Path.Combine(folder.FullName, "grid.glb");
            await File.WriteAllBytesAsync(mesh, quad.ToGlb("grid"));

            var lines = Path.Combine(folder.FullName, "grid.png");
            await File.WriteAllBytesAsync(lines, PngCodec.Encode(Size, Size, Draw()));

            _mesh = mesh;
            _texture = lines;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A project folder that will not take a file is a project where the grid does not
            // show. Everything else still works, which beats refusing to open the scene.
            _mesh = null;
            _texture = null;
        }
        finally
        {
            _building = false;
        }
    }

    /// <summary>Where the grid should be this frame, or null when it is off or not built.</summary>
    public GridPlan? PlanFor(Snapping snapping, Vector3 lookingAt)
    {
        var mesh = _mesh;
        var texture = _texture;
        if (!snapping.On || snapping.Step <= 0 || mesh is null || texture is null)
        {
            return null;
        }

        // Snapped to its own squares, so it slides under the camera in whole cells and never
        // appears to move.
        double Snapped(double at) => Math.Round(at / snapping.Step, MidpointRounding.AwayFromZero) * snapping.Step;

        return new GridPlan(
            mesh,
            texture,
            new Vector3(Snapped(lookingAt.X), 0, Snapped(lookingAt.Z)),
            snapping.Step * GridPlan.Across,
            snapping.Step);
    }

    /// <summary>
    /// Ten squares by ten, with the edges of the tile heavier than the lines inside it. That is
    /// where the every-tenth line comes from once the image is repeated ten times across the quad.
    /// </summary>
    private static byte[] Draw()
    {
        var pixels = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var at = ((y * Size) + x) * 4;
                pixels[at] = 255;
                pixels[at + 1] = 255;
                pixels[at + 2] = 255;
                pixels[at + 3] = AlphaAt(x, y);
            }
        }

        return pixels;
    }

    private static byte AlphaAt(int x, int y)
    {
        if (x < 2 || x >= Size - 1 || y < 2 || y >= Size - 1)
        {
            return 150;
        }

        return x % Cell == 0 || y % Cell == 0 ? (byte)60 : (byte)0;
    }
}

/// <summary>The grid as the renderer takes it.</summary>
public static class GridAsScene
{
    /// <summary>
    /// Where the quad stands: flat, centred, and scaled to the whole extent.
    ///
    /// Exactly on nought. It used to sit a hair below to lose the depth test against anything
    /// built on the ground plane, which worked at that height and nowhere else: a surface dragged
    /// through the plane still met it. The material's depth bias does that job properly and at
    /// every height.
    /// </summary>
    public static Matrix4 Transform(this GridPlan plan)
    {
        var matrix = Matrix4.Identity();
        matrix.SetTranslation(new Vector3(plan.Centre.X, 0, plan.Centre.Z));
        matrix.Multiply(Matrix4.Diagonal3Values(plan.Extent, 1, plan.Extent));
        return matrix;
    }

    public static RenderObject ToObject(this GridPlan plan) => new()
    {
        Key = GridPlan.RenderKey,
        Transform = plan.Transform(),
        Colour = new Vector3(1, 1, 1),
        Mesh = plan.Mesh,
        Material = GridPlan.MaterialKey,
        // It is a drawing aid, not a thing in the world: it casts nothing and catches nothing.
        CastShadows = false,
        ReceiveShadows = false,
    };

    public static RenderMaterial ToMaterial(this GridPlan plan) => new()
    {
        Key = GridPlan.MaterialKey,
        Shading = MaterialShading.Unlit,
        Blend = MaterialBlend.Fade,
        Culling = MaterialCulling.None,
        // Writing depth would make the grid hide what is behind it, and it is a hint about where
        // the floor is rather than a floor.
        DepthWrite = false,
        // And behind anything sharing its plane. A floor built on the ground plane, or a surface
        // dragged through it, is at the same depth as the grid for a moment, and two things at
        // the same depth flicker pixel by pixel as the camera moves. The grid is a hint about
        // where the ground is; it should lose every time.
        DepthBias = 0.002,
        BaseColour = new Vector4(0.62, 0.68, 0.78, 0.75),
        // Ten across the image and ten images across the quad: a hundred squares, each one snap
        // step.
        Tiling = new Vector2(10, 10),
        BaseColourMap = new Texture(plan.Texture),
    };
}
