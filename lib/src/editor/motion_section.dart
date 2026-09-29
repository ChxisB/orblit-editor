import 'package:flutter/material.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'clip_bench.dart';
import 'commands.dart';
import 'inspector.dart';
import 'scene.dart';

doc.MotionComponent? motionOf(SceneObject object) =>
    switch (object.components[doc.SceneComponents.motion]) {
      final doc.MotionComponent motion => motion,
      _ => null,
    };

InspectorSection motionSection({
  required ClipBench bench,
  required ValueChanged<String> onOpen,
  required VoidCallback onAttach,
}) => InspectorSection(
  name: 'motion',
  appliesTo: (target) => target.object.kind != ObjectKind.scene,
  build: (target) => _MotionSection(
    target: target,
    bench: bench,
    onOpen: onOpen,
    onAttach: onAttach,
  ),
);

final class _MotionSection extends StatelessWidget {
  const _MotionSection({
    required this.target,
    required this.bench,
    required this.onOpen,
    required this.onAttach,
  });

  final InspectorTarget target;
  final ClipBench bench;
  final ValueChanged<String> onOpen;
  final VoidCallback onAttach;

  void _put(doc.MotionComponent? motion) {
    target.history.seal();
    target.history.run(
      SetObjectComponent(
        sceneId: target.sceneId,
        id: target.object.id,
        label: 'Change animation on ${target.object.name}',
        type: doc.SceneComponents.motion,
        from: motionOf(target.object),
        to: motion,
      ),
    );
    target.history.seal();
  }

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: bench,
    builder: (context, _) {
      final motion = motionOf(target.object);
      return OrblitSection(
        title: 'Animation clips',
        icon: Icons.animation,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (motion == null || motion.clips.isEmpty)
              const Text(
                'Make a clip in Animation to move this object. '
                'You can also attach the clip open on the Timeline.',
                style: OrblitText.caption,
              ),
            for (final path in motion?.clips ?? <String>[])
              OrblitButton(
                label: path,
                expand: true,
                tooltip: 'Open this object’s clip in Animation.',
                onPressed: () => onOpen(path),
              ),
            if (motion != null && motion.clips.isNotEmpty)
              ChoiceRow(
                label: 'On start',
                options: ['None', ...motion.clips],
                selected: motion.clips.contains(motion.autoplay)
                    ? motion.autoplay!
                    : 'None',
                onSelect: (path) => _put(
                  doc.MotionComponent(
                    clips: motion.clips,
                    autoplay: path == 'None' ? null : path,
                  ),
                ),
              ),
            OrblitButton(
              label: 'Attach open clip',
              expand: true,
              tooltip: 'Let this object play the clip open on the Timeline.',
              onPressed: bench.shown == null ? null : onAttach,
            ),
            if (motion != null)
              OrblitButton(
                label: 'Remove animation',
                expand: true,
                tooltip: 'Unlink the clips from this object. Keep the files.',
                onPressed: () => _put(null),
              ),
          ],
        ),
      );
    },
  );
}
