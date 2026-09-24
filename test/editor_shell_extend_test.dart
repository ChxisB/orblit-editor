import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/console_panel.dart';
import 'package:orblit_editor/src/editor/dock.dart';
import 'package:orblit_editor/src/editor/editor_mode.dart';
import 'package:orblit_editor/src/editor/editor_shell.dart';
import 'package:orblit_editor/src/editor/gizmo_registry.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/editor/outliner.dart';
import 'package:orblit_editor/src/editor/panel_registry.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_editor/src/editor/viewport_input.dart';
import 'package:orblit_editor/src/widgets/controls.dart';
import 'package:path/path.dart' as p;

import 'support/editor_shell.dart';

// What something outside the editor can add to it — a panel, an inspector
// section, a gizmo, a mode — each arriving the way the built-in ones do.

const _notes = PanelKind('notes', 'Notes', Icons.sticky_note_2_outlined);

void _addNotes(EditorRegistry registry) => registry.panels.register(
  PanelType(kind: _notes, build: (_, _) => const Text('what the notes say')),
);

void main() {
  useShell();

  group('from outside the editor', () {
    testWidgets('a panel opens from the View menu and comes back', (
      tester,
    ) async {
      await open(tester, extend: _addNotes);
      expect(find.text('what the notes say'), findsNothing);

      await viewMenu(tester, 'Notes');
      expect(find.text('what the notes say'), findsOneWidget);

      // Saved with the rest of the layout, and put back with it.
      await tester.pumpWidget(const SizedBox());
      await open(tester, extend: _addNotes);
      expect(find.text('what the notes say'), findsOneWidget);
    });

    testWidgets('without what registered it, the rest of the layout opens', (
      tester,
    ) async {
      await open(tester, extend: _addNotes);
      await viewMenu(tester, 'Notes');

      await tester.pumpWidget(const SizedBox());
      await open(tester);

      expect(find.text('what the notes say'), findsNothing);
      expect(find.byType(Outliner), findsOneWidget);
      expect(find.byType(SceneViewport), findsOneWidget);
    });

    testWidgets('an inspector section shows where it asked to, for what it '
        'applies to', (tester) async {
      await open(
        tester,
        extend: (registry) => registry.sections.register(
          InspectorSection(
            name: 'glow',
            appliesTo: (target) => target.object.kind == ObjectKind.light,
            build: (target) => Text('the glow of ${target.object.name}'),
          ),
          before: 'transform',
        ),
      );

      await add(tester, 'Light');
      final glow = find.textContaining('the glow of');
      expect(glow, findsOneWidget);
      expect(
        tester.getTopLeft(glow).dy,
        lessThan(
          tester
              .getTopLeft(
                find.descendant(
                  of: find.byType(Inspector),
                  matching: find.text('Position'),
                ),
              )
              .dy,
        ),
      );

      await add(tester, 'Group');
      expect(glow, findsNothing);
    });

    testWidgets('a gizmo is drawn in the scene view for what it applies to', (
      tester,
    ) async {
      await open(
        tester,
        extend: (registry) => registry.gizmos.register(
          GizmoType(
            name: 'reach',
            appliesTo: (target) => target.object.kind == ObjectKind.light,
            overlay: (target) => Positioned(
              left: 0,
              top: 0,
              child: Text('the reach of ${target.object.name}'),
            ),
          ),
        ),
      );
      final reach = find.descendant(
        of: find.byType(SceneViewport),
        matching: find.textContaining('the reach of'),
      );

      await add(tester, 'Group');
      expect(reach, findsNothing);

      await add(tester, 'Light');
      expect(reach, findsOneWidget);
    });
  });

  group('modes', () {
    Finder modeButton(IconData icon) => find.byWidgetPredicate(
      (widget) => widget is OrblitButton && widget.icon == icon,
    );

    Future<void> enter(WidgetTester tester, IconData icon) async {
      await tester.tap(modeButton(icon));
      await tester.pumpAndSettle();
    }

    testWidgets("the editor's own are the scene and the terrain", (
      tester,
    ) async {
      await open(tester);
      expect(modeButton(Icons.open_with), findsOneWidget);
      expect(modeButton(Icons.landscape_outlined), findsOneWidget);
    });

    testWidgets('a second brings its own panels, its tools and the first say '
        'over the pointer', (tester) async {
      final phases = <ViewportPhase>[];
      await open(
        tester,
        extend: (registry) => registry.modes.register(
          EditorMode(
            name: 'ground',
            label: 'Ground',
            icon: Icons.terrain,
            layout: () => const DockLayout(
              root: DockGroup(
                id: 'only',
                panels: [DockPanel(id: 'scene', kind: PanelKind.viewport)],
              ),
            ),
            tools: (_) => const Text('ground tools'),
            input: (gesture) {
              phases.add(gesture.phase);
              return gesture.phase == ViewportPhase.tap;
            },
          ),
        ),
      );

      // It opens in the scene, as it always has.
      expect(modeButton(Icons.open_with), findsOneWidget);
      expect(find.text('ground tools'), findsNothing);
      expect(find.byType(Outliner), findsOneWidget);

      await enter(tester, Icons.terrain);
      expect(find.text('ground tools'), findsOneWidget);
      expect(find.byType(Outliner), findsNothing);

      await tester.tapAt(tester.getCenter(find.byType(SceneViewport)));
      await tester.pumpAndSettle();
      expect(phases, contains(ViewportPhase.tap));

      // Arranged in this mode, kept for this mode.
      await viewMenu(tester, 'Console');
      expect(
        File(
          p.join(root.path, '.orblit', 'layout.ground.json'),
        ).readAsStringSync(),
        contains('"console"'),
      );

      await enter(tester, Icons.open_with);
      expect(find.text('ground tools'), findsNothing);
      expect(find.byType(Outliner), findsOneWidget);
      expect(find.byType(ConsolePanel), findsNothing);

      // And the pointer is the scene's again.
      phases.clear();
      await tester.tapAt(tester.getCenter(find.byType(SceneViewport)));
      await tester.pumpAndSettle();
      expect(phases, isEmpty);

      await enter(tester, Icons.terrain);
      expect(find.byType(ConsolePanel), findsOneWidget);
    });
  });
}
