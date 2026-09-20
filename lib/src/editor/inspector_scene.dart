part of 'inspector.dart';

/// The scene's own settings.
///
/// A scene is a thing with properties, not just a container — the sky and the
/// light it casts belong to it rather than to anything in it, and there was
/// nowhere to put them until it had a row of its own.
class _SceneFields extends StatelessWidget {
  const _SceneFields({
    super.key,
    required this.entry,
    required this.history,
    required this.onLoad,
  });

  final SceneEntry entry;
  final History history;
  final ValueChanged<SceneEntry> onLoad;

  /// The time as it stands, likewise.
  ({double hour, bool cycle, double speed}) _timeOf(EditorScene scene) => (
    hour: scene.timeOfDay,
    cycle: scene.dayCycle,
    speed: scene.hoursPerSecond,
  );

  /// A light level, at a precision that says something at both ends of the
  /// day. A night rounded to the nearest lux is a night that reads as zero.
  static String _lux(double lux) =>
      lux >= 10 ? '${lux.round()} lx' : '${lux.toStringAsFixed(2)} lx';

  /// An hour as a clock reads it.
  static String _clock(double hour) {
    final total = ((hour % 24) * 60).round();
    final hours = (total ~/ 60) % 24;
    final minutes = total % 60;
    return '${hours.toString().padLeft(2, '0')}:'
        '${minutes.toString().padLeft(2, '0')}';
  }

  @override
  Widget build(BuildContext context) {
    final scene = entry.scene;

    // An unloaded scene has no document to show settings from. Saying where it
    // is and offering to open it beats a panel of fields that would edit
    // nothing.
    if (scene == null) return _unloaded();

    // What every scene has is a scene, and almost nothing about a scene
    // applies to it: it has no sky of its own, and no weather, because it is
    // in whatever sky the open scene has.
    if (entry.id == sharedSceneId) return _shared(scene);

    return ListView(
      padding: const EdgeInsets.symmetric(vertical: Space.sm),
      children: [
        _Header(
          name: scene.name,
          icon: Icons.public,
          onRename: (value) {
            if (value == scene.name) return;
            history.run(
              RenameScene(sceneId: entry.id, from: scene.name, to: value),
            );
          },
          onRenameDone: history.seal,
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(Space.md, 0, Space.md, Space.sm),
          child: Text(
            entry.path == null
                ? 'Not saved to a file yet'
                : p.basename(entry.path!),
            overflow: TextOverflow.ellipsis,
            style: OrblitText.mono.copyWith(fontSize: 11),
          ),
        ),
        _sky(scene),
        _environment(scene),
        _contents(scene),
      ],
    );
  }

