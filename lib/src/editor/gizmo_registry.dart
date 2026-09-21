import 'package:flutter/widgets.dart';

import 'gizmo.dart';
import 'registry.dart';
import 'scene.dart';
import 'viewport_input.dart';

/// What a gizmo is being asked about: the object the handles sit on, and the
/// view it is being drawn in.
@immutable
class GizmoTarget {
  const GizmoTarget({
    required this.object,
    required this.scene,
    required this.selected,
    required this.projection,
  });

  /// The one of the selection the handles belong to.
  final SceneObject object;

  /// The scene holding [object]: the open one, or the set every scene shares.
  final EditorScene scene;

  /// Everything selected, [object] included.
  final Set<String> selected;

  /// How the view's pixels map into the world.
  final ViewportProjection projection;
}

/// A kind of thing a scene view draws for what is selected, and what it does
/// with the pointer.
///
/// Keyed by what it applies to rather than listed in the view, so a body, a
/// joint or a brush ring brings its own and the view does not have to know it
/// exists.
class GizmoType implements Registered {
  const GizmoType({
    required this.name,
    required this.appliesTo,
    this.overlay,
    this.input,
  });

  @override
  final String name;

  /// Whether it has anything to show for [GizmoTarget.object].
  final bool Function(GizmoTarget target) appliesTo;

  /// What it draws, as a child of the view's stack: wrap it in a [Positioned]
  /// to say where. Drawn in the order the gizmos were registered, over the
  /// scene and under the view's own chips.
  final Widget Function(GizmoTarget target)? overlay;

  /// What it does with the pointer.
  ///
  /// Asked after the mode's tool and anything being drawn, and before the
  /// selection: a drag that starts on a handle is a handle's, and one that
  /// starts anywhere else is not.
  final bool Function(GizmoTarget target, ViewportGesture gesture)? input;
}
