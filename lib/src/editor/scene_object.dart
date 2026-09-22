part of 'scene.dart';

/// One object in the edited scene.
///
/// Identified by an id rather than by name, because a rename is an ordinary
/// edit and everything that refers to an object — the selection, the undo
/// stack, a parent link — has to survive one.
class SceneObject {
  SceneObject({
    required this.id,
    required this.name,
    required this.kind,
    int? renderKey,
    this.parentId,
    Vector3? position,
    Vector3? rotation,
    Vector3? scale,
    this.colour = const Color(0xFFD9634F),
    this.power = 1000,
    this.lightType = LightType.sun,
    this.spotSize = 45,
    this.spotBlend = 0.15,
    this.sourceRadius = 0.1,
    this.sunAngle = 0.526,
    this.body = CelestialBody.sun,
    WeatherState? weather,
    this.condition = WeatherCondition.clear,
    this.cloudKind,
    this.windDirection = 135,
    this.transitionSeconds = 8,
    this.sway = 0,
    this.castShadows = true,
    this.receiveShadows = true,
    this.visible = true,
    this.meshAsset,
    this.materialAsset,
    this.shape,
    this.geometry,
    List<Surface>? surfaces,
    this.outline,
    Boundary? boundary,
    this.interfaceAsset,
    this.prefab,
    List<String>? data,
    Map<String, doc.SceneComponent>? components,
  }) : renderKey = renderKey ?? _nextRenderKey++,
       data = data ?? [],
       components = components ?? {},
       boundary = boundary ?? Boundary(),
       surfaces = surfaces ?? [],
       weather = weather ?? WeatherState.of(condition),
       position = position ?? Vector3.zero(),
       rotation = rotation ?? Vector3.zero(),
       scale = scale ?? Vector3(1, 1, 1);

  final String id;

  /// What the renderer knows this object by.
  ///
  /// A number rather than the id, because it crosses to native code on every
  /// frame of a drag and a string would be encoded, copied and hashed each
  /// time. Assigned once and never reused, so a pasted copy gets a key of its
  /// own instead of inheriting the entity of the thing it was copied from.
  ///
  /// It can be handed one, and exactly one thing does: rebuilding the scene
  /// from a document, where an object with the same id is the same object —
  /// giving it a new key there would make the renderer throw away everything
  /// it had built for it, every time anybody undid anything.
  final int renderKey;

  static int _nextRenderKey = 1;

  String name;

  final ObjectKind kind;

  /// The object this one hangs off, or null for a root.
  String? parentId;

  /// Local to the parent, not to the world. Moving a parent carries its
  /// children, which is the whole reason a hierarchy is worth having.
  final Vector3 position;

  /// Euler angles in degrees, XYZ order — the units the inspector shows, kept
  /// as the source of truth so a value typed in comes back out unchanged
  /// instead of round-tripping through a quaternion and drifting.
  final Vector3 rotation;

  final Vector3 scale;

  Color colour;

  /// Light power, in the units its [lightType] is stated in: watts per square
  /// metre for a sun, which has no total to state, and watts for everything
  /// else. Ignored by anything that is not a light.
  double power;

  /// What kind of light this is. Decides what [power] means, whether the cone
  /// applies, and what the renderer is asked for.
  LightType lightType;

  /// The full cone angle of a spot in degrees, and how much of it is falloff
  /// rather than full brightness — zero for a hard edge, one for a cone that
  /// is all gradient.
  double spotSize;
  double spotBlend;

  /// How large the emitting source is, in metres.
  ///
  /// Not a brightness control: the power is unchanged and spread over a bigger
  /// surface. What it changes is the shadow — a point source gives a knife
  /// edge, and anything with size gives a penumbra that widens with distance.
  double sourceRadius;

