namespace Orblit.Editor.Panels;

using System.Globalization;
using Orblit.Editor.Core;
using Orblit.Filament;
using Orblit.Mesh;
using Orblit.VectorMath;
using IoFile = System.IO.File;
using IoPath = System.IO.Path;

/// <summary>What the preview has to show for the file picked.</summary>
public enum PreviewStage
{
    /// <summary>Nothing is picked.</summary>
    Nothing,

    /// <summary>A folder is picked, and has nothing to draw.</summary>
    Folder,

    /// <summary>A texture the picture decoder handles, shown as itself.</summary>
    Picture,

    /// <summary>A file that is not a model or a picture, so it is named and not drawn.</summary>
    Kind,

    /// <summary>A model, on a machine the renderer does not run on.</summary>
    Unavailable,

    /// <summary>A model, drawn by the renderer from the file.</summary>
    Model,
}

/// <summary>
/// What the asset preview shows and where it looks from.
/// </summary>
/// <remarks>
/// A model is drawn by the renderer from the file on disk, framed by the size the file declares and
/// turning slowly so its shape reads. Not a picture saved beside it, which stops being true the first
/// time the model changes. The two glTF containers declare their size and an <c>.obj</c> is read for
/// its points. Anything else opens at a distance that suits a human-sized object and is moved by
/// hand, because a confident guess at the wrong scale puts the model inside the camera or far off,
/// and both look like the renderer failing.
/// </remarks>
/// <param name="renderer">Whether this machine runs the renderer.</param>
public sealed class AssetPreviewModel(bool renderer)
{
    public const string UnavailableText = "The 3D viewport is not available on this platform yet.";

    private const double Away = 2.6;
    private const double TurnPerSecond = 0.5;
    private const double MinZoom = 0.3;
    private const double MaxZoom = 6;
    private const double MaxPitch = 1.4;
    private const double DragStep = 0.008;
    private const double ScrollStep = 0.0016;

    private (Vector3 Min, Vector3 Max)? _bounds;

    /// <summary>What is picked, or null when nothing is.</summary>
    public Asset? Asset { get; private set; }

    /// <summary>Whether the model is turning by itself. Taking hold of it turns that off.</summary>
    public bool Turning { get; private set; } = true;

    /// <summary>Radians round the model, from the front.</summary>
    public double Yaw { get; private set; } = 0.7;

    /// <summary>Radians above the model's middle.</summary>
    public double Pitch { get; private set; } = 0.3;

    /// <summary>How many times the framing distance the camera sits at.</summary>
    public double Zoom { get; private set; } = 1;

    public PreviewStage Stage => Asset switch
    {
        null => PreviewStage.Nothing,
        { IsFolder: true } => PreviewStage.Folder,
        { } asset when AssetBrowserModel.ShowsPicture(asset) => PreviewStage.Picture,
        { Kind: var kind } when kind != AssetKind.Mesh => PreviewStage.Kind,
        _ => renderer ? PreviewStage.Model : PreviewStage.Unavailable,
    };

    /// <summary>The heading: the file's name, or the panel's own where nothing is picked.</summary>
    public string Heading => Asset?.Name ?? "Preview";

    /// <summary>Whether there is a model to stop and start. It is offered even where the renderer is not.</summary>
    public bool OffersTurn => Asset?.Kind == AssetKind.Mesh;

    /// <summary>What a stage with nothing to draw says, rather than being left blank.</summary>
    public string Saying => Stage switch
    {
        PreviewStage.Nothing => "Select a file to see what is in it",
        PreviewStage.Folder => "A folder has nothing to show",
        PreviewStage.Unavailable => UnavailableText,
        PreviewStage.Kind => $"{Asset!.Kind.Label}: nothing to draw",
        _ => "",
    };

    /// <summary>The lines under the stage: what the file is, how big it is on disk, and the box a model fills.</summary>
    public IReadOnlyList<(string Label, string Value)> Facts()
    {
        if (Asset is not { } asset)
        {
            return [];
        }

        var facts = new List<(string, string)> { ("Kind", asset.Kind.Label) };
        if (asset.Size.Length > 0)
        {
            facts.Add(("Size", asset.Size));
        }

        if (_bounds is { } box)
        {
            var extent = box.Max - box.Min;
            facts.Add(("Extent", string.Create(CultureInfo.InvariantCulture, $"{extent.X:0.00} × {extent.Y:0.00} × {extent.Z:0.00}")));
        }

        return facts;
    }