  Widget _unloaded() {
    return ListView(
      padding: const EdgeInsets.symmetric(vertical: Space.sm),
      children: [
        _Header(
          name: entry.title,
          icon: Icons.public_off,
          onRename: (_) {},
          onRenameDone: () {},
          editable: false,
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(Space.md, 0, Space.md, Space.md),
          child: Text(
            entry.path ?? 'Never saved',
            overflow: TextOverflow.ellipsis,
            style: OrblitText.mono.copyWith(fontSize: 11),
          ),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: Space.md),
          child: OrblitButton(
            label: 'Load scene',
            icon: Icons.folder_open,
            expand: true,
            onPressed: () => onLoad(entry),
          ),
        ),
        Padding(
          padding: const EdgeInsets.all(Space.md),
          child: Text(
            'Loading a scene replaces the one open. Only one scene is in the '
            'viewport at a time.',
            style: OrblitText.caption,
          ),
        ),
      ],
    );
  }

  Widget _shared(EditorScene scene) {
    return ListView(
      padding: const EdgeInsets.symmetric(vertical: Space.sm),
      children: [
        _Header(
          name: 'Shared',
          icon: Icons.inventory_2_outlined,
          onRename: (_) {},
          onRenameDone: () {},
          editable: false,
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(Space.md, 0, Space.md, Space.md),
          child: Text(
            'What every scene in this project has in it. Objects here are '
            'drawn and lit alongside whichever scene is open, and saved '
            'beside it.',
            style: OrblitText.caption,
          ),
        ),
        OrblitSection(
          title: 'Contents',
          icon: Icons.list,
          child: Column(
            children: [
              TextRow(label: 'Objects', value: '${scene.length}'),
              TextRow(
                label: 'Drawn',
                value: '${scene.objects.where((o) => o.isDrawable).length}',
              ),
              TextRow(
                label: 'Lights',
                value:
                    '${scene.objects.where((o) => o.kind == ObjectKind.light).length}',
              ),
            ],
          ),
        ),
        Padding(
          padding: const EdgeInsets.all(Space.md),
          child: Text(
            'A scene of its own overrules it: a sun or a Weather object in '
            'the open scene is used instead of the one here, so a level can '
            'have its own without the shared one being in the way.',
            style: OrblitText.caption,
          ),
        ),
      ],
    );
  }

  Widget _sky(EditorScene scene) {
    return OrblitSection(
      title: 'Sky',
      icon: Icons.schedule,
      child: Column(
        children: [
          ChoiceRow(
            label: 'Day cycle',
            options: const ['Off', 'On'],
            selected: scene.dayCycle ? 'On' : 'Off',
            onSelect: (value) {
              final wanted = value == 'On';
              if (wanted == scene.dayCycle) return;
              history
                ..run(
                  SetSceneTime(
                    sceneId: entry.id,
                    from: _timeOf(scene),
                    to: (
                      hour: scene.timeOfDay,
                      cycle: wanted,
                      speed: scene.hoursPerSecond,
                    ),
                  ),
                )
                ..seal();
            },
          ),
          SliderRow(
            label: 'Time',
            value: scene.timeOfDay,
            min: 0,
            max: 24,
            decimals: 2,
            onChanged: (value) => history.run(
              SetSceneTime(
                sceneId: entry.id,
                from: _timeOf(scene),
                to: (
                  hour: value,
                  cycle: scene.dayCycle,
                  speed: scene.hoursPerSecond,
                ),
              ),
            ),
            onSettled: history.seal,
          ),
          TextRow(
            label: scene.dayCycle ? 'Now' : 'Set to',
            value:
                '${_clock(scene.currentTimeOfDay)}'
                '  ${scene.activeBody.label}',
          ),
          if (scene.dayCycle)
            SliderRow(
              label: 'Speed',
              value: scene.hoursPerSecond,
              min: 0.05,
              max: 6,
              decimals: 2,
              unit: ' h/s',
              onChanged: (value) => history.run(
                SetSceneTime(
                  sceneId: entry.id,
                  from: _timeOf(scene),
                  to: (hour: scene.timeOfDay, cycle: true, speed: value),
                ),
              ),
              onSettled: history.seal,
            ),
          Padding(
            padding: const EdgeInsets.fromLTRB(
              Space.md,
              Space.xs,
              Space.md,
              0,
            ),
            child: Text(
              scene.dayCycle
                  ? 'The time runs from where it is set, and the light '
                        'above the scene is whichever body is up. The scene '
                        'keeps the hour it was saved at.'
                  : 'The scene sits at this hour. The light above it is '
                        'whichever body it is set to be.',
              style: OrblitText.caption,
            ),
          ),
        ],
      ),
    );
  }

  Widget _environment(EditorScene scene) {
    return OrblitSection(
      title: 'Environment',
      icon: Icons.wb_twilight,
      child: Column(
        children: [
          // Under a running day these are answers rather than questions:
          // a slider that cannot move is worse than a value that says
          // where it came from.
          if (scene.dayCycle) ...[
            TextRow(label: 'Sky', value: 'From the time of day'),
            TextRow(label: 'Ambient', value: _lux(scene.skyState.ambient)),
          ] else ...[
            ColourRow(
              label: 'Sky',
              value: scene.skyColour,
              onChanged: (value) => history
                ..run(
                  SetSceneSky(
                    sceneId: entry.id,
                    fromColour: scene.skyColour,
                    toColour: value,
                    fromAmbient: scene.ambient,
                    toAmbient: scene.ambient,
                  ),
                )
                ..seal(),
            ),
            SliderRow(
              label: 'Ambient',
              value: scene.ambient,
              min: 0,
              max: 120000,
              unit: ' lx',
              onChanged: (value) => history.run(
                SetSceneSky(
                  sceneId: entry.id,
                  fromColour: scene.skyColour,
                  toColour: scene.skyColour,
                  fromAmbient: scene.ambient,
                  toAmbient: value,
                ),
              ),
              onSettled: history.seal,
            ),
          ],
        ],
      ),
    );
  }

  Widget _contents(EditorScene scene) {
    return OrblitSection(
      title: 'Contents',
      icon: Icons.list,
      child: Column(
        children: [
          TextRow(label: 'Objects', value: '${scene.length}'),
          TextRow(
            label: 'Drawn',
            value: '${scene.objects.where((o) => o.isDrawable).length}',
          ),
          TextRow(
            label: 'Lights',
            value:
                '${scene.objects.where((o) => o.kind == ObjectKind.light).length}',
          ),
        ],
      ),
    );
  }
}
