import 'package:flutter/material.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:path/path.dart' as p;

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'commands.dart';
import 'inspector.dart';
import 'terrain_bench.dart';
import 'terrain_edits.dart';

/// The inspector's section for a terrain: how it takes shadows, which
/// regions of ground it has, the sets it is painted with, and how bare ground
/// chooses between them.
///
/// The shaping itself is the terrain mode's. This is what a terrain is made
/// of, which is changed now and then rather than stroked all afternoon.
InspectorSection terrainSection({required TerrainBench bench}) =>
    InspectorSection(
      name: 'terrain',
      appliesTo: (target) => terrainComponentOf(target.object) != null,
      build: (target) => _TerrainSection(target: target, bench: bench),
    );

/// Pictures a set can use: the ones a picture can be read from here and
/// scaled to the size the renderer keeps them at.
const _pictureExtensions = {'.png', '.jpg', '.jpeg'};

class _TerrainSection extends StatelessWidget {
  const _TerrainSection({required this.target, required this.bench});

  final InspectorTarget target;
  final TerrainBench bench;

  @override
  Widget build(BuildContext context) {
    final component = terrainComponentOf(target.object);
    if (component == null) return const SizedBox.shrink();
    final open = bench.terrainFor(component.file);
    final editing = open == null
        ? null
        : _Editing(target: target, bench: bench, open: open);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        OrblitSection(
          title: 'Terrain',
          icon: Icons.landscape_outlined,
          child: _about(component, open),
        ),
        if (editing != null) ...[
          OrblitSection(
            title: 'Regions',
            icon: Icons.grid_view,
            child: editing.regions(),
          ),
          OrblitSection(
            title: 'Sets',
            icon: Icons.layers_outlined,
            child: editing.sets(),
          ),
          OrblitSection(
            title: 'Automatic cover',
            icon: Icons.auto_awesome_outlined,
            child: editing.automatic(),
          ),
        ],
      ],
    );
  }

  Widget _about(doc.TerrainComponent component, OpenTerrain? open) {
    final file = component.file;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TextRow(label: 'File', value: file ?? 'None'),
        if (open == null)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: Space.xs),
            child: Text(
              file == null
                  ? 'Names no terrain file, so there is no ground to show.'
                  : 'Could not be read. The console says why.',
              style: OrblitText.caption.copyWith(fontSize: 11),
            ),
          ),
        ChoiceRow(
          label: 'Cast shadows',
          options: const ['Off', 'On'],
          selected: component.castShadows ? 'On' : 'Off',
          onSelect: (value) => _put(
            component,
            doc.TerrainComponent(
              file: file,
              castShadows: value == 'On',
              receiveShadows: component.receiveShadows,
            ),
          ),
        ),
        ChoiceRow(
          label: 'Take shadows',
          options: const ['Off', 'On'],
          selected: component.receiveShadows ? 'On' : 'Off',
          onSelect: (value) => _put(
            component,
            doc.TerrainComponent(
              file: file,
              castShadows: component.castShadows,
              receiveShadows: value == 'On',
            ),
          ),
        ),
      ],
    );
  }

  void _put(doc.TerrainComponent from, doc.TerrainComponent to) {
    if (from.castShadows == to.castShadows &&
        from.receiveShadows == to.receiveShadows) {
      return;
    }
    target.history
      ..run(
        SetObjectComponent(
          sceneId: target.sceneId,
          id: target.object.id,
          label: 'Set ${target.object.name} shadows',
          type: doc.SceneComponents.terrain,
          from: from,
          to: to,
        ),
      )
      ..seal();
  }
}

/// What a terrain is made of, changed through the undo stack.
class _Editing {
  const _Editing({
    required this.target,
    required this.bench,
    required this.open,
  });

  final InspectorTarget target;
  final TerrainBench bench;
  final OpenTerrain open;

  Terrain get _terrain => open.terrain;

  List<TerrainSet> get _sets => _terrain.sets;

