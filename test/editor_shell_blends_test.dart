import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/blend_graph_panel.dart';
import 'package:orblit_editor/src/widgets/controls.dart';
import 'package:orblit_motion/orblit_motion.dart'
    show BlendChange, BlendClip, BlendCondition, BlendDocument, BlendState;
import 'package:path/path.dart' as p;

import 'support/editor_shell.dart';

// The blend graph: a blend opened from Project, chosen from, changed through
// the undo stack, and written back as the file a game reads.

void main() {
  useShell();

  File blendFile(String name) =>
      File(p.join(root.path, 'blends', '$name.oblend'));

  /// Writes blends/moves.oblend, which goes from idle to move once speed is
  /// above a tenth.
  void writeMoves() {
    Directory(p.join(root.path, 'blends')).createSync();
    blendFile('moves').writeAsStringSync(
      BlendDocument(
        name: 'Moves',
        inputs: const {'speed': 0},
        states: [
          BlendState('idle', plays: const BlendClip('idle')),
          BlendState('move', plays: const BlendClip('walk')),
        ],
        changes: const [
          BlendChange(
            from: 'idle',
            to: 'move',
            when: BlendCondition.above('speed', 0.1),
          ),
        ],
      ).encode(),
    );
  }

  BlendDocument saved(String name) =>
      BlendDocument.decode(blendFile(name).readAsStringSync()).blend;

  Finder state(String name) => find.byKey(ValueKey('state/$name'));

  Finder inGraph(String text) => find.descendant(
    of: find.byType(BlendGraphPanel),
    matching: find.text(text),
  );

  /// Opens moves.oblend from Project, which takes a double tap.
  Future<void> openMoves(WidgetTester tester) async {
    await open(tester);
    await openFolder(tester, 'blends');
    final tile = find.descendant(
      of: find.byType(GridView),
      matching: find.text('moves.oblend'),
    );
    await tester.tap(tile);
    await tester.pump(const Duration(milliseconds: 50));
    await tester.tap(tile);
    await tester.pumpAndSettle();
  }

  /// A button of the graph's own, which the toolbar can share a word with.
  Future<void> tapButton(WidgetTester tester, String label) async {
    final button = find.descendant(
      of: find.byType(BlendGraphPanel),
      matching: find.widgetWithText(OrblitButton, label),
    );
    await tester.ensureVisible(button);
    await tester.tap(button);
    await tester.pumpAndSettle();
  }

  testWidgets('opening a blend from Project draws its graph', (tester) async {
    writeMoves();
    await openMoves(tester);

    expect(inMode(tester, 'animation'), isTrue);
    expect(dockTab('blendGraph'), findsOneWidget);
    expect(state('idle'), findsOneWidget);
    expect(state('move'), findsOneWidget);
    expect(find.byKey(const ValueKey('change/0')), findsOneWidget);
  });

  testWidgets('a file that is not a blend does not open the graph', (
    tester,
  ) async {
    Directory(p.join(root.path, 'blends')).createSync();
    blendFile('bad').writeAsStringSync('{"kind":"orblit.clip"}');
    await open(tester);
    await openFolder(tester, 'blends');
    final tile = find.descendant(
      of: find.byType(GridView),
      matching: find.text('bad.oblend'),
    );
    await tester.tap(tile);
    await tester.pump(const Duration(milliseconds: 50));
    await tester.tap(tile);
    await tester.pumpAndSettle();

    expect(dockTab('blendGraph'), findsNothing);
  });

  testWidgets('a choice of start state is a step, and is saved', (
    tester,
  ) async {
    writeMoves();
    await openMoves(tester);

    await tester.tap(state('move'));
    await tester.pumpAndSettle();
    await tapButton(tester, 'Start here');
    await save(tester);
    expect(saved('moves').writtenStart, 'move');

    await undo(tester);
    await save(tester);
    expect(saved('moves').writtenStart, 'idle');
  });

  testWidgets('dragging from a state onto another makes a change', (
    tester,
  ) async {
    writeMoves();
    await openMoves(tester);

    final handle = find.byKey(const ValueKey('link/move'));
    await tester.ensureVisible(handle);
    await tester.pumpAndSettle();
    final gesture = await tester.startGesture(tester.getCenter(handle));
    await tester.pump();
    await gesture.moveBy(const Offset(0, 24));
    await tester.pump();
    await gesture.moveTo(tester.getCenter(state('idle')));
    await tester.pump();
    await gesture.up();
    await tester.pumpAndSettle();

    await save(tester);
    final changes = saved('moves').writtenChanges;
    expect(changes.length, 2);
    expect((changes.last.from, changes.last.to), ('move', 'idle'));
  });

  testWidgets(
    'a change is chosen from its arrow and deleted from the inspector',
    (tester) async {
      writeMoves();
      await openMoves(tester);

      await tester.tap(find.byKey(const ValueKey('change/0')));
      await tester.pumpAndSettle();
      await tapButton(tester, 'Delete');
      await save(tester);
      expect(saved('moves').writtenChanges, isEmpty);

      await undo(tester);
      await save(tester);
      expect(saved('moves').writtenChanges.length, 1);
    },
  );

  testWidgets('the panel opened empty makes a blend', (tester) async {
    await open(tester);
    await viewMenu(tester, 'Blend');
    expect(dockTab('blendGraph'), findsOneWidget);

    await tapButton(tester, 'New blend');

    expect(blendFile('blend').existsSync(), isTrue);
    expect(state('idle'), findsOneWidget);
  });

  testWidgets('a blend with changes marks the tab until it is saved', (
    tester,
  ) async {
    writeMoves();
    await openMoves(tester);
    expect(inGraph('moves'), findsOneWidget);

    await tester.tap(state('move'));
    await tester.pumpAndSettle();
    await tapButton(tester, 'Start here');
    expect(inGraph('moves •'), findsOneWidget);

    await save(tester);
    expect(inGraph('moves'), findsOneWidget);
  });
}
