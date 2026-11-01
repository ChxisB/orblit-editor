namespace Orblit.Editor.Core;

using System.Diagnostics.CodeAnalysis;
using Orblit.Light;
using Orblit.Mesh;
using Orblit.Scene;
using Orblit.VectorMath;
using Orblit.Weather;
using MeshEdge = (int, int);

/// <summary>
/// One object in the edited scene.
/// </summary>
/// <remarks>
/// Identified by an id rather than by name, because a rename is an ordinary edit and everything
/// that refers to an object, such as the selection, the undo stack and a parent link, has to
/// survive one.
/// </remarks>
public sealed class SceneObject
{
    private static int lastRenderKey;

    private Mesh? _built;
    private object? _builtFrom;
    private Mesh? _shell;
    private Mesh? _shellFrom;
    private Boundary? _shellFor;
    private IReadOnlyList<MeshEdge> _edges = [];
    private Mesh? _edgesFrom;

    // F2: the fields of an object are data, and every one has a default.
    [SuppressMessage("Maintainability", "CA1502", Justification = "F2: a list of fields is data.")]
    public SceneObject(
        string id,
        string name,
        ObjectKind kind,
        int? renderKey = null,
        string? parentId = null,
        Vector3? position = null,
        Vector3? rotation = null,
        Vector3? scale = null,
        Colour? colour = null,
        double power = 1000,
        LightType lightType = LightType.Sun,
        double spotSize = 45,
        double spotBlend = 0.15,
        double sourceRadius = 0.1,
        double sunAngle = 0.526,
        CelestialBody body = CelestialBody.Sun,
        WeatherState? weather = null,
        WeatherCondition condition = WeatherCondition.Clear,
        CloudKind? cloudKind = null,
        double windDirection = 135,
        double transitionSeconds = 8,
        double sway = 0,
        bool castShadows = true,
        bool receiveShadows = true,
        bool visible = true,
        string? meshAsset = null,
        string? materialAsset = null,
        OrderedDictionary<string, object>? materialValues = null,
        Shape? shape = null,
        Mesh? geometry = null,
        List<Surface>? surfaces = null,
        PolyShape? outline = null,
        Boundary? boundary = null,
        string? interfaceAsset = null,
        PrefabComponent? prefab = null,
        List<string>? data = null,
        OrderedDictionary<string, SceneComponent>? components = null)
    {
        Id = id;
        Name = name;
        Kind = kind;
        RenderKey = renderKey ?? Interlocked.Increment(ref lastRenderKey);
        ParentId = parentId;
        Position = position ?? Vector3.Zero();
        Rotation = rotation ?? Vector3.Zero();
        Scale = scale ?? new Vector3(1, 1, 1);
        Colour = colour ?? new Colour(0xFFD9634F);
        Power = power;
        LightType = lightType;
        SpotSize = spotSize;
        SpotBlend = spotBlend;
        SourceRadius = sourceRadius;
        SunAngle = sunAngle;
        Body = body;
        Weather = weather ?? WeatherState.Of(condition);
        Condition = condition;
        CloudKind = cloudKind;
        WindDirection = windDirection;
        TransitionSeconds = transitionSeconds;
        Sway = sway;
        CastShadows = castShadows;
        ReceiveShadows = receiveShadows;
        Visible = visible;
        MeshAsset = meshAsset;
        MaterialAsset = materialAsset;
        MaterialValues = materialValues ?? [];
        Shape = shape;
        Geometry = geometry;
        Surfaces = surfaces ?? [];
        Outline = outline;
        Boundary = boundary ?? new Boundary();
        InterfaceAsset = interfaceAsset;
        Prefab = prefab;
        Data = data ?? [];
        Components = components ?? [];
    }

    public string Id { get; }

    /// <summary>
    /// What the renderer knows this object by. A number rather than the id, because it crosses to
    /// native code on every frame of a drag. Assigned once and never reused, so a pasted copy
    /// gets a key of its own instead of inheriting the entity of the thing it was copied from.
    /// </summary>
    /// <remarks>
    /// It can be handed one, and exactly one thing does: rebuilding the scene from a document,
    /// where an object with the same id is the same object. A new key there would make the
    /// renderer throw away everything it had built for it.
    /// </remarks>
    public int RenderKey { get; }

    public string Name { get; set; }

    public ObjectKind Kind { get; }

