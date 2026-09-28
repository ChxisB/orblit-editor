import 'package:flutter/widgets.dart';

import '../theme/orblit_theme.dart';
import 'dock.dart';
import 'gizmo.dart';
import 'registry.dart';
import 'viewport_input.dart';

/// A way of working in the editor.
///
/// Laying out a level, shaping the ground and posing a character want the
/// same scene under different hands. What a mode changes is how the panels
/// are arranged, the tools on the shelf, and who is asked first about the
/// pointer in a scene view. The scene and the selection stay as they were,
/// so switching never loses anybody's place.
class EditorMode implements Registered {
  const EditorMode({
    required this.name,
    required this.label,
    required this.icon,
    required this.layout,
    this.tools,
    this.status,
    this.input,
    this.overlay,
    this.onEnter,
    this.onLeave,
  });

  /// Also what its layout is saved under, so a word that is safe in a file
  /// name.
  @override
  final String name;

  final String label;

  final IconData icon;

  /// How the panels are arranged the first time it is used. After that they
  /// are however they were left, kept for each mode separately.
  final DockLayout Function() layout;

  /// Its tool shelf, shown under the menus while it is the mode, in a
  /// [ModeShelf] it builds itself. Null for none.
  final WidgetBuilder? tools;

  /// What the bar along the bottom says on its right while it is the mode.
  /// Null for the scene's file, how many objects it has and the frame rate.
  final WidgetBuilder? status;

  /// First refusal on every gesture in a scene view, ahead of the handles and
  /// the selection. Null passes everything through.
  final ViewportInput? input;

  /// Drawn over every scene view while it is the mode, above the handles:
  /// whatever the mode's own input wants seen, such as a brush's reach.
  final ViewportOverlay? overlay;

  /// Sets up the job as the editor switches to it, the way modelling goes
  /// into the selected shape's parts. Null leaves everything as it was.
  final VoidCallback? onEnter;

  /// Puts back what [onEnter] set up, as the editor switches away.
  final VoidCallback? onLeave;
}

/// The strip under the menus that a mode's tools sit in.
///
/// Built by the shelf rather than around it, so a shelf with nothing to hold
/// builds nothing and leaves no empty strip.
final class ModeShelf extends StatelessWidget {
  const ModeShelf({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 36,
      padding: const EdgeInsets.symmetric(horizontal: Space.md),
      decoration: const BoxDecoration(
        color: OrblitColors.surface,
        border: Border(bottom: BorderSide(color: OrblitColors.lineSoft)),
      ),
      child: Row(children: [Expanded(child: child)]),
    );
  }
}

/// Something drawn over a scene view, given how that view projects.
typedef ViewportOverlay = Widget Function(
  BuildContext context,
  ViewportProjection projection,
);
