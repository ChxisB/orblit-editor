import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:orblit_motion/orblit_motion.dart' show CutsceneShot;
import 'package:vector_math/vector_math_64.dart' show Matrix4, Vector3, degrees;

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'clip_bench.dart';
import 'dock.dart';
import 'editor_mode.dart';
import 'game_view.dart' show cameraOf;
import 'inspector.dart' show FieldRow;
import 'scene.dart';
import 'timeline_sheet.dart' show secondsLabel;
import 'viewport.dart' show OrbitCamera;

/// The finished shot, beside the scene view in the Cinematics workspace.
const PanelKind shotPreviewPanel = PanelKind(
  'shotPreview',
  'Shot',
  Icons.movie_outlined,
);

/// The open cutscene's shots, one card each.
const PanelKind shotListPanel = PanelKind(
  'shots',
  'Shots',
  Icons.video_library_outlined,
);

/// The cutscene's keys, marks and shots over time, under the view.
const PanelKind cutsceneTimelinePanel = PanelKind(
  'cutsceneTimeline',
  'Cutscene',
  Icons.view_timeline_outlined,
);

/// Making cutscenes: moving things over time and cutting between cameras.
///
/// [onAddShot] cuts to a camera at the playhead, and [onUseView] puts a
/// camera where the scene view is and cuts to that. [onLeave] stops the
/// scene view steering a camera.
EditorMode cinematicsMode({
  required ClipBench bench,
  required VoidCallback onNew,
  required VoidCallback onAddShot,
  required VoidCallback onUseView,
  required VoidCallback onLeave,
}) => EditorMode(
  name: 'cinematics',
  label: 'Cinematics',
  icon: Icons.movie_outlined,
  layout: cinematicsLayout,
  tools: (_) => CinematicsShelf(
    bench: bench,
    onNew: onNew,
    onAddShot: onAddShot,
    onUseView: onUseView,
  ),
  onLeave: onLeave,
);

/// The scene view with the finished shot beside it, so a camera is framed
/// against what it will show. The shots come first on the right. The
/// cutscene is open under the view and as tall as the timeline is in
/// Animation, because timing is the work here.
DockLayout cinematicsLayout() => DockLayout.columns(
  const DockSplit(
    id: 'views',
    axis: Axis.horizontal,
    weights: [0.5, 0.5],
    children: [
      DockGroup(
        id: 'centre',
        panels: [DockPanel(id: 'scene', kind: PanelKind.viewport)],
      ),
      DockGroup(
        id: 'finished',
        panels: [DockPanel(id: 'shot', kind: shotPreviewPanel)],
      ),
    ],
  ),
  bottom: const [
    DockPanel(id: 'cutsceneTimeline', kind: cutsceneTimelinePanel),
  ],
  right: const [
    DockPanel(id: 'shots', kind: shotListPanel),
    DockPanel(id: 'inspector', kind: PanelKind.inspector),
  ],
  folded: false,
  below: 0.4,
);

/// [shots] with a cut to [camera] at [at], in a cutscene [length] long.
///
/// The shot running at [at] ends there and one starting there gives way,
/// so the change is a cut and not a blend. The new shot holds until the
/// next one starts, or to the end.
List<CutsceneShot> cutTo(
  List<CutsceneShot> shots, {
  required String camera,
  required double at,
  required double length,
}) {
  final next = shots
      .map((shot) => shot.start)
      .where((start) => start > at)
      .fold<double>(length, math.min);
  // At the very end there is nothing left to hold for, and a shot has to
  // last some time, so it gets a couple of seconds past the end.
  final end = next > at ? next : at + 2;
  return [
    for (final shot in shots)
      if (shot.start < at && shot.end > at)
        shot.copyWith(duration: at - shot.start)
      else if (shot.start != at)
        shot,
    CutsceneShot(camera: camera, start: at, duration: end - at),
  ];
}

