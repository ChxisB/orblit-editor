import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/editor/timeline_sheet.dart';
import 'package:orblit_motion/orblit_motion.dart' show ClipDocument;
import 'package:path/path.dart' as p;

import 'support/editor_shell.dart';

// A clip in the editor: opened from the project, keyed from the inspector,
// and saved along with everything else.

void main() {
  useShell();

  group('clips', () {
    File clipFile() => File(p.join(root.path, 'wave.oclip'));

    /// Opens the editor with a clip in the project, and opens the clip.
    Future<void> openClip(WidgetTester tester) async {
      clipFile().writeAsStringSync(
        ClipDocument(name: 'wave', duration: 2).encode(),
      );
      await open(tester);

      final tile = find.descendant(
        of: find.byType(GridView),
        matching: find.text('wave.oclip'),
      );
      await tester.tap(tile);
      await tester.pump(const Duration(milliseconds: 50));
      await tester.tap(tile);
      await tester.pumpAndSettle();
    }

    /// The key buttons in the inspector that nothing is keyed on yet.
    Finder unkeyed() => find.descendant(
      of: find.byType(Inspector),
      matching: find.byIcon(Icons.diamond_outlined),
    );

    testWidgets('opening one shows it on the timeline', (tester) async {
      await openClip(tester);
      expect(find.byType(TimelineRuler), findsOneWidget);
      expect(find.text('0.00 s   frame 0'), findsOneWidget);
    });

    testWidgets('a key from the inspector is saved with the scene', (
      tester,
    ) async {
      await openClip(tester);
      await tester.tap(row('Cube'));
      await tester.pumpAndSettle();

      // Position, rotation and scale, at least.
      expect(unkeyed(), findsWidgets);
      await tester.tap(unkeyed().first);
      await tester.pumpAndSettle();
      expect(
        find.descendant(
          of: find.byType(Inspector),
          matching: find.byIcon(Icons.diamond),
        ),
        findsOneWidget,
      );

      await save(tester);
      expect(clipFile().readAsStringSync(), contains('transform.position'));
    });
  });
}