    /// <summary>Picks a file, or nothing. The file's declared size is read here, once.</summary>
    // shortcut: a .glb is read whole to get at its JSON, so a very large one holds the interface for a moment. Read the header and the first chunk only if that shows.
    public void Show(Asset? asset)
    {
        Asset = asset;
        _bounds = asset?.Kind == AssetKind.Mesh ? BoundsOf(asset.Path) : null;
    }

    public void Turn(bool on) => Turning = on;

    /// <summary>
    /// Moves the camera round the model by a drag. Taking hold of it stops the turntable, because
    /// somebody dragging is trying to look at one side and a model that keeps turning under the
    /// pointer is one they cannot.
    /// </summary>
    public void Orbit(double dx, double dy)
    {
        Turning = false;
        Yaw -= dx * DragStep;
        Pitch = Math.Clamp(Pitch + (dy * DragStep), -MaxPitch, MaxPitch);
    }

    /// <summary>Moves the camera in or out by a scroll of <paramref name="dy"/>.</summary>
    public void Scroll(double dy) => Zoom = Math.Clamp(Zoom * (1 + (dy * ScrollStep)), MinZoom, MaxZoom);

    /// <summary>Lets <paramref name="seconds"/> pass. Only a model on screen turns.</summary>
    public void Tick(double seconds)
    {
        if (Turning && Stage == PreviewStage.Model)
        {
            Yaw += seconds * TurnPerSecond;
        }
    }

    /// <summary>The model seen from where the camera is, or null when the stage is not a model.</summary>
    public RenderScene? Scene()
    {
        if (Stage != PreviewStage.Model)
        {
            return null;
        }

        var (centre, radius) = Framing();
        var away = radius * Away * Zoom;
        var flat = away * Math.Cos(Pitch);
        return new RenderScene
        {
            Camera = new RenderCamera
            {
                Position = new Vector3(
                    centre.X + (flat * Math.Sin(Yaw)),
                    centre.Y + (away * Math.Sin(Pitch)),
                    centre.Z + (flat * Math.Cos(Yaw))),
                Target = centre,
                FieldOfView = 45,
            },
            Objects = [new RenderObject { Key = 1, Mesh = Asset!.Path, Transform = Matrix4.Identity(), Colour = new Vector3(1, 1, 1) }],
            Lights = Lights(),
            Sky = new Sky { Colour = new Vector3(0.055, 0.065, 0.086), Ambient = 14000, Drawn = false },
        };
    }

    // A key light, a fill from the other side and a rim behind, which is the arrangement a product
    // photograph uses and for the same reason: one light leaves half of a shape unreadable.
    private static RenderLight[] Lights() =>
    [
        new RenderLight { Key = 1, Kind = RenderLightKind.Directional, Direction = new Vector3(-0.4, -0.8, -0.45).Normalized(), Intensity = 80000 },
        new RenderLight
        {
            Key = 2,
            Kind = RenderLightKind.Directional,
            Direction = new Vector3(0.6, -0.35, 0.5).Normalized(),
            Colour = new Vector3(0.72, 0.79, 1),
            Intensity = 26000,
            CastShadows = false,
        },
        new RenderLight { Key = 3, Kind = RenderLightKind.Directional, Direction = new Vector3(0.15, -0.2, -0.9).Normalized(), Intensity = 18000, CastShadows = false },
    ];

    private (Vector3 Centre, double Radius) Framing() => _bounds is { } box
        ? ((box.Min + box.Max) * 0.5, Math.Max(0.05, (box.Max - box.Min).Length * 0.5))
        : (Vector3.Zero(), 1);

    private static (Vector3 Min, Vector3 Max)? BoundsOf(string path)
    {
        try
        {
            return IoPath.GetExtension(path).ToLowerInvariant() switch
            {
                ".glb" => MeshExport.BoundsOfGlb(IoFile.ReadAllBytes(path)),
                ".gltf" => MeshExport.BoundsOfGltf(IoFile.ReadAllText(path)),
                ".obj" => ObjFiles.BoundsOfObj(IoFile.ReadAllBytes(path)),
                _ => null,
            };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