  Widget regions() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Text(
        'Each square is ${_metres(_terrain.regionSize * _terrain.spacing)} m '
        'of ground. Click an empty one to add ground there, a full one to '
        'take it away.',
        style: OrblitText.caption.copyWith(fontSize: 11),
      ),
      const SizedBox(height: Space.sm),
      _RegionGrid(terrain: _terrain, onToggle: _toggle),
    ],
  );

  void _toggle(RegionKey key) {
    final had = _terrain.regionAt(key);
    target.history
      ..run(
        TerrainRegionEdit(
          bench: bench,
          file: open.file,
          region: had ?? TerrainRegion(key, _terrain.regionSize),
          adding: had == null,
          label: had == null
              ? 'Add ground to ${open.name}'
              : 'Take ground from ${open.name}',
        ),
      )
      ..seal();
  }

  /// Changes the settings. A [gesture] is a slider being dragged, which the
  /// slider seals when it lets go; anything else is a step of its own.
  void _edit(
    String label,
    TerrainSettings Function(TerrainSettings settings) change, {
    Object? gesture,
  }) {
    bench.edit(open, label, change, gesture: gesture);
    if (gesture == null) target.history.seal();
  }

  void _editSet(
    int index,
    String what,
    TerrainSet Function(TerrainSet set) change, {
    Object? gesture,
  }) => _edit(
    'Set ${_sets[index].name} $what',
    (settings) => (
      sets: [...settings.sets]..[index] = change(settings.sets[index]),
      autoCover: settings.autoCover,
      blendSharpness: settings.blendSharpness,
    ),
    gesture: gesture,
  );

  Widget sets() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      for (final (index, set) in _sets.indexed) _set(index, set),
      Row(
        children: [
          Expanded(
            child: OrblitButton(
              label: 'Add set',
              icon: Icons.add,
              tone: ButtonTone.quiet,
              expand: true,
              onPressed: _sets.length >= Cover.setCount ? null : _addSet,
            ),
          ),
          const SizedBox(width: Space.xs),
          // Only the last: the ground names a set by its place in the list,
          // so taking one from the middle would repaint everything after it.
          Expanded(
            child: Tooltip(
              message:
                  'Takes the last set away. Ground painted with it shows '
                  'the one before.',
              child: OrblitButton(
                label: 'Remove last',
                icon: Icons.remove,
                tone: ButtonTone.quiet,
                expand: true,
                onPressed: _sets.length <= 1 ? null : _removeLastSet,
              ),
            ),
          ),
        ],
      ),
    ],
  );

  void _addSet() => _edit(
    'Add a set to ${open.name}',
    (settings) => (
      sets: [
        ...settings.sets,
        TerrainSet(name: 'Set ${settings.sets.length + 1}'),
      ],
      autoCover: settings.autoCover,
      blendSharpness: settings.blendSharpness,
    ),
  );

  void _removeLastSet() =>
      _edit('Remove ${_sets.last.name} from ${open.name}', (settings) {
        final last = settings.sets.length - 2;
        final auto = settings.autoCover;
        return (
          sets: settings.sets.sublist(0, last + 1),
          // Bare ground must not be left choosing a set that is not there.
          autoCover: AutoCover(
            steep: auto.steep.clamp(0, last),
            flat: auto.flat.clamp(0, last),
            slope: auto.slope,
            heightFalloff: auto.heightFalloff,
          ),
          blendSharpness: settings.blendSharpness,
        );
      });

  Widget _set(int index, TerrainSet set) => Container(
    margin: const EdgeInsets.only(bottom: Space.sm),
    padding: const EdgeInsets.all(Space.sm),
    decoration: BoxDecoration(
      color: OrblitColors.surface,
      borderRadius: BorderRadius.circular(Radii.control),
      border: Border.all(color: OrblitColors.lineSoft),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          '${index + 1}. ${set.name}',
          style: OrblitText.label.copyWith(color: OrblitColors.ink),
        ),
        const SizedBox(height: Space.xs),
        _PictureRow(
          label: 'Colour',
          path: set.albedo,
          relative: _relative,
          onChanged: (path) => _editSet(
            index,
            'colour picture',
            (set) => _copy(set, albedo: () => path),
          ),
        ),
        _PictureRow(
          label: 'Surface',
          path: set.normal,
          relative: _relative,
          onChanged: (path) => _editSet(
            index,
            'surface picture',
            (set) => _copy(set, normal: () => path),
          ),
        ),
        SliderRow(
          label: 'Repeats',
          value: set.tileSize,
          min: 0.5,
          max: 32,
          decimals: 1,
          unit: ' m',
          onChanged: (value) => _editSet(
            index,
            'repeat',
            (set) => _copy(set, tileSize: value),
            gesture: 'tileSize:$index',
          ),
          onSettled: target.history.seal,
        ),
        ChoiceRow(
          label: 'On cliffs',
          options: const ['Stretch', 'Wrap'],
          selected: set.triplanar ? 'Wrap' : 'Stretch',
          onSelect: (value) {
            if ((value == 'Wrap') == set.triplanar) return;
            _editSet(
              index,
              'cliffs',
              (set) => _copy(set, triplanar: value == 'Wrap'),
            );
          },
        ),
      ],
    ),
  );

  /// [set] with some of its fields changed. The pictures are asked for as
  /// functions, so taking one off can be told apart from leaving it be.
  static TerrainSet _copy(
    TerrainSet set, {
    String? Function()? albedo,
    String? Function()? normal,
    double? tileSize,
    bool? triplanar,
  }) => TerrainSet(
    name: set.name,
    albedo: albedo == null ? set.albedo : albedo(),
    normal: normal == null ? set.normal : normal(),
    tileSize: tileSize ?? set.tileSize,
    triplanar: triplanar ?? set.triplanar,
  );

  /// A dropped picture's path as the terrain file keeps it: from the
  /// project folder, with forward slashes. Null for one outside the project.
  String? _relative(String path) {
    final root = bench.projectRoot;
    if (!p.isWithin(root, path)) return null;
    return p.posix.joinAll(p.split(p.relative(path, from: root)));
  }

  Widget automatic() {
    final auto = _terrain.autoCover;
    final names = [for (final set in _sets) set.name];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'What ground nobody has painted shows: one set where it is flat, '
          'another where it is steep.',
          style: OrblitText.caption.copyWith(fontSize: 11),
        ),
        const SizedBox(height: Space.xs),
        if (names.isNotEmpty) ...[
          _SetChoice(
            label: 'Flat',
            names: names,
            selected: auto.flat,
            onSelect: (index) {
              if (index != auto.flat) _cover('flat set', flat: index);
            },
          ),
          _SetChoice(
            label: 'Steep',
            names: names,
            selected: auto.steep,
            onSelect: (index) {
              if (index != auto.steep) _cover('steep set', steep: index);
            },
          ),
        ],
        SliderRow(
          label: 'Slope',
          value: auto.slope,
          min: 0,
          max: 4,
          decimals: 2,
          onChanged: (value) => _cover('slope', slope: value, gesture: 'slope'),
          onSettled: target.history.seal,
        ),
        SliderRow(
          label: 'By height',
          value: auto.heightFalloff,
          min: 0,
          max: 1,
          decimals: 2,
          onChanged: (value) =>
              _cover('height', heightFalloff: value, gesture: 'height'),
          onSettled: target.history.seal,
        ),
        SliderRow(
          label: 'Sharpness',
          value: _terrain.blendSharpness,
          min: 0,
          max: 1,
          decimals: 2,
          onChanged: (value) => _edit(
            'Set ${open.name} blend',
            (settings) => (
              sets: settings.sets,
              autoCover: settings.autoCover,
              blendSharpness: value,
            ),
            gesture: 'blend',
          ),
          onSettled: target.history.seal,
        ),
      ],
    );
  }

  void _cover(
    String what, {
    int? steep,
    int? flat,
    double? slope,
    double? heightFalloff,
    Object? gesture,
  }) => _edit('Set ${open.name} $what', (settings) {
    final auto = settings.autoCover;
    return (
      sets: settings.sets,
      autoCover: AutoCover(
        steep: steep ?? auto.steep,
        flat: flat ?? auto.flat,
        slope: slope ?? auto.slope,
        heightFalloff: heightFalloff ?? auto.heightFalloff,
      ),
      blendSharpness: settings.blendSharpness,
    );
  }, gesture: gesture);
}

