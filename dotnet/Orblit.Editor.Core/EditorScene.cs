namespace Orblit.Editor.Core;

using Orblit.Light;
using Orblit.Mesh;
using Orblit.VectorMath;
using Orblit.Weather;

/// <summary>
/// The scene being edited.
/// </summary>
/// <remarks>
/// A flat list plus a parent link rather than nested children: reparenting is then one field,
/// ordering survives it, and there is no second copy of the tree to keep in step with the first.
/// </remarks>
public sealed class EditorScene
{
    private const int MaxDepth = 256;

    private readonly List<SceneObject> _objects;
    private readonly Dictionary<string, SceneObject> _byId = [];

    // Bumped by every structural change, so derived work can tell whether the answer it cached is
    // still the answer.
    private int _generation;
    private int _cachedGeneration = -1;
    private readonly Dictionary<string, Matrix4> _worldCache = [];

    // Who holds whom, built on the first question after a change. A walk down the tree asks for
    // the children of every object it meets, and scanning the whole list for each one made a walk
    // of a large scene take seconds.
    private int _indexedGeneration = -1;
    private List<SceneObject> _topLevel = [];
    private Dictionary<string, List<SceneObject>> _under = [];

    // F2: the settings of a scene are data, and every one has a default.
    public EditorScene(
        List<SceneObject> objects,
        string name = "Scene",
        Colour? skyColour = null,
        double ambient = 28000,
        double timeOfDay = 10,
        bool dayCycle = false,
        double hoursPerSecond = 0.5,
        IReadOnlyList<string>? layerNames = null)
    {
        _objects = objects;
        Name = name;
        SkyColour = skyColour ?? new Colour(0xFF1A2029);
        Ambient = ambient;
        TimeOfDay = timeOfDay;
        DayCycle = dayCycle;
        HoursPerSecond = hoursPerSecond;
        LayerNames = layerNames ?? [];

        foreach (var item in objects)
        {
            if (_byId.ContainsKey(item.Id))
            {
                throw new SceneException($"Two objects share the id \"{item.Id}\".");
            }

            _byId[item.Id] = item;
        }
    }

    /// <summary>What the scene is called, which need not match its file name.</summary>
    public string Name { get; set; }

    /// <summary>
    /// The sky, and by the same setting the light it casts. One property rather than two, because
    /// a backdrop that lights nothing reads as a photograph behind the scene.
    /// </summary>
    public Colour SkyColour { get; set; }

    /// <summary>How much light the sky casts, in lux.</summary>
    public double Ambient { get; set; }

    /// <summary>
    /// The hour the scene is set at, from zero to twenty-four. What is authored, not what is
    /// showing: with a cycle running, this is where the day starts from and
    /// <see cref="CurrentTimeOfDay"/> is where it has got to.
    /// </summary>
    public double TimeOfDay { get; set; }

    /// <summary>
    /// Whether the day runs on its own. Off, the scene sits at its hour and the body above it is
    /// whichever one the light says it is. On, the hour advances and the sky decides.
    /// </summary>
    public bool DayCycle { get; set; }

    public double HoursPerSecond { get; set; }

    /// <summary>
    /// What each physics layer is called, from the first. Shorter than thirty-two when the last
    /// layers have no name. Replaced whole rather than edited in place, so a command holds the list
    /// it started from and undo puts back exactly that.
    /// </summary>
    public IReadOnlyList<string> LayerNames { get; set; }

    /// <summary>
    /// How the scene looks held each way up, and which of them the rows are showing. Set when the
    /// scene is read, so the constructor does not take a ninth setting.
    /// </summary>
    public EditedLayouts Layouts { get; init; } = new();

    /// <summary>
    /// Seconds since the editor started animating this scene. Not saved and not part of the
    /// document: it is the editor's clock.
    /// </summary>
    public double Clock { get; set; }

    public int Length => _objects.Count;

    public IReadOnlyList<SceneObject> Objects => _objects.ToArray();

    /// <summary>Objects with no parent, in order.</summary>
    public IReadOnlyList<SceneObject> Roots => SiblingsOf(null);

