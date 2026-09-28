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
      // In the Animation workspace, where the timeline has room.
      expect(inMode(tester, 'animation'), isTrue);
      expect(find.byType(TimelineRuler), findsOneWidget);
      expect(find.text('0.00 s   frame 0'), findsOneWidget);
    });

    testWidgets('an empty timeline makes one and shows it', (tester) async {
      await open(tester);
      await enterMode(tester, 'animation');

      await tester.tap(find.text('Make a clip'));
      await tester.pumpAndSettle();

      expect(
        File(p.join(root.path, 'clips', 'clip.oclip')).existsSync(),
        isTrue,
      );
      expect(find.byType(TimelineRuler), findsOneWidget);
    });

    testWidgets('the clip menu makes another', (tester) async {
      await openClip(tester);

      await tester.tap(find.text('wave'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Make a clip'));
      await tester.pumpAndSettle();

      expect(
        File(p.join(root.path, 'clips', 'clip.oclip')).existsSync(),
        isTrue,
      );
      // Shown in place of the one before.
      expect(find.text('clip'), findsOneWidget);
      expect(find.text('wave'), findsNothing);
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