    /// <summary>The object this one hangs off, or null for a root.</summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// Local to the parent, not to the world. Moving a parent carries its children, which is the
    /// whole reason a hierarchy is worth having.
    /// </summary>
    public Vector3 Position { get; }

    /// <summary>
    /// Euler angles in degrees, XYZ order, which are the units the inspector shows. They are the
    /// source of truth so a value typed in comes back out unchanged instead of round-tripping
    /// through a quaternion and drifting.
    /// </summary>
    public Vector3 Rotation { get; }

    public Vector3 Scale { get; }

    public Colour Colour { get; set; }

    /// <summary>
    /// Light power, in the units its <see cref="LightType"/> is stated in: watts per square metre
    /// for a sun, and watts for everything else. Ignored by anything that is not a light.
    /// </summary>
    public double Power { get; set; }

    /// <summary>
    /// What kind of light this is. Decides what <see cref="Power"/> means, whether the cone
    /// applies, and what the renderer is asked for.
    /// </summary>
    public LightType LightType { get; set; }

    /// <summary>
    /// The full cone angle of a spot in degrees. <see cref="SpotBlend"/> is how much of it is
    /// falloff rather than full brightness, from zero for a hard edge to one for all gradient.
    /// </summary>
    public double SpotSize { get; set; }

    public double SpotBlend { get; set; }

    /// <summary>
    /// How large the emitting source is, in metres. Not a brightness control: the power is spread
    /// over a bigger surface, and what changes is the shadow, which gets a penumbra.
    /// </summary>
    public double SourceRadius { get; set; }

    /// <summary>
    /// What the air is doing, for the object that is the weather. The values rather than the
    /// name, because a condition is where they came from and they are free to be moved
    /// afterwards.
    /// </summary>
    public WeatherState Weather { get; set; }

    /// <summary>The condition last applied, which is what the panel shows as chosen.</summary>
    public WeatherCondition Condition { get; set; }

    /// <summary>
    /// Which shape of cloud the sky has, or null to take the condition's own. Null rather than a
    /// default so that changing the condition still changes the sky for anybody who has not made a
    /// choice.
    /// </summary>
    public CloudKind? CloudKind { get; set; }

    /// <summary>
    /// Which way the wind blows, in degrees. Not part of a condition: a storm is windy wherever
    /// it is, and which way is a fact about the place.
    /// </summary>
    public double WindDirection { get; set; }

    /// <summary>How long a change of condition takes to arrive, in seconds.</summary>
    public double TransitionSeconds { get; set; }

    /// <summary>
    /// How much this object answers the wind, where nought is rigid. On the object rather than on
    /// the weather, because it is a fact about the thing: the same gust moves a canopy and leaves
    /// a wall alone.
    /// </summary>
    public double Sway { get; set; }

    /// <summary>
    /// The weather this object is on its way from, and when it set off. Not saved and not part of
    /// the document: a scene reopened tomorrow is in the weather it was saved in.
    /// </summary>
    public WeatherState? BlendFrom { get; set; }

    public double BlendSince { get; set; }

    /// <summary>
    /// Which body a directional light is, when nothing else is deciding. A day cycle decides for
    /// itself, and this is what the light is between cycles or in a scene that has none.
    /// </summary>
    public CelestialBody Body { get; set; }

    /// <summary>
    /// The sun's angular diameter in degrees, which is the same idea as a source radius for a
    /// light that has no position. Defaults to the real sun's.
    /// </summary>
    public double SunAngle { get; set; }

    public bool CastShadows { get; set; }

    /// <summary>Whether shadows land on this object.</summary>
    public bool ReceiveShadows { get; set; }

    /// <summary>
    /// Whether it is drawn, and whether it lights anything. Hidden is not deleted: it keeps its
    /// place in the tree, its children, and the key the renderer knows it by.
    /// </summary>
    public bool Visible { get; set; }

    /// <summary>
    /// The mesh this object draws, as a path relative to the project. Null means the built-in
    /// cube.
    /// </summary>
    public string? MeshAsset { get; set; }

    /// <summary>
    /// A texture this object is drawn with, as a project-relative path, or null to keep whatever
    /// its mesh brought. A material named on an object overrides the materials its file brought,
    /// on every primitive.
    /// </summary>
    public string? MaterialAsset { get; set; }

    /// <summary>
    /// What this object states for the uniforms of <see cref="MaterialAsset"/> when that is an
    /// author-written shader, by name. Whatever is left out keeps the shader's own default.
    /// Replaced whole rather than edited in place, so an undo step can hold the map it came from.
    /// </summary>
    public OrderedDictionary<string, object> MaterialValues { get; set; }