    public SceneObject? this[string id] => _byId.GetValueOrDefault(id);

    /// <summary>
    /// Whether anything here moves without somebody moving it. Drifting cloud is not in this list:
    /// the renderer moves that on its own clock.
    /// </summary>
    public bool IsAnimated => DayCycle || WeatherIsChanging || (WeatherNow?.Lightning ?? 0) > 0;

    /// <summary>
    /// The object that decides what the air is doing, if there is one. The first of them: a scene
    /// with two would be two answers to one question.
    /// </summary>
    public SceneObject? Weather => _objects.FirstOrDefault(one => one.Kind == ObjectKind.Weather);

    /// <summary>Whether more than one thing is claiming to be the weather.</summary>
    public bool HasSpareWeather => _objects.Count(one => one.Kind == ObjectKind.Weather) > 1;

    /// <summary>
    /// What the air is doing this instant, part of the way through whatever change it is in the
    /// middle of.
    /// </summary>
    public WeatherState? WeatherNow
    {
        get
        {
            var weather = Weather;
            if (weather is null || !IsShown(weather.Id))
            {
                return null;
            }

            var from = weather.BlendFrom;
            if (from is null || weather.TransitionSeconds <= 0)
            {
                return weather.Weather;
            }

            var t = (Clock - weather.BlendSince) / weather.TransitionSeconds;
            return t >= 1 ? weather.Weather : WeatherState.Lerp(from, weather.Weather, t);
        }
    }

    /// <summary>
    /// The hour the scene is showing, which is the authored one until a cycle starts carrying it
    /// forward.
    /// </summary>
    public double CurrentTimeOfDay => DayCycle ? Doubles.Mod(TimeOfDay + (Clock * HoursPerSecond), 24) : TimeOfDay;

    /// <summary>The sky at that hour.</summary>
    public SkyState SkyState => Orblit.Weather.DayCycle.At(CurrentTimeOfDay);

    /// <summary>
    /// The light everything is lit from above by, if there is one. The first directional light in
    /// the scene. A renderer draws one, so a second is a light that would be quietly ignored.
    /// </summary>
    public SceneObject? Celestial =>
        _objects.FirstOrDefault(one => one.Kind == ObjectKind.Light && one.LightType == LightType.Sun);

    /// <summary>Which body is up: the cycle's, or the one the light was told to be.</summary>
    public CelestialBody ActiveBody => DayCycle ? SkyState.Body : (Celestial?.Body ?? CelestialBody.Sun);

    private bool WeatherIsChanging
    {
        get
        {
            var weather = Weather;
            return weather?.BlendFrom is not null && Clock - weather.BlendSince < weather.TransitionSeconds;
        }
    }

    /// <summary>
    /// A default scene, so a new project opens on something rather than nothing. The ground is a
    /// flattened box rather than a plane because the renderer draws boxes.
    /// </summary>
    public static EditorScene Starter() => new(
    [
        // Watts per square metre, because that is what a sun's strength is stated in. A hundred
        // and ten of them is about seventy-five thousand lux, which is a bright but not blinding
        // afternoon.
        new SceneObject(
            id: "sun",
            name: "Sun",
            kind: ObjectKind.Light,
            rotation: new Vector3(-55, 35, 0),
            colour: new Colour(0xFFFFF3E0),
            power: 110),
        new SceneObject(
            id: "ground",
            name: "Ground",
            kind: ObjectKind.Mesh,
            position: new Vector3(0, -1.05, 0),
            scale: new Vector3(8, 0.05, 8),
            colour: new Colour(0xFF3B424C)),
        new SceneObject(id: "props", name: "Props", kind: ObjectKind.Group),
        new SceneObject(
            id: "cube",
            name: "Cube",
            kind: ObjectKind.Mesh,
            parentId: "props",
            rotation: new Vector3(0, 25, 0),
            colour: new Colour(0xFFD9634F)),
        new SceneObject(
            id: "crate",
            name: "Crate",
            kind: ObjectKind.Mesh,
            parentId: "props",
            position: new Vector3(2.2, -0.65, 0.6),
            scale: new Vector3(0.7, 0.7, 0.7),
            colour: new Colour(0xFFE5B84F)),
        new SceneObject(
            id: "camera",
            name: "Camera",
            kind: ObjectKind.Camera,
            position: new Vector3(6, 4, 8),
            rotation: new Vector3(-20, 35, 0)),

        // A fair day rather than a clear one, so the object in the tree is visibly doing
        // something the moment somebody selects it.
        new SceneObject(id: "weather", name: "Weather", kind: ObjectKind.Weather, condition: WeatherCondition.Fair),
    ]);

