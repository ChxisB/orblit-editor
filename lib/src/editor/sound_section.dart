import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'component_rows.dart';
import 'inspector.dart';
import 'scene.dart';

T? _of<T extends doc.SceneComponent>(SceneObject object, String type) =>
    switch (object.components[type]) {
      final T component => component,
      _ => null,
    };

/// The sound source on [object], if it has one.
doc.SoundSourceComponent? soundSourceOf(SceneObject object) =>
    _of(object, doc.SceneComponents.soundSource);

doc.SoundListenerComponent? _listenerOf(SceneObject object) =>
    _of(object, doc.SceneComponents.soundListener);

doc.SoundSurfaceComponent? _surfaceOf(SceneObject object) =>
    _of(object, doc.SceneComponents.soundSurface);

/// How close the near and far ends of a source may come, so that dragging one
/// past the other leaves a range and not an error in the player.
const double _gap = 0.1;

/// [source] with its near end at [near], held below the far end. A range
/// narrower than twice the gap keeps the near end above zero.
doc.SoundSourceComponent withNear(
  doc.SoundSourceComponent source,
  double near,
) => source.copyWith(
  near: math.min(near, source.far - math.min(_gap, source.far / 2)),
);

/// [source] with its far end at [far], held above the near end.
doc.SoundSourceComponent withFar(doc.SoundSourceComponent source, double far) =>
    source.copyWith(far: math.max(far, source.near + _gap));

/// [source] with the clip [text], or with none for an empty one, since a
/// source with no clip is how one is placed before its sound is chosen.
doc.SoundSourceComponent withClip(
  doc.SoundSourceComponent source,
  String text,
) => text.isNotEmpty
    ? source.copyWith(clip: text)
    : doc.SoundSourceComponent(
        bus: source.bus,
        loop: source.loop,
        volume: source.volume,
        near: source.near,
        far: source.far,
        curve: source.curve,
      );

/// The inspector's section for sound: a source, where sound is heard from,
/// and what an object is made of when two things touch.
///
/// Offered on every object, since any of them can make a sound.
InspectorSection soundSection() => InspectorSection(
  name: 'sound',
  appliesTo: (target) =>
      target.object.kind != ObjectKind.scene &&
      target.object.kind != ObjectKind.weather,
  build: (target) => _SoundSection(target: target),
);

class _SoundSection extends StatelessWidget {
  const _SoundSection({required this.target});

  final InspectorTarget target;

  @override
  Widget build(BuildContext context) {
    final name = target.object.name;
    final source = ComponentRows<doc.SoundSourceComponent>(
      target,
      type: doc.SceneComponents.soundSource,
      of: soundSourceOf,
    );
    final listener = ComponentRows<doc.SoundListenerComponent>(
      target,
      type: doc.SceneComponents.soundListener,
      of: _listenerOf,
    );
    final surface = ComponentRows<doc.SoundSurfaceComponent>(
      target,
      type: doc.SceneComponents.soundSurface,
      of: _surfaceOf,
    );
    return OrblitSection(
      title: 'Sound',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          ..._source(source, name),
          const SizedBox(height: Space.xs),
          ChoiceRow(
            label: 'Listener',
            options: const ['No', 'Yes'],
            selected: _listenerOf(target.object) == null ? 'No' : 'Yes',
            onSelect: (choice) {
              if ((choice == 'Yes') == (_listenerOf(target.object) != null)) {
                return;
              }
              listener.put(
                choice == 'Yes' ? const doc.SoundListenerComponent() : null,
                label: 'Set $name listener',
                alone: true,
              );
            },
          ),
          FieldRow(
            label: 'Surface',
            child: ValueField(
              value: _surfaceOf(target.object)?.name ?? '',
              hint: 'wood',
              mono: true,
              // An empty name is no surface, so the file does not keep one.
              onChanged: (text) => surface.put(
                text.isEmpty ? null : doc.SoundSurfaceComponent(name: text),
                label: 'Set $name surface',
              ),
              onDone: target.history.seal,
            ),
          ),
        ],
      ),
    );
  }

  List<Widget> _source(
    ComponentRows<doc.SoundSourceComponent> rows,
    String name,
  ) {
    if (soundSourceOf(target.object) == null) {
      return [
        OrblitButton(
          label: 'Add sound source',
          tooltip: 'Make this object a place a sound comes from.',
          icon: Icons.add,
          tone: ButtonTone.quiet,
          expand: true,
          onPressed: () => rows.put(
            doc.SoundSourceComponent(),
            label: 'Add sound source to $name',
            alone: true,
          ),
        ),
      ];
    }
    return [
      rows.text(
        'Clip',
        (source) => source.clip ?? '',
        withClip,
        hint: 'sounds/fire.ogg',
      ),
      rows.text(
        'Bus',
        (source) => source.bus,
        (source, text) => source.copyWith(bus: text.isEmpty ? 'master' : text),
        hint: 'master',
      ),
      rows.toggle(
        'Loop',
        (source) => source.loop,
        (source, on) => source.copyWith(loop: on),
      ),
      rows.drag(
        'Volume',
        (source) => source.volume,
        (source, value) => source.copyWith(volume: value),
        minimum: 0,
        step: 0.05,
      ),
      rows.drag(
        'Near',
        (source) => source.near,
        withNear,
        minimum: _gap,
        step: 0.1,
      ),
      rows.drag(
        'Far',
        (source) => source.far,
        withFar,
        minimum: _gap * 2,
        step: 0.5,
        decimals: 1,
      ),
      rows.choice(
        'Fade',
        const ['Inverse', 'Linear'],
        (source) =>
            source.curve == doc.SoundCurve.linear ? 'Linear' : 'Inverse',
        (source, option) => source.copyWith(
          curve: option == 'Linear'
              ? doc.SoundCurve.linear
              : doc.SoundCurve.inverse,
        ),
      ),
      const SizedBox(height: Space.xs),
      OrblitButton(
        label: 'Remove sound source',
        tooltip: 'Stop this object making a sound.',
        icon: Icons.remove,
        tone: ButtonTone.quiet,
        expand: true,
        onPressed: () => rows.put(
          null,
          label: 'Remove sound source from $name',
          alone: true,
        ),
      ),
    ];
  }
}