/// The scene view put where [camera] is and looking where it looks, turning
/// around a point a few metres ahead.
OrbitCamera viewThrough(EditorScene scene, SceneObject camera) {
  final seen = cameraOf(scene, camera);
  final forward = (seen.target - seen.position).normalized();
  // Short of straight up or down, as orbiting keeps it, because the view
  // matrix collapses there.
  const limit = math.pi / 2 - 0.02;
  const distance = 5.0;
  return OrbitCamera(
    yaw: math.atan2(-forward.x, -forward.z),
    pitch: math.asin(-forward.y.clamp(-1.0, 1.0)).clamp(-limit, limit),
    distance: distance,
    target: seen.position + forward * distance,
  );
}

/// Where a camera under [parent] has to stand, and how it turns in degrees,
/// to see what [view] sees. A null [parent] is the scene itself. A view
/// cannot roll, so neither does this.
({Vector3 position, Vector3 rotation}) cameraPlaceFor(
  OrbitCamera view,
  EditorScene scene, {
  String? parent,
}) {
  final position = view.toRenderCamera().position;
  final rotation = Vector3(degrees(-view.pitch), degrees(view.yaw), 0);
  if (parent == null || !scene.contains(parent)) {
    // As they are. Read back from a matrix they can come out as another set
    // of angles that turns the same way, which reads oddly in the inspector.
    return (position: position, rotation: rotation);
  }
  final world = Matrix4.translation(position)
    ..multiply(rotationFromDegrees(rotation));
  final local = Matrix4.inverted(scene.worldOf(parent)).multiplied(world);
  return (position: local.getTranslation(), rotation: eulerDegreesOf(local));
}

/// The Cinematics workspace's shelf: a new cutscene, and the two ways to
/// add a shot to the open one.
final class CinematicsShelf extends StatelessWidget {
  const CinematicsShelf({
    super.key,
    required this.bench,
    required this.onNew,
    required this.onAddShot,
    required this.onUseView,
  });

  final ClipBench bench;
  final VoidCallback onNew;
  final VoidCallback onAddShot;
  final VoidCallback onUseView;

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: bench,
    builder: (context, _) {
      // A shot belongs to a cutscene, so both wait for one to be open.
      final open = bench.shown?.shots != null;
      return ModeShelf(
        child: SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(
            children: [
              OrblitButton(
                label: 'New cutscene',
                tooltip: 'Create a cutscene for this scene.',
                icon: Icons.add,
                tone: ButtonTone.quiet,
                onPressed: onNew,
              ),
              const SizedBox(width: Space.sm),
              OrblitButton(
                label: 'Add shot',
                tooltip: 'Cut to the selected camera at the playhead.',
                icon: Icons.videocam_outlined,
                tone: ButtonTone.quiet,
                onPressed: open ? onAddShot : null,
              ),
              const SizedBox(width: Space.sm),
              OrblitButton(
                label: 'Use this view',
                tooltip:
                    'Put a camera where the scene view is, and cut to it at '
                    'the playhead.',
                icon: Icons.add_a_photo_outlined,
                tone: ButtonTone.quiet,
                onPressed: open ? onUseView : null,
              ),
            ],
          ),
        ),
      );
    },
  );
}

/// The longest a typed start or length can be, which is as long as a
/// clip can be.
const double _longest = 600;

/// The open cutscene's shots, first to last: the camera each looks through,
/// when it starts and how long it holds.
///
/// [lookingThrough] is the camera the scene view steers, if any, and
/// [onLookThrough] starts steering one, or stops with null.
final class ShotList extends StatefulWidget {
  const ShotList({
    super.key,
    required this.bench,
    required this.scene,
    required this.lookingThrough,
    required this.onLookThrough,
  });

  final ClipBench bench;
  final EditorScene? scene;
  final String? lookingThrough;
  final ValueChanged<String?> onLookThrough;

  @override
  State<ShotList> createState() => _ShotListState();
}

