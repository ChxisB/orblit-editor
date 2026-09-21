import 'package:flutter/material.dart';

import '../theme/orblit_theme.dart';
import 'dock.dart';
import 'registry.dart';

/// A kind of panel the editor can show, and how to build one.
///
/// The layout says where a panel goes and this says what it is. The shell
/// looks a panel's kind up here and calls [build]; it has no list of its own,
/// so a panel registered from outside the core opens, docks, saves and comes
/// back exactly as the built-in ones do.
class PanelType implements Registered {
  const PanelType({
    required this.kind,
    required this.build,
    this.showsMovement = false,
    this.opensAs,
  });

  final PanelKind kind;

  /// Builds what goes inside [panel].
  final Widget Function(BuildContext context, DockPanel panel) build;

  /// Whether it shows where things are.
  ///
  /// A drag only changes where things are, so a panel that does not show that
  /// is handed back exactly as it was while one is running — Flutter compares
  /// the widget by identity and skips the whole subtree.
  final bool showsMovement;

  /// The id the View menu opens one under, when it is not the kind's name.
  ///
  /// The first scene view is `scene`, because that is what every layout
  /// written before this called it.
  final String? opensAs;

  /// The one the View menu opens.
  DockPanel get panel => DockPanel(id: opensAs ?? kind.name, kind: kind);

  @override
  String get name => kind.name;
}

/// Every kind of panel something has registered.
typedef PanelRegistry = Registry<PanelType>;

extension PanelKinds on Registry<PanelType> {
  /// What a saved layout is read against.
  Iterable<PanelKind> get kinds => [for (final type in all) type.kind];

  PanelType? typeOf(PanelKind kind) => this[kind.name];
}

/// What stands in a panel whose kind nothing has registered.
///
/// A layout read from a file cannot have one — it leaves those out — so this
/// is only ever a panel added in code for something that forgot to register.
/// Shown rather than thrown, because a mistake in one panel should not take
/// the rest of the window down with it.
class UnregisteredPanel extends StatelessWidget {
  const UnregisteredPanel({super.key, required this.panel});

  final DockPanel panel;

  @override
  Widget build(BuildContext context) => Center(
    child: Text(
      'Nothing is registered to show a ${panel.kind.name} panel.',
      style: OrblitText.caption,
    ),
  );
}