    /// <summary>
    /// sRGB to linear, because the shading maths is linear and a colour handed over unconverted is
    /// washed out in a way that reads as a lighting bug.
    /// </summary>
    public static Vector3 LinearFromColour(Colour colour) => colour.ToLinear();

    /// <summary>Where an object sits among its siblings, for reordering.</summary>
    public int IndexOf(string id)
    {
        var found = _byId.GetValueOrDefault(id);
        return found is null ? -1 : SiblingsOf(found.ParentId).ToList().FindIndex(one => one.Id == id);
    }

    /// <summary>The objects sharing a parent, in the order they are drawn in the tree.</summary>
    public IReadOnlyList<SceneObject> SiblingsOf(string? parentId)
    {
        if (_indexedGeneration != _generation)
        {
            Index();
        }

        return parentId is null ? _topLevel : _under.GetValueOrDefault(parentId) ?? [];
    }

    // A new list each time rather than an edit of the old one, so a caller still holding the
    // siblings it was given keeps the answer it asked for.
    private void Index()
    {
        _topLevel = [];
        _under = [];
        foreach (var one in _objects)
        {
            if (one.ParentId is not { } parentId)
            {
                _topLevel.Add(one);
            }
            else if (_under.TryGetValue(parentId, out var siblings))
            {
                siblings.Add(one);
            }
            else
            {
                _under[parentId] = [one];
            }
        }

        _indexedGeneration = _generation;
    }

    /// <summary>
    /// Moves an object to a new parent at a given position among its siblings. Order is the list's
    /// own order rather than a number on each object: an index stored per object has to be
    /// renumbered on every move, and the renumbering is what goes wrong.
    /// </summary>
    public void MoveTo(string id, int index, string? parentId = null)
    {
        var moving = _byId.GetValueOrDefault(id);
        if (moving is null)
        {
            return;
        }

        if (parentId is not null)
        {
            if (parentId == id || IsAncestorOf(id, parentId))
            {
                throw new SceneException($"Cannot put \"{moving.Name}\" inside itself or its own children.");
            }

            if (!_byId.ContainsKey(parentId))
            {
                return;
            }
        }

        _objects.Remove(moving);
        moving.ParentId = parentId;

        // The siblings below are read from what the list holds now, not from before the move.
        Invalidate();

        // Placed relative to its new siblings rather than at an absolute position in the flat
        // list, which is what makes the tree read in the right order.
        var siblings = SiblingsOf(parentId);
        var at = Math.Clamp(index, 0, siblings.Count);
        _objects.Insert(at >= siblings.Count ? AfterLastBranch(siblings) : _objects.IndexOf(siblings[at]), moving);

        Invalidate();
    }

    // After the last sibling and everything nested under it, so it does not land in the middle of
    // somebody else's children.
    private int AfterLastBranch(IReadOnlyList<SceneObject> siblings)
    {
        if (siblings.Count == 0)
        {
            return 0;
        }

        var last = siblings[^1];
        var below = DescendantsOf(last.Id);
        return _objects.IndexOf(below.Count == 0 ? last : below[^1]) + 1;
    }

    public bool Contains(string id) => _byId.ContainsKey(id);

    /// <summary>Call after mutating an object, so cached world matrices are recomputed.</summary>
    public void Invalidate() => _generation++;

    public IReadOnlyList<SceneObject> ChildrenOf(string id) => SiblingsOf(id);

    /// <summary>Every object under <paramref name="id"/>, deepest last.</summary>
    public IReadOnlyList<SceneObject> DescendantsOf(string id)
    {
        var found = new List<SceneObject>();
        Walk(id, found);
        return found;
    }