final class _ShotListState extends State<ShotList> {
  /// What each card is keyed by, handed on from a shot to the one an edit
  /// makes of it. Typing a start can move a shot past another, and the
  /// card being typed in has to move with it rather than hand its focus to
  /// the shot that took its place.
  final Expando<Object> _cards = Expando();

  /// Ties one typed number's edits into one undo step.
  Object? _typing;

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: widget.bench,
    builder: (context, _) {
      final shots = widget.bench.shown?.shots;
      if (shots == null) {
        return const PanelMessage(
          'The shots of an open cutscene show here. Choose New cutscene on '
          'the shelf.',
        );
      }
      if (shots.isEmpty) {
        return const PanelMessage(
          'No shots yet. Add shot cuts to the selected camera at the '
          'playhead, and Use this view puts a camera where the scene view '
          'is.',
        );
      }
      final cameras = [
        for (final object in widget.scene?.objects ?? const <SceneObject>[])
          if (object.kind == ObjectKind.camera) object,
      ];
      return ListView(
        padding: const EdgeInsets.only(top: Space.sm),
        children: [
          for (final (index, shot) in shots.indexed)
            _ShotCard(
              key: ObjectKey(_cardOf(shot)),
              list: this,
              number: index + 1,
              shot: shot,
              cameras: cameras,
            ),
        ],
      );
    },
  );

  /// Made the first time a shot is shown, and kept for as long as it is.
  Object _cardOf(CutsceneShot shot) => _cards[shot] ??= Object();

  /// Swaps [shot] for [next], or takes it out when [next] is null.
  void _replace(
    String label,
    CutsceneShot shot,
    CutsceneShot? next, {
    Object? gesture,
  }) {
    if (next != null) _cards[next] = _cardOf(shot);
    widget.bench.editShots(
      label,
      (shots) => [
        for (final one in shots)
          if (!identical(one, shot)) one else ?next,
      ],
      gesture: gesture,
    );
  }

  /// A number typed into one of [shot]'s fields, as it is typed. [make] is
  /// null for a number that does not make a shot, which waits for the next
  /// keystroke.
  void _typed(
    CutsceneShot shot,
    String label,
    CutsceneShot? Function(double value) make,
    String text,
  ) {
    final value = double.tryParse(text.trim());
    final next = value == null ? null : make(value);
    if (next == null) return;
    _replace(label, shot, next, gesture: _typing ??= Object());
  }

  void _typedDone() {
    _typing = null;
    widget.bench.history.seal();
  }
}

/// One shot: its camera, when it starts, how long it holds, and the
/// buttons that look through it and take it out.
final class _ShotCard extends StatelessWidget {
  const _ShotCard({
    super.key,
    required this.list,
    required this.number,
    required this.shot,
    required this.cameras,
  });

  final _ShotListState list;
  final int number;
  final CutsceneShot shot;
  final List<SceneObject> cameras;

  @override
  Widget build(BuildContext context) => OrblitSection(
    title: 'Shot $number',
    icon: Icons.videocam_outlined,
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        FieldRow(
          label: 'Camera',
          child: _CameraChoice(
            chosen: shot.camera,
            cameras: cameras,
            onChosen: (id) => list._replace(
              'Change shot camera',
              shot,
              shot.copyWith(camera: id),
            ),
          ),
        ),
        _SecondsRow(
          label: 'Starts at',
          value: shot.start,
          onChanged: (text) => list._typed(
            shot,
            'Move shot',
            (at) => at < 0 || at > _longest ? null : shot.copyWith(start: at),
            text,
          ),
          onDone: list._typedDone,
        ),
        _SecondsRow(
          label: 'Length',
          value: shot.duration,
          onChanged: (text) => list._typed(
            shot,
            'Change shot length',
            (length) => length <= 0 || length > _longest
                ? null
                : shot.copyWith(duration: length),
            text,
          ),
          onDone: list._typedDone,
        ),
        const SizedBox(height: Space.xs),
        _ShotButtons(list: list, shot: shot, cameras: cameras),
      ],
    ),
  );
}