  /// What the air is doing, for the object that is the weather.
  ///
  /// The values rather than the name: a condition is where they came from and
  /// they are free to be moved afterwards. Held as one object because weather
  /// changes, and a change needs both ends of it in one place — eight fields
  /// on the object would be eight things to keep in step through a
  /// transition.
  WeatherState weather;

  /// The condition last applied, which is what the panel shows as chosen.
  WeatherCondition condition;

  /// Which shape of cloud the sky has, or null to take the condition's own.
  ///
  /// Separate from the condition because the same weather makes very
  /// different skies: a fair afternoon can be cauliflower cumulus with blue
  /// between them or one flat sheet, and a scene should be able to say which.
  /// Null rather than a default so that changing the condition still changes
  /// the sky for anybody who has not made a choice.
  CloudKind? cloudKind;

  /// Which way the wind blows, in degrees. Not part of a condition: a storm
  /// is windy wherever it is, and which way is a fact about the place.
  double windDirection;

  /// How long a change of condition takes to arrive, in seconds.
  double transitionSeconds;

  /// How much this object answers the wind, where nought is rigid.
  ///
  /// On the object rather than on the weather, because it is a fact about the
  /// thing rather than about the air: the same gust moves a canopy and leaves
  /// a wall alone. One is foliage, a quarter is a heavy branch, nought is
  /// everything that does not move — which is almost everything, and is why
  /// it is the default.
  double sway;

  /// The weather this object is on its way from, and when it set off.
  ///
  /// Not saved and not part of the document: a scene reopened tomorrow is in
  /// the weather it was saved in, not halfway into it.
  WeatherState? blendFrom;
  double blendSince = 0;

  /// Which body a directional light is, when nothing else is deciding.
  ///
  /// A day cycle decides for itself — whatever is above the horizon — and this
  /// is what the light is between cycles, or in a scene that has none.
  CelestialBody body;

  /// The sun's angular diameter in degrees, which is the same idea for a light
  /// that has no position. Defaults to the real sun's.
  ///
  /// It is why a shadow outdoors is crisp at your feet and soft at its far
  /// end, and setting it to zero is the quickest way to make a scene look
  /// computer-generated.
  double sunAngle;

  bool castShadows;

  /// Whether shadows land on this object.
  bool receiveShadows;

  /// Whether it is drawn, and whether it lights anything.
  ///
  /// Hidden is not deleted: it keeps its place in the tree, its children, and
  /// the key the renderer knows it by, so showing it again is immediate.
  bool visible;

  /// The mesh this object draws, as a path relative to the project.
  ///
  /// Null means the built-in cube. A referenced mesh is *still* drawn as a
  /// cube for now — the reference is recorded and shown, and the renderer
  /// honours it once glTF loading exists. Naming it here rather than pretending
  /// to load it keeps the file honest about what the scene says.
  String? meshAsset;

  /// A texture this object is drawn with, as a project-relative path, or null
  /// to keep whatever its mesh brought.
  ///
  /// The case this exists for: asset packs that ship a model and its colour
  /// map as two files. The glTF names no texture, so the model loads grey,
  /// and until now the only fix was a round trip through a modelling package
  /// to bind the two together and export them again. The renderer can do
  /// that binding itself — a material named on an object overrides the
  /// materials its file brought, on every primitive — so this records which
  /// texture, and the scene builds the material from it.
  String? materialAsset;

  /// What this object is, while it is still a shape.
  ///
  /// A box is a width, a height and a depth until somebody pulls a face off
  /// it. Changing the width of a box should change its width, not move eight
  /// corners — and that stays true right up to the moment they edit it, at
  /// which point the shape is what it *was* and [geometry] is what it is.
  Shape? shape;

  /// The geometry, once it stopped being a shape.
  ///
  /// Null while the object is still parametric, which is most of the time and
  /// is much the smaller thing to save. Set the moment anybody extrudes a
  /// face, and from then on the shape's numbers are history rather than truth.
  Mesh? geometry;