    private void Walk(string parent, List<SceneObject> found)
    {
        foreach (var child in ChildrenOf(parent))
        {
            found.Add(child);
            Walk(child.Id, found);
        }
    }

    /// <summary>
    /// What an object is called on screen. A celestial light that still has a body's name for a
    /// name follows the body, so a scene that runs into the night says Moon in the tree without
    /// anybody editing the document. Give it a name of your own and it keeps that instead.
    /// </summary>
    public string DisplayNameOf(SceneObject target)
    {
        if (!ReferenceEquals(target, Celestial))
        {
            return target.Name;
        }

        var named = Enum.GetValues<CelestialBody>().Any(body => body.Label() == target.Name);
        return named ? ActiveBody.Label() : target.Name;
    }

    /// <summary>How deep an object sits, for indenting.</summary>
    public int DepthOf(string id)
    {
        var depth = 0;
        var current = _byId.GetValueOrDefault(id)?.ParentId;

        // Bounded rather than trusted: a cycle here would hang the outliner while it drew, which
        // is the worst place to discover one.
        while (current is not null && depth < MaxDepth)
        {
            depth++;
            current = _byId.GetValueOrDefault(current)?.ParentId;
        }

        return depth;
    }

    /// <summary>
    /// Whether an object is shown, which means it and everything above it is. Hiding a group has
    /// to hide what is inside it, or hiding a folder reads as a broken toggle.
    /// </summary>
    public bool IsShown(string id)
    {
        var current = _byId.GetValueOrDefault(id);
        var steps = 0;
        while (current is not null && steps < MaxDepth)
        {
            if (!current.Visible)
            {
                return false;
            }

            if (current.ParentId is not { } parentId)
            {
                return true;
            }

            current = _byId.GetValueOrDefault(parentId);
            steps++;
        }

        return true;
    }

    /// <summary>
    /// Whether <paramref name="ancestor"/> is above <paramref name="id"/> in the tree. The check
    /// that stops somebody dragging a parent onto its own child, which would detach the whole
    /// branch from the scene.
    /// </summary>
    public bool IsAncestorOf(string ancestor, string id)
    {
        var current = _byId.GetValueOrDefault(id)?.ParentId;
        var steps = 0;
        while (current is not null && steps < MaxDepth)
        {
            if (current == ancestor)
            {
                return true;
            }

            current = _byId.GetValueOrDefault(current)?.ParentId;
            steps++;
        }

        return false;
    }

    /// <summary>
    /// Whether any object above <paramref name="id"/> is in <paramref name="among"/>. Asked of each
    /// of a large selection, it walks up the one chain and never compares the selection with
    /// itself.
    /// </summary>
    public bool HasAncestorAmong(string id, IReadOnlySet<string> among)
    {
        var current = _byId.GetValueOrDefault(id)?.ParentId;
        var steps = 0;
        while (current is not null && steps < MaxDepth)
        {
            if (among.Contains(current))
            {
                return true;
            }

            current = _byId.GetValueOrDefault(current)?.ParentId;
            steps++;
        }

        return false;
    }

