import 'package:flutter/widgets.dart';

import 'dock.dart';
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
    this.input,
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

  /// Its tool shelf, shown under the menus while it is the mode. Null for
  /// none.
  final WidgetBuilder? tools;

  /// First refusal on every gesture in a scene view, ahead of the handles and
  /// the selection. Null passes everything through.
  final ViewportInput? input;
}