  /// Whether editing this would throw the shape's parameters away.
  bool get isParametric => shape != null && geometry == null;

  /// The geometry as it stands, whichever of the two it came from.
  /// An outline somebody drew and pulled up, kept so it can be redrawn.
  ///
  /// Beside [shape] rather than one of its kinds, because a shape is a set of
  /// numbers and this is a set of points — and beside [geometry] rather than
  /// replaced by it, so a wall can still be moved by dragging the corner it
  /// belongs to a week later. Editing the mesh directly fills in [geometry],
  /// and from then on that wins: an outline cannot describe a face that has
  /// been extruded.
  PolyShape? outline;

  /// The geometry as it stands: edited if it has been, otherwise built from
  /// whatever describes it.
  ///
  /// Cached, because building it is not free and this is asked several times
  /// a frame — the selection outline wants it, the click test wants it, and
  /// the writer that hands it to the renderer wants it. Rebuilding a
  /// twenty-step staircase sixty times a second to draw a line round it is
  /// the kind of waste that only shows up as "the editor feels slow".
  ///
  /// The cache is keyed on the two things that can produce one, by identity.
  /// A shape or an outline is replaced rather than mutated when it changes —
  /// every edit goes through a command that hands over a new one — so
  /// identity is exactly the right test and costs a pointer compare.
  Mesh? get currentMesh {
    final made = geometry;
    if (made != null) return made;

    if (identical(_builtFrom, outline ?? shape) && _built != null) {
      return _built;
    }
    _builtFrom = outline ?? shape;
    _built = outline?.build() ?? shape?.build();
    return _built;
  }

  Mesh? _built;
  Object? _builtFrom;

  /// Where this object begins and ends, as far as anything but the eye is
  /// concerned.
  ///
  /// A mesh by default, because that is right for anything however odd — a
  /// doorway is a hole you can walk through rather than a wall you cannot —
  /// and because a box is only ever right by luck for a shape somebody drew.
  Boundary boundary;

  /// The boundary as geometry, cached the same way and for the same reason.
  ///
  /// Two things can change it: the shape underneath, and the boundary's own
  /// settings. Both are compared by identity, and both are replaced rather
  /// than edited in place.
  Mesh? get boundaryMesh {
    final shape = currentMesh;
    if (identical(_shellFrom, shape) && identical(_shellFor, boundary)) {
      return _shell;
    }
    _shellFrom = shape;
    _shellFor = boundary;
    _shell = boundary.meshFrom(shape);
    return _shell;
  }

  Mesh? _shell;
  Mesh? _shellFrom;
  Boundary? _shellFor;

  /// The boundary's edges, for drawing it.
  ///
  /// Cached beside the mesh because `allEdges` builds a fresh set every time
  /// it is asked, and the thing asking is a painter running every frame.
  List<MeshEdge> get boundaryEdges {
    final shell = boundaryMesh;
    if (!identical(_edgesFrom, shell)) {
      _edgesFrom = shell;
      _edges = shell == null ? const [] : shell.allEdges.toList();
    }
    return _edges;
  }

  List<MeshEdge> _edges = const [];
  Mesh? _edgesFrom;

  /// The box this object actually occupies, in its own space.
  ///
  /// What a click is tested against and what the selection outline is drawn
  /// round. It used to be a two-metre cube for everything, which was right
  /// when everything *was* the placeholder cube — a shape half a metre across
  /// was picked and outlined four times its own size, and a model imported at
  /// any other scale was worse.
  ///
  /// [reported] is what a file said about itself, for the objects whose
  /// geometry the editor does not hold.
  ({Vector3 min, Vector3 max}) localBounds({
    ({Vector3 min, Vector3 max})? reported,
  }) {
    final mesh = currentMesh;
    if (mesh != null && !mesh.isEmpty) return mesh.bounds;
    if (reported != null) return reported;
    // The placeholder the renderer draws when it has nothing else, which is
    // genuinely two metres across.
    return (min: Vector3.all(-1), max: Vector3.all(1));
  }