String _metres(double value) => value == value.roundToDouble()
    ? '${value.round()}'
    : value.toStringAsFixed(1);

/// Where a terrain has ground, as a map of squares with a margin of empty
/// ones round it to add to.
class _RegionGrid extends StatelessWidget {
  const _RegionGrid({required this.terrain, required this.onToggle});

  final Terrain terrain;
  final ValueChanged<RegionKey> onToggle;

  static const double _gap = 2;

  @override
  Widget build(BuildContext context) {
    final keys = [for (final region in terrain.regions) region.key];
    // Round the origin when there is no ground yet, so the first square
    // added is somewhere the camera already is.
    var (x0, x1, z0, z1) = (-1, 0, -1, 0);
    if (keys.isNotEmpty) {
      x0 = x1 = keys.first.x;
      z0 = z1 = keys.first.z;
      for (final key in keys) {
        if (key.x < x0) x0 = key.x;
        if (key.x > x1) x1 = key.x;
        if (key.z < z0) z0 = key.z;
        if (key.z > z1) z1 = key.z;
      }
      (x0, x1, z0, z1) = (x0 - 1, x1 + 1, z0 - 1, z1 + 1);
    }
    final columns = x1 - x0 + 1;
    return LayoutBuilder(
      builder: (context, constraints) {
        final fit = (constraints.maxWidth - _gap * (columns - 1)) / columns;
        final cell = fit.clamp(4.0, 22.0);
        return Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              for (var z = z0; z <= z1; z++)
                Padding(
                  padding: EdgeInsets.only(bottom: z == z1 ? 0 : _gap),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      for (var x = x0; x <= x1; x++)
                        Padding(
                          padding: EdgeInsets.only(right: x == x1 ? 0 : _gap),
                          child: _RegionCell(
                            size: cell,
                            present: terrain.regionAt(RegionKey(x, z)) != null,
                            origin: x == 0 && z == 0,
                            tooltip: '$x, $z',
                            onTap: () => onToggle(RegionKey(x, z)),
                          ),
                        ),
                    ],
                  ),
                ),
            ],
          ),
        );
      },
    );
  }
}