    /// <summary>Where an object ends up, with every parent applied.</summary>
    public Matrix4 WorldOf(string id)
    {
        if (_cachedGeneration != _generation)
        {
            _worldCache.Clear();
            _cachedGeneration = _generation;
        }

        if (_worldCache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var found = _byId.GetValueOrDefault(id);
        if (found is null)
        {
            return Matrix4.Identity();
        }

        var world = found.ParentId is not { } parentId || !_byId.ContainsKey(parentId)
            ? found.LocalTransform
            : WorldOf(parentId).Multiplied(found.LocalTransform);

        return _worldCache[id] = world;
    }

    // ---- structural edits, called by commands rather than by widgets ----

    public void Add(SceneObject added, int? at = null)
    {
        if (_byId.ContainsKey(added.Id))
        {
            throw new SceneException($"There is already an object with id \"{added.Id}\".");
        }

        _byId[added.Id] = added;
        _objects.Insert(at ?? _objects.Count, added);
        Invalidate();
    }

    /// <summary>
    /// Removes an object and everything under it, and says where it was. Returns the removed
    /// objects with their positions so an undo can put them back exactly, rather than appending
    /// them to the end where they would silently reorder the outliner.
    /// </summary>
    public List<(SceneObject Object, int Index)> Remove(string id) => Remove([id]);

    /// <summary>
    /// Removes each of <paramref name="ids"/> and everything under them in one pass over the list.
    /// An id that is gone, or sits inside another one in the list, is not an error. The answer is
    /// in list order, which is the order <see cref="Restore"/> has to replay it in.
    /// </summary>
    public List<(SceneObject Object, int Index)> Remove(IReadOnlyList<string> ids)
    {
        var gone = new HashSet<SceneObject>();
        foreach (var found in ids.Select(id => _byId.GetValueOrDefault(id)).OfType<SceneObject>())
        {
            gone.Add(found);
            gone.UnionWith(DescendantsOf(found.Id));
        }

        var removed = new List<(SceneObject Object, int Index)>(gone.Count);
        if (gone.Count == 0)
        {
            return removed;
        }

        var kept = 0;
        for (var index = 0; index < _objects.Count; index++)
        {
            var one = _objects[index];
            if (gone.Contains(one))
            {
                removed.Add((one, index));
                _byId.Remove(one.Id);
            }
            else
            {
                _objects[kept++] = one;
            }
        }

        _objects.RemoveRange(kept, _objects.Count - kept);
        Invalidate();
        return removed;
    }

    /// <summary>
    /// Swaps every object in this scene for <paramref name="replacements"/>, keeping the scene
    /// itself. For the one caller that has a whole new set of them: a change stated as a difference
    /// between two documents. Everything else edits the objects it already has, and should.
    /// </summary>
    public void ReplaceAll(IReadOnlyList<SceneObject> replacements)
    {
        _objects.Clear();
        _objects.AddRange(replacements);
        _byId.Clear();
        foreach (var item in replacements)
        {
            _byId[item.Id] = item;
        }

        Invalidate();
    }

    /// <summary>
    /// Puts <paramref name="replacement"/> where the object with its id is, in the same place. For
    /// a change worked out on a document and read back as a new row, one object at a time.
    /// </summary>
    public void Replace(SceneObject replacement)
    {
        var was = _byId.GetValueOrDefault(replacement.Id)
            ?? throw new SceneException($"There is no object with id \"{replacement.Id}\".");
        _objects[_objects.IndexOf(was)] = replacement;
        _byId[replacement.Id] = replacement;
        Invalidate();
    }

    /// <summary>
    /// Puts back what <see cref="Remove(IReadOnlyList{string})"/> took out, in the order it gave it:
    /// by position, lowest first, so each object lands at the index it came from. The list is
    /// merged in one pass. Inserting one object at a time moved everything after it each time.
    /// </summary>
    public void Restore(IReadOnlyList<(SceneObject Object, int Index)> entries)
    {
        var merged = new List<SceneObject>(_objects.Count + entries.Count);
        var taken = 0;
        foreach (var (restored, index) in entries)
        {
            _byId[restored.Id] = restored;
            var upTo = Math.Min(index, _objects.Count - taken + merged.Count);
            while (merged.Count < upTo)
            {
                merged.Add(_objects[taken++]);
            }

            merged.Add(restored);
        }

        merged.AddRange(_objects.Skip(taken));
        _objects.Clear();
        _objects.AddRange(merged);
        Invalidate();
    }

    public void Reparent(string id, string? parentId)
    {
        var found = _byId.GetValueOrDefault(id);
        if (found is null)
        {
            return;
        }

        if (parentId is not null)
        {
            if (parentId == id || IsAncestorOf(id, parentId))
            {
                throw new SceneException($"Cannot put \"{found.Name}\" inside itself or its own children.");
            }

            if (!_byId.ContainsKey(parentId))
            {
                return;
            }
        }

        found.ParentId = parentId;
        Invalidate();
    }

    /// <summary>
    /// The nearest drawable object a ray runs into, or null for empty space. Tested in each
    /// object's own space, so an object that has been rotated and squashed is hit where it looks
    /// rather than inside the upright box that would contain it.
    /// </summary>
    public string? ObjectAlong(
        Vector3 origin,
        Vector3 direction,
        Func<SceneObject, (Vector3 Min, Vector3 Max)?>? boundsOf = null)
    {
        string? nearest = null;
        var closest = double.PositiveInfinity;

        foreach (var item in _objects)
        {
            if (!item.IsDrawable || !IsShown(item.Id))
            {
                continue;
            }

            if (HitDistance(item, origin, direction, closest, boundsOf) is { } distance)
            {
                closest = distance;
                nearest = item.Id;
            }
        }

        return nearest;
    }

    // How far along the ray the object is met, or null for a miss or for anything not closer than
    // what has been found already.
    private double? HitDistance(
        SceneObject target,
        Vector3 origin,
        Vector3 direction,
        double closest,
        Func<SceneObject, (Vector3 Min, Vector3 Max)?>? boundsOf)
    {
        // A zero scale on any axis leaves nothing to hit.
        if (SceneMath.TryInvert(WorldOf(target.Id)) is not { } inverse)
        {
            return null;
        }

        var from = inverse.Transformed3(origin.Clone());

        // As the difference of two transformed points, so the translation cancels and what is left
        // is the direction in the object's space, still measured in world units, which is what
        // makes the distances comparable between objects.
        var along = inverse.Transformed3(origin + direction) - from;

        // Nothing to hit, by choice: decoration somebody should walk straight through is
        // decoration they should not be able to click either.
        if (target.Boundary.IsNothing)
        {
            return null;
        }

        var box = target.Boundary.BoxFrom(target.LocalBounds(boundsOf?.Invoke(target)));
        var hit = BoxHit(from, along, box.Min, box.Max);
        if (hit is null || hit >= closest)
        {
            return null;
        }

        // The box got the ray into the neighbourhood and the boundary decides. Clicking the gap in
        // an L-shaped room should select what is behind it, not the room.
        var distance = BoundaryHit(target.BoundaryMesh, hit.Value, from, along);
        return distance is { } found && found < closest ? found : null;
    }

    private static double? BoundaryHit(Mesh? shell, double boxHit, Vector3 origin, Vector3 direction) =>
        shell is null || shell.IsEmpty ? boxHit : MeshHit(shell, origin, direction);

    /// <summary>How far along a ray a mesh is first met, or null for a miss.</summary>
    private static double? MeshHit(Mesh mesh, Vector3 origin, Vector3 direction)
    {
        var nearest = double.PositiveInfinity;

        foreach (var face in mesh.Faces)
        {
            var points = mesh.PointsOf(face);
            if (points.Count < 3)
            {
                continue;
            }

            // The same fan the renderer draws it with, so what is clicked is what is on screen.
            // Ear clipping would be exact for a concave face, and the difference is a click in the
            // dent of one, which is not worth walking every face twice for.
            for (var i = 1; i + 1 < points.Count; i++)
            {
                var hit = TriangleHit(origin, direction, points[0], points[i], points[i + 1]);
                if (hit is { } at && at < nearest)
                {
                    nearest = at;
                }
            }
        }

        return double.IsFinite(nearest) ? nearest : null;
    }

    /// <summary>
    /// Möller–Trumbore. Both sides count: clicking the far wall of a room from inside it should
    /// select the room.
    /// </summary>
    private static double? TriangleHit(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c)
    {
        var edge1 = b - a;
        var edge2 = c - a;
        var h = direction.Cross(edge2);
        var det = edge1.Dot(h);
        if (Math.Abs(det) < 1e-12)
        {
            return null;
        }

        var f = 1 / det;
        var s = origin - a;
        var u = f * s.Dot(h);
        if (u < 0 || u > 1)
        {
            return null;
        }

        var q = s.Cross(edge1);
        var v = f * direction.Dot(q);
        if (v < 0 || u + v > 1)
        {
            return null;
        }

        var t = f * edge2.Dot(q);
        return t > 1e-6 ? t : null;
    }

    /// <summary>
    /// How far along a ray a box is first met, or null for a miss. The slab method: the span of the
    /// ray inside each pair of parallel faces, intersected. If what is left is empty the ray goes
    /// past.
    /// </summary>
    private static double? BoxHit(Vector3 origin, Vector3 direction, Vector3 low, Vector3 high)
    {
        var near = double.NegativeInfinity;
        var far = double.PositiveInfinity;

        for (var axis = 0; axis < 3; axis++)
        {
            var o = origin[axis];
            var d = direction[axis];

            if (Math.Abs(d) < 1e-9)
            {
                // Parallel to this pair of faces: either between them for the whole ray, or never.
                if (o < low[axis] || o > high[axis])
                {
                    return null;
                }

                continue;
            }

            var first = (low[axis] - o) / d;
            var second = (high[axis] - o) / d;
            near = Math.Max(near, Math.Min(first, second));
            far = Math.Min(far, Math.Max(first, second));
            if (near > far)
            {
                return null;
            }
        }

        // Behind the eye is not in front of it.
        if (far < 0)
        {
            return null;
        }

        return near >= 0 ? near : far;
    }

    /// <summary>
    /// Roughly where an object sits and how big it is, with its children. Built from the unit cube
    /// the renderer draws for everything, so it is good enough to frame by, which is all it is for.
    /// </summary>
    public (Vector3 Centre, double Radius) BoundsOf(string id)
    {
        var drawn = new List<SceneObject>();
        if (this[id] is { } self)
        {
            drawn.Add(self);
        }

        drawn.AddRange(DescendantsOf(id));
        drawn.RemoveAll(one => !one.IsDrawable);

        // A group of nothing, or a light: frame its own position rather than refusing, so F always
        // does something.
        if (drawn.Count == 0)
        {
            return (this[id] is { } lone ? WorldOf(lone.Id).GetTranslation() : Vector3.Zero(), 1);
        }

        var minimum = Vector3.All(double.PositiveInfinity);
        var maximum = Vector3.All(double.NegativeInfinity);
        foreach (var item in drawn)
        {
            var world = WorldOf(item.Id);
            foreach (var corner in UnitCorners)
            {
                Extend(minimum, maximum, world.Transformed3(corner.Clone()));
            }
        }

        // Floored, so framing something flat, such as a ground plane, does not put the camera
        // inside it.
        return ((minimum + maximum) * 0.5, Math.Max((maximum - minimum).Length / 2, 0.5));
    }

    /// <summary>What the whole scene occupies, for framing with nothing selected.</summary>
    public (Vector3 Centre, double Radius) BoundsOfEverything()
    {
        var roots = Roots;
        if (roots.Count == 0)
        {
            return (Vector3.Zero(), 4);
        }

        var minimum = Vector3.All(double.PositiveInfinity);
        var maximum = Vector3.All(double.NegativeInfinity);
        foreach (var root in roots)
        {
            var bounds = BoundsOf(root.Id);
            Extend(minimum, maximum, bounds.Centre - Vector3.All(bounds.Radius));
            Extend(minimum, maximum, bounds.Centre + Vector3.All(bounds.Radius));
        }

        return ((minimum + maximum) * 0.5, Math.Max((maximum - minimum).Length / 2, 1));
    }

    private static readonly Vector3[] UnitCorners =
    [
        new(-1, -1, -1), new(-1, -1, 1), new(-1, 1, -1), new(-1, 1, 1),
        new(1, -1, -1), new(1, -1, 1), new(1, 1, -1), new(1, 1, 1),
    ];

    private static void Extend(Vector3 minimum, Vector3 maximum, Vector3 point)
    {
        minimum.SetValues(Math.Min(minimum.X, point.X), Math.Min(minimum.Y, point.Y), Math.Min(minimum.Z, point.Z));
        maximum.SetValues(Math.Max(maximum.X, point.X), Math.Max(maximum.Y, point.Y), Math.Max(maximum.Z, point.Z));
    }
}