  /// The materials this shape's faces can be painted with.
  ///
  /// Ordered, because `Face.material` is a position in this list. Removing
  /// one would repoint every face after it, so nothing removes from the
  /// middle — a slot is emptied by being painted over, not by going.
  final List<Surface> surfaces;

  /// The interface this canvas shows, as a path relative to the project.
  ///
  /// A reference, like a mesh and like a data object. The `.oui` is the
  /// document and this says which one is on screen — so one interface can be
  /// on two scenes, and changing it changes both.
  String? interfaceAsset;

  /// The prefab this came from, as a path relative to the project.
  ///
  /// Null for an ordinary object. Set on every object in an instance, root
  /// and children alike, because a change three levels down still has to know
  /// which asset it belongs to. Unpacking clears it, and from then on this is
  /// an ordinary object that happens to look like a prefab.
  String? prefab;

  /// Whether this object came from a prefab and still remembers it.
  bool get isPrefabInstance => prefab != null;

  /// Data objects this one is configured by, as paths relative to the project.
  ///
  /// A reference rather than a copy, which is the whole point: forty crates
  /// pointing at one `weight.odata` change together, and the value is edited
  /// where it lives instead of being typed onto forty objects and missed on
  /// thirty-nine. What is *in* the data object is not this object's business —
  /// the file is the source of truth, and the editor, a script and a person
  /// with a text editor all read the same one.
  final List<String> data;

  /// The components this row has no fields of its own for, as the document
  /// had them.
  ///
  /// A body, a set of splats, a tilemap, and anything written by a newer
  /// Orblit or by a project's own tool. Kept whole and written back whole, so
  /// opening a scene and saving it does not delete the parts of it this
  /// editor has no panel for — the promise [doc.UnknownComponent] makes for
  /// the format, kept by the editor too.
  ///
  /// A component in here is never changed in place: an edit replaces it with
  /// a new one, which is what lets undo keep the old one without copying it.
  final Map<String, doc.SceneComponent> components;

  IconData get icon => switch (kind) {
    ObjectKind.scene => Icons.public,
    ObjectKind.group => Icons.folder_outlined,
    ObjectKind.mesh => Icons.view_in_ar_outlined,
    ObjectKind.light => Icons.wb_sunny_outlined,
    ObjectKind.camera => Icons.videocam_outlined,
    ObjectKind.weather => Icons.cloud_outlined,
    ObjectKind.canvas => Icons.web_asset,
    ObjectKind.shape => Icons.category_outlined,
  };

  /// Whether this object is drawn.
  bool get isDrawable => kind == ObjectKind.mesh || kind == ObjectKind.shape;

  /// Where the object sits relative to its parent.
  Matrix4 get localTransform => Matrix4.identity()
    ..setTranslation(position)
    ..multiply(rotationFromDegrees(rotation))
    ..multiply(Matrix4.diagonal3(scale));

  /// A copy with a new identity, for pasting.
  SceneObject copyAs({required String id, String? parentId}) =>
      _copyWith(id: id, parentId: parentId);

  SceneObject copy() => _copyWith(id: id, parentId: parentId);

  /// One place both copies are made, so a field added to an object cannot be
  /// remembered by paste and forgotten by the clipboard.
  SceneObject _copyWith({required String id, String? parentId}) => SceneObject(
    id: id,
    name: name,
    kind: kind,
    parentId: parentId,
    position: position.clone(),
    rotation: rotation.clone(),
    scale: scale.clone(),
    colour: colour,
    power: power,
    lightType: lightType,
    spotSize: spotSize,
    spotBlend: spotBlend,
    sourceRadius: sourceRadius,
    sunAngle: sunAngle,
    body: body,
    weather: weather,
    condition: condition,
    cloudKind: cloudKind,
    windDirection: windDirection,
    transitionSeconds: transitionSeconds,
    sway: sway,
    castShadows: castShadows,
    receiveShadows: receiveShadows,
    visible: visible,
    meshAsset: meshAsset,
    materialAsset: materialAsset,
    shape: shape,
    geometry: geometry?.copy(),
    outline: outline?.copy(),
    boundary: boundary.copyWith(offset: boundary.offset.clone()),
    surfaces: [...surfaces],
    interfaceAsset: interfaceAsset,
    prefab: prefab,
    data: List<String>.from(data),
    components: Map.of(components),
  );
}