    /// <summary>
    /// Whether <see cref="MaterialAsset"/> is a shader somebody wrote, which is cooked and drawn
    /// as a surface rather than loaded as a picture.
    /// </summary>
    public bool WearsSurface => MaterialAsset?.EndsWith(MaterialDocument.ShaderExtension, StringComparison.Ordinal) ?? false;

    /// <summary>
    /// What this object is, while it is still a shape. A box is a width, a height and a depth until
    /// somebody pulls a face off it, at which point the shape is what it was and
    /// <see cref="Geometry"/> is what it is.
    /// </summary>
    public Shape? Shape { get; set; }

    /// <summary>
    /// The geometry, once it stopped being a shape. Null while the object is still parametric,
    /// which is most of the time and is much the smaller thing to save.
    /// </summary>
    public Mesh? Geometry { get; set; }

    /// <summary>Whether editing this would throw the shape's parameters away.</summary>
    public bool IsParametric => Shape is not null && Geometry is null;

    /// <summary>
    /// An outline somebody drew and pulled up, kept so it can be redrawn. Beside
    /// <see cref="Shape"/> because a shape is a set of numbers and this is a set of points, and
    /// beside <see cref="Geometry"/> so a wall can still be moved by dragging a corner a week later.
    /// Editing the mesh directly fills in <see cref="Geometry"/>, and from then on that wins.
    /// </summary>
    public PolyShape? Outline { get; set; }

    /// <summary>
    /// The geometry as it stands: edited if it has been, otherwise built from whatever describes
    /// it.
    /// </summary>
    /// <remarks>
    /// Cached, because this is asked several times a frame by the selection outline, the click
    /// test and the writer that hands it to the renderer. The cache is keyed on the two things
    /// that can produce one, by identity. A shape or an outline is replaced rather than mutated
    /// when it changes, so identity is the right test and costs a pointer compare.
    /// </remarks>
    public Mesh? CurrentMesh
    {
        get
        {
            if (Geometry is { } made)
            {
                return made;
            }

            object? source = (object?)Outline ?? Shape;
            if (ReferenceEquals(_builtFrom, source) && _built is not null)
            {
                return _built;
            }

            _builtFrom = source;
            _built = Outline?.Build() ?? Shape?.Build();
            return _built;
        }
    }

    /// <summary>
    /// Where this object begins and ends, as far as anything but the eye is concerned. A mesh by
    /// default, because that is right for anything however odd.
    /// </summary>
    public Boundary Boundary { get; set; }

    /// <summary>
    /// The boundary as geometry, cached the same way and for the same reason. Two things can
    /// change it, the shape underneath and the boundary's own settings, and both are compared by
    /// identity.
    /// </summary>
    public Mesh? BoundaryMesh
    {
        get
        {
            var shape = CurrentMesh;
            if (ReferenceEquals(_shellFrom, shape) && ReferenceEquals(_shellFor, Boundary))
            {
                return _shell;
            }

            _shellFrom = shape;
            _shellFor = Boundary;
            _shell = Boundary.MeshFrom(shape);
            return _shell;
        }
    }

    /// <summary>
    /// The boundary's edges, for drawing it. Cached beside the mesh because <c>AllEdges</c> builds
    /// a fresh set every time and the thing asking is a painter running every frame.
    /// </summary>
    public IReadOnlyList<MeshEdge> BoundaryEdges
    {
        get
        {
            var shell = BoundaryMesh;
            if (!ReferenceEquals(_edgesFrom, shell))
            {
                _edgesFrom = shell;
                _edges = shell is null ? [] : [.. shell.AllEdges];
            }

            return _edges;
        }
    }

    /// <summary>
    /// The materials this shape's faces can be painted with. Ordered, because a face's material is
    /// a position in this list. Nothing removes from the middle: a slot is emptied by being
    /// painted over, not by going.
    /// </summary>
    public List<Surface> Surfaces { get; }

    /// <summary>
    /// The interface this canvas shows, as a path relative to the project. A reference, so one
    /// interface can be on two scenes and changing it changes both.
    /// </summary>
    public string? InterfaceAsset { get; set; }

