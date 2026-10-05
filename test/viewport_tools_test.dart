import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/editor_shell.dart';
import 'package:orblit_editor/src/editor/gizmo.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_editor/src/launcher/project.dart';
import 'package:orblit_editor/src/theme/orblit_theme.dart';
import 'package:orblit_editor/src/widgets/icon_tile.dart';

import 'support/editor_shell.dart';

// The strip of tools over the corner of the view, and the chips that make
// room for it.

void main() {
  useShell();

  /// A test that runs as it would on a Mac, where the renderer runs, with the
  /// shell open at [size].
  ///
  /// Pumped for a moment rather than settled: the renderer keeps a frame
  /// scheduled, so pumpAndSettle waits for one that never comes. The platform
  /// is put back inside the test, because the binding checks for it before
  /// any tear-down runs.
  void drawing(
    String description,
    Future<void> Function(WidgetTester tester) body, {
    Size size = const Size(1440, 900),
  }) {
    testWidgets(description, (tester) async {
      debugDefaultTargetPlatformOverride = TargetPlatform.macOS;
      try {
        await tester.binding.setSurfaceSize(size);
        addTearDown(() => tester.binding.setSurfaceSize(null));
        await tester.pumpWidget(
          MaterialApp(
            theme: orblitTheme(),
            home: EditorShell(
              project: Project(
                name: 'Test',
                directory: root.path,
                lastOpened: DateTime(2026),
              ),
              onClose: () {},
            ),
          ),
        );
        await tester.pump(const Duration(milliseconds: 400));
        await body(tester);
      } finally {
        debugDefaultTargetPlatformOverride = null;
      }
    });
  }

  Finder tool(GizmoMode mode) => find.ancestor(
    of: find.byIcon(mode.icon),
    matching: find.byType(IconTile),
  );

  bool isOn(WidgetTester tester, GizmoMode mode) =>
      tester.widget<IconTile>(tool(mode)).active;

  drawing('every tool is in the strip, stacked down the left', (tester) async {
    final view = tester.getRect(find.byType(SceneViewport));
    final move = tester.getRect(tool(GizmoMode.move));
    final rotate = tester.getRect(tool(GizmoMode.rotate));

    expect(move.left, rotate.left);
    expect(rotate.top, greaterThan(move.bottom - 1));
    expect(move.left - view.left, lessThan(32));
    expect(move.top - view.top, lessThan(32));
  });

  drawing('move is on to begin with, and a tap switches to rotate', (
    tester,
  ) async {
    expect(isOn(tester, GizmoMode.move), isTrue);
    expect(isOn(tester, GizmoMode.rotate), isFalse);

    await tester.tap(tool(GizmoMode.rotate));
    await tester.pump();

    expect(isOn(tester, GizmoMode.move), isFalse);
    expect(isOn(tester, GizmoMode.rotate), isTrue);
  });

  drawing('each tool says what it is', (tester) async {
    expect(find.byTooltip('Move'), findsOneWidget);
    expect(find.byTooltip('Rotate'), findsOneWidget);
  });

  drawing('the chips start to the right of the strip', (tester) async {
    final strip = tester.getRect(tool(GizmoMode.move));
    final chip = tester.getRect(find.text('Perspective'));

    expect(chip.left, greaterThan(strip.right));
  });

  testWidgets('there is no strip where there is no renderer', (tester) async {
    await open(tester);

    expect(find.byTooltip('Move'), findsNothing);
    expect(find.byTooltip('Rotate'), findsNothing);
  });

  drawing(
    'the smallest window fits the strip and the chips',
    size: const Size(960, 620),
    (tester) async {
      expect(tester.takeException(), isNull);
      expect(find.byTooltip('Move'), findsOneWidget);
      expect(find.text('Perspective'), findsOneWidget);
    },
  );
}