/// A rotation from XYZ degrees, applied X then Y then Z.
///
/// Built from three explicit axis rotations rather than from a library's Euler
/// constructor. vector_math's takes its three angles in an order that does not
/// match its argument names, so a rotation built with it and read back with
/// the obvious inverse comes out with the axes permuted — silently, and only
/// visible once something is animated. Owning both directions makes the
/// convention checkable, and [eulerDegreesOf] is its exact inverse.
Matrix4 rotationFromDegrees(Vector3 degreesXyz) =>
    Matrix4.rotationZ(radians(degreesXyz.z))
      ..multiply(Matrix4.rotationY(radians(degreesXyz.y)))
      ..multiply(Matrix4.rotationX(radians(degreesXyz.x)));

/// A transform's rotation, back into the XYZ degrees the inspector shows.
///
/// Scale is divided out first. Reading the angles straight off the upper 3x3
/// works only while the scale is one, and gives quietly wrong angles the
/// moment somebody resizes the object.
Vector3 eulerDegreesOf(Matrix4 transform) {
  final m = transform.getRotation();

  // A column's length is that axis's scale, because the rotation part is
  // orthonormal before scaling.
  double column(int index) =>
      Vector3(m.entry(0, index), m.entry(1, index), m.entry(2, index)).length;

  final scales = [column(0), column(1), column(2)];
  double r(int row, int col) {
    final scale = scales[col];
    // A zero-scaled axis carries no direction; treating it as unscaled keeps
    // the matrix well-formed rather than filling it with infinities.
    return scale < 1e-12 ? (row == col ? 1.0 : 0.0) : m.entry(row, col) / scale;
  }

  // The inverse of Rz * Ry * Rx. Clamped before asin: a value a hair outside
  // [-1, 1] from rounding returns NaN, and a NaN in a transform makes the
  // object vanish with nothing to say why.
  final sinPitch = (-r(2, 0)).clamp(-1.0, 1.0);
  final y = math.asin(sinPitch);

  final double x, z;
  if (sinPitch.abs() < 0.9999) {
    x = math.atan2(r(2, 1), r(2, 2));
    z = math.atan2(r(1, 0), r(0, 0));
  } else {
    // Gimbal lock: the object points straight up or down, so two of the three
    // angles turn about the same axis and only their sum survives. It all goes
    // into one of them.
    x = math.atan2(-r(0, 1), r(1, 1));
    z = 0;
  }

  return Vector3(degrees(x), degrees(y), degrees(z));
}

/// Sets an object's local transform so it lands on a given world matrix.
///
/// What keeps a thing where it looks when its parent changes — on a reparent,
/// and on a paste into a scene whose parent chain is different. Without it,
/// dropping something into a folder teleports it.
void placeInWorld(EditorScene scene, SceneObject object, Matrix4 world) {
  final parentId = object.parentId;
  final local = parentId == null || !scene.contains(parentId)
      ? world
      : Matrix4.inverted(scene.worldOf(parentId)).multiplied(world);

  final position = Vector3.zero();
  final rotation = Quaternion.identity();
  final scale = Vector3.zero();
  local.decompose(position, rotation, scale);

  object.position.setFrom(position);
  object.rotation.setFrom(eulerDegreesOf(local));
  object.scale.setFrom(scale);
  scene.invalidate();
}