/// Look through and Delete, under a shot's fields.
final class _ShotButtons extends StatelessWidget {
  const _ShotButtons({
    required this.list,
    required this.shot,
    required this.cameras,
  });

  final _ShotListState list;
  final CutsceneShot shot;
  final List<SceneObject> cameras;

  @override
  Widget build(BuildContext context) {
    final there = cameras.any((camera) => camera.id == shot.camera);
    final looking = list.widget.lookingThrough == shot.camera;
    return OrblitButtonRow(
      buttons: [
        OrblitButton(
          label: looking ? 'Stop looking' : 'Look through',
          tooltip: looking
              ? 'Stop steering this camera from the scene view.'
              : 'Fly the scene view to this camera, and steer the camera '
                    'by moving the view.',
          icon: Icons.visibility_outlined,
          tone: looking ? ButtonTone.primary : ButtonTone.quiet,
          expand: true,
          onPressed: there
              ? () => list.widget.onLookThrough(looking ? null : shot.camera)
              : null,
        ),
        OrblitButton(
          label: 'Delete',
          tooltip: 'Take this shot out of the cutscene.',
          icon: Icons.delete_outline,
          tone: ButtonTone.quiet,
          expand: true,
          onPressed: () => list._replace('Delete shot', shot, null),
        ),
      ],
    );
  }
}

/// A number of seconds, typed.
final class _SecondsRow extends StatelessWidget {
  const _SecondsRow({
    required this.label,
    required this.value,
    required this.onChanged,
    required this.onDone,
  });

  final String label;
  final double value;
  final ValueChanged<String> onChanged;
  final VoidCallback onDone;

  @override
  Widget build(BuildContext context) => FieldRow(
    label: label,
    trailing: const Text('s', style: OrblitText.caption),
    child: ValueField(
      value: secondsLabel(value),
      mono: true,
      onChanged: onChanged,
      onDone: onDone,
    ),
  );
}

/// Which camera a shot looks through, chosen from the scene's cameras.
///
/// One the scene no longer has shows its id in the warning colour, since
/// the game would have nothing to look through for that shot.
final class _CameraChoice extends StatelessWidget {
  const _CameraChoice({
    required this.chosen,
    required this.cameras,
    required this.onChosen,
  });

  final String chosen;
  final List<SceneObject> cameras;
  final ValueChanged<String> onChosen;

  @override
  Widget build(BuildContext context) {
    final named = cameras.where((camera) => camera.id == chosen).firstOrNull;
    return MenuAnchor(
      style: orblitMenuStyle,
      menuChildren: [
        for (final camera in cameras)
          MenuItemButton(
            onPressed: () => onChosen(camera.id),
            leadingIcon: Icon(
              camera.id == chosen ? Icons.check : null,
              size: 14,
              color: OrblitColors.inkMid,
            ),
            child: Text(camera.name, style: OrblitText.label),
          ),
      ],
      builder: (context, controller, _) => _Chosen(
        label: named?.name ?? chosen,
        missing: named == null,
        onTap: () => controller.isOpen ? controller.close() : controller.open(),
      ),
    );
  }
}

final class _Chosen extends StatelessWidget {
  const _Chosen({
    required this.label,
    required this.missing,
    required this.onTap,
  });

  final String label;
  final bool missing;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final colour = missing ? OrblitColors.warn : OrblitColors.ink;
    final box = MouseRegion(
      cursor: SystemMouseCursors.click,
      child: GestureDetector(
        onTap: onTap,
        child: Container(
          height: 24,
          padding: const EdgeInsets.symmetric(horizontal: Space.sm),
          decoration: BoxDecoration(
            color: OrblitColors.raised,
            borderRadius: BorderRadius.circular(Radii.control),
          ),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: OrblitText.label.copyWith(color: colour),
                ),
              ),
              Icon(Icons.arrow_drop_down, size: 14, color: colour),
            ],
          ),
        ),
      ),
    );
    return missing
        ? Tooltip(message: 'This scene has no such camera.', child: box)
        : box;
  }
}