class _RegionCell extends StatelessWidget {
  const _RegionCell({
    required this.size,
    required this.present,
    required this.origin,
    required this.tooltip,
    required this.onTap,
  });

  final double size;
  final bool present;

  /// Whether the world's origin is at its corner, marked so the map can be
  /// told apart from its mirror image.
  final bool origin;

  final String tooltip;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => Tooltip(
    message: tooltip,
    waitDuration: const Duration(milliseconds: 400),
    child: MouseRegion(
      cursor: SystemMouseCursors.click,
      child: GestureDetector(
        onTap: onTap,
        child: Container(
          width: size,
          height: size,
          decoration: BoxDecoration(
            color: present ? OrblitColors.inkDim : OrblitColors.raised,
            borderRadius: BorderRadius.circular(2),
            border: Border.all(
              color: origin ? OrblitColors.ember : OrblitColors.lineSoft,
            ),
          ),
        ),
      ),
    ),
  );
}

/// One of a set's two pictures, dropped on from the project browser.
class _PictureRow extends StatelessWidget {
  const _PictureRow({
    required this.label,
    required this.path,
    required this.relative,
    required this.onChanged,
  });

  final String label;
  final String? path;
  final String? Function(String absolute) relative;
  final ValueChanged<String?> onChanged;

  bool _takes(String dropped) =>
      _pictureExtensions.contains(p.extension(dropped).toLowerCase()) &&
      relative(dropped) != null;

  @override
  Widget build(BuildContext context) {
    final path = this.path;
    return FieldRow(
      label: label,
      trailing: path == null
          ? null
          : IconButton(
              tooltip: 'Take the picture off',
              iconSize: 13,
              visualDensity: VisualDensity.compact,
              padding: EdgeInsets.zero,
              constraints: const BoxConstraints.tightFor(width: 22, height: 22),
              color: OrblitColors.inkDim,
              icon: const Icon(Icons.close),
              onPressed: () => onChanged(null),
            ),
      child: DragTarget<String>(
        onWillAcceptWithDetails: (details) => _takes(details.data),
        onAcceptWithDetails: (details) {
          final to = relative(details.data);
          if (to != null && to != path) onChanged(to);
        },
        builder: (context, candidate, _) => Container(
          height: 24,
          padding: const EdgeInsets.symmetric(horizontal: Space.sm),
          alignment: Alignment.centerLeft,
          decoration: BoxDecoration(
            color: candidate.isEmpty ? OrblitColors.raised : OrblitColors.hover,
            borderRadius: BorderRadius.circular(4),
            border: Border.all(
              color: candidate.isEmpty
                  ? Colors.transparent
                  : OrblitColors.ember,
            ),
          ),
          child: Text(
            path == null ? 'Drop a PNG or JPEG here' : p.basename(path),
            overflow: TextOverflow.ellipsis,
            style: path == null
                ? OrblitText.caption.copyWith(fontSize: 11)
                : OrblitText.monoValue.copyWith(fontSize: 11),
          ),
        ),
      ),
    );
  }
}

/// A choice of one set, from a list that may be too long to lay side by
/// side.
class _SetChoice extends StatelessWidget {
  const _SetChoice({
    required this.label,
    required this.names,
    required this.selected,
    required this.onSelect,
  });

  final String label;
  final List<String> names;
  final int selected;
  final ValueChanged<int> onSelect;

  @override
  Widget build(BuildContext context) {
    final shown = selected >= 0 && selected < names.length
        ? names[selected]
        : 'Set ${selected + 1} (missing)';
    return FieldRow(
      label: label,
      child: PopupMenuButton<int>(
        tooltip: '',
        color: OrblitColors.raised,
        onSelected: onSelect,
        itemBuilder: (context) => [
          for (final (index, name) in names.indexed)
            PopupMenuItem(
              value: index,
              height: 28,
              child: Text(name, style: OrblitText.label),
            ),
        ],
        child: Container(
          height: 24,
          padding: const EdgeInsets.symmetric(horizontal: Space.sm),
          decoration: BoxDecoration(
            color: OrblitColors.raised,
            borderRadius: BorderRadius.circular(4),
          ),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  shown,
                  overflow: TextOverflow.ellipsis,
                  style: OrblitText.label.copyWith(
                    fontSize: 11.5,
                    color: OrblitColors.ink,
                  ),
                ),
              ),
              const Icon(
                Icons.expand_more,
                size: 14,
                color: OrblitColors.inkDim,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