    /// <summary>
    /// The prefab this is an instance of, and what is different about it. Null for an ordinary
    /// object. Set on an instance's root only: the parts under it say which instance they belong
    /// to by their ids, which are paths into it.
    /// </summary>
    public PrefabComponent? Prefab { get; set; }

    /// <summary>Whether this object came from a prefab and still remembers it.</summary>
    public bool IsPrefabInstance => Prefab is not null;

    /// <summary>
    /// Data objects this one is configured by, as paths relative to the project. A reference
    /// rather than a copy, so forty crates pointing at one file change together.
    /// </summary>
    public List<string> Data { get; }

    /// <summary>
    /// The components this row has no fields of its own for, as the document had them. Kept whole
    /// and written back whole, so opening a scene and saving it does not delete the parts of it
    /// this editor has no panel for. A component in here is never changed in place: an edit
    /// replaces it with a new one, which is what lets undo keep the old one without copying it.
    /// </summary>
    public OrderedDictionary<string, SceneComponent> Components { get; }

    /// <summary>What the inspector calls this kind of object, under its name.</summary>
    [SuppressMessage("Maintainability", "CA1502", Justification = "F2: a list of cases is data.")]
    public string KindLabel => Kind switch
    {
        ObjectKind.Scene => "Scene",
        ObjectKind.Group => "Group",
        ObjectKind.Mesh => "Mesh object",
        ObjectKind.Light => "Light",
        ObjectKind.Camera => "Camera",
        ObjectKind.Weather => "Weather",
        ObjectKind.Canvas => "Interface canvas",
        ObjectKind.Shape => "Drawn shape",
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    /// <summary>Whether this object is drawn.</summary>
    public bool IsDrawable => Kind is ObjectKind.Mesh or ObjectKind.Shape;

    /// <summary>Where the object sits relative to its parent.</summary>
    public Matrix4 LocalTransform
    {
        get
        {
            var local = Matrix4.Identity();
            local.SetTranslation(Position);
            local.Multiply(SceneMath.RotationFromDegrees(Rotation));
            local.Multiply(Matrix4.Diagonal3Values(Scale.X, Scale.Y, Scale.Z));
            return local;
        }
    }

    /// <summary>A copy with a new identity, for pasting.</summary>
    public SceneObject CopyAs(string id, string? parentId = null) => CopyWith(id, parentId);

    public SceneObject Copy() => CopyWith(Id, ParentId);

    // One place both copies are made, so a field added to an object cannot be remembered by paste
    // and forgotten by the clipboard.
    private SceneObject CopyWith(string id, string? parentId) => new(
        id: id,
        name: Name,
        kind: Kind,
        parentId: parentId,
        position: Position.Clone(),
        rotation: Rotation.Clone(),
        scale: Scale.Clone(),
        colour: Colour,
        power: Power,
        lightType: LightType,
        spotSize: SpotSize,
        spotBlend: SpotBlend,
        sourceRadius: SourceRadius,
        sunAngle: SunAngle,
        body: Body,
        weather: Weather,
        condition: Condition,
        cloudKind: CloudKind,
        windDirection: WindDirection,
        transitionSeconds: TransitionSeconds,
        sway: Sway,
        castShadows: CastShadows,
        receiveShadows: ReceiveShadows,
        visible: Visible,
        meshAsset: MeshAsset,
        materialAsset: MaterialAsset,
        materialValues: new OrderedDictionary<string, object>(MaterialValues),
        shape: Shape,
        geometry: Geometry?.Copy(),
        outline: Outline?.Copy(),
        boundary: Boundary.CopyWith(offset: Boundary.Offset.Clone()),
        surfaces: [.. Surfaces],
        interfaceAsset: InterfaceAsset,
        prefab: Prefab,
        data: [.. Data],
        components: new OrderedDictionary<string, SceneComponent>(Components));

    /// <summary>
    /// The box this object actually occupies, in its own space. What a click is tested against
    /// and what the selection outline is drawn round. <paramref name="reported"/> is what a file
    /// said about itself, for the objects whose geometry the editor does not hold.
    /// </summary>
    public (Vector3 Min, Vector3 Max) LocalBounds((Vector3 Min, Vector3 Max)? reported = null)
    {
        var mesh = CurrentMesh;
        if (mesh is not null && !mesh.IsEmpty)
        {
            return mesh.Bounds;
        }

        // The placeholder the renderer draws when it has nothing else, which is genuinely two
        // metres across.
        return reported ?? (Vector3.All(-1), Vector3.All(1));
    }
}
