import 'dart:io';

import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/cinematics_mode.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_editor/src/widgets/controls.dart';
import 'package:orblit_motion/orblit_motion.dart'
    show ClipDocument, CutsceneDocument, CutsceneShot;
import 'package:path/path.dart' as p;
import 'package:vector_math/vector_math_64.dart' hide Colors;

import 'support/editor_shell.dart';

// The Cinematics workspace: cutscenes made and opened there, shots cut to
// the scene's cameras, and a camera framed by steering the scene view.

void main() {
  useShell();

  File cutsceneFile(String name) =>
      File(p.join(root.path, 'cutscenes', '$name.ocutscene'));

  /// Writes cutscenes/intro.ocutscene with [shots], all through the starter
  /// scene's camera.
  void writeIntro(List<(double, double)> shots) {
    Directory(p.join(root.path, 'cutscenes')).createSync();
    cutsceneFile('intro').writeAsStringSync(
      CutsceneDocument(
        motion: ClipDocument(name: 'intro', duration: 4),
        shots: [
          for (final (start, duration) in shots)
            CutsceneShot(camera: 'camera', start: start, duration: duration),
        ],
      ).encode(),
    );
  }

  /// Each saved shot as camera, start and end.
  List<(String, double, double)> savedShots(String name) => [
    for (final shot in CutsceneDocument.decode(
      cutsceneFile(name).readAsStringSync(),
    ).cutscene.shots)
      (shot.camera, shot.start, shot.end),
  ];

  Finder inShots(String text) =>
      find.descendant(of: find.byType(ShotList), matching: find.text(text));

  /// The card of the shot numbered [number], by its title, which a section
  /// draws in capitals.
  Finder card(int number) => inShots('Shot $number');

  Finder onShelf(String label) => find.descendant(
    of: find.byType(CinematicsShelf),
    matching: find.text(label),
  );

  bool offered(WidgetTester tester, String label) =>
      tester
          .widget<OrblitButton>(
            find.ancestor(
              of: onShelf(label),
              matching: find.byType(OrblitButton),
            ),
          )
          .onPressed !=
      null;

  Future<void> tapShelf(WidgetTester tester, String label) async {
    await tester.tap(onShelf(label));
    await tester.pumpAndSettle();
  }

  Future<void> tapInShots(WidgetTester tester, String label) async {
    await tester.tap(inShots(label));
    await tester.pumpAndSettle();
  }

  /// Opens the editor in Cinematics with a new cutscene open.
  Future<void> newCutscene(WidgetTester tester) async {
    await open(tester);
    await enterMode(tester, 'cinematics');
    await tapShelf(tester, 'New cutscene');
  }

  /// Opens intro.ocutscene from Project, which takes a double tap.
  Future<void> openIntro(WidgetTester tester) async {
    await open(tester);
    await openFolder(tester, 'cutscenes');
    final tile = find.descendant(
      of: find.byType(GridView),
      matching: find.text('intro.ocutscene'),
    );
    await tester.tap(tile);
    await tester.pump(const Duration(milliseconds: 50));
    await tester.tap(tile);
    await tester.pumpAndSettle();
  }

  /// Flies the scene view forward for a moment, with the right button held.
  Future<void> flyForward(WidgetTester tester) async {
    final centre = tester.getCenter(find.byType(SceneViewport));
    final gesture = await tester.createGesture(
      kind: PointerDeviceKind.mouse,
      buttons: kSecondaryButton,
    );
    await gesture.addPointer(location: centre);
    addTearDown(gesture.removePointer);
    await gesture.down(centre);
    // Pumped rather than settled, since flying keeps asking for frames.
    await tester.pump();
    await tester.sendKeyDownEvent(LogicalKeyboardKey.keyW);
    await tester.pump(const Duration(milliseconds: 100));
    await tester.pump(const Duration(milliseconds: 100));
    await tester.sendKeyUpEvent(LogicalKeyboardKey.keyW);
    await gesture.up();
    await tester.pumpAndSettle();
  }

  testWidgets('with nothing open it offers to make one', (tester) async {
    await open(tester);
    await enterMode(tester, 'cinematics');
    expect(find.textContaining('Choose New cutscene'), findsOneWidget);
    // A shot belongs to a cutscene.
    expect(offered(tester, 'Add shot'), isFalse);
    expect(offered(tester, 'Use this view'), isFalse);

    await tapShelf(tester, 'New cutscene');

    expect(cutsceneFile('cutscene').existsSync(), isTrue);
    expect(find.textContaining('No shots yet'), findsOneWidget);
    expect(offered(tester, 'Add shot'), isTrue);
    expect(dockTab('cutsceneTimeline'), findsOneWidget);
  });

  testWidgets('opening a cutscene from Project goes there', (tester) async {
    writeIntro([(0, 4)]);
    await openIntro(tester);

    expect(inMode(tester, 'cinematics'), isTrue);
    expect(card(1), findsOneWidget);
    expect(dockTab('shot'), findsOneWidget);
  });

  testWidgets('Add shot cuts to the camera, and the shot is saved', (
    tester,
  ) async {
    await newCutscene(tester);
    await tapShelf(tester, 'Add shot');
    expect(card(1), findsOneWidget);

    await save(tester);
    // The starter scene's camera, from the playhead to the end.
    expect(savedShots('cutscene'), [('camera', 0, 4)]);

    await tapInShots(tester, 'Delete');
    expect(card(1), findsNothing);
    await undo(tester);
    expect(card(1), findsOneWidget);
  });

  testWidgets('Add shot with no camera says how to get one', (tester) async {
    await open(tester);
    await tester.tap(row('Camera'));
    await tester.pumpAndSettle();
    await tester.sendKeyEvent(LogicalKeyboardKey.delete);
    await tester.pumpAndSettle();
    expect(row('Camera'), findsNothing);

    await enterMode(tester, 'cinematics');
    await tapShelf(tester, 'New cutscene');
    await tapShelf(tester, 'Add shot');

    expect(
      find.text('Add a camera to the scene first, or choose Use this view.'),
      findsOneWidget,
    );
    expect(card(1), findsNothing);
  });

  testWidgets('Use this view adds a camera and cuts to it', (tester) async {
    await newCutscene(tester);
    await tapShelf(tester, 'Use this view');

    expect(card(1), findsOneWidget);
    // Beside the starter scene's own.
    expect(inShots('Camera 2'), findsOneWidget);

    // The camera and the shot are a step each.
    await undo(tester);
    expect(card(1), findsNothing);
    await undo(tester);
    await enterMode(tester, 'scene');
    expect(row('Camera 2'), findsNothing);
    expect(row('Camera'), findsOneWidget);
  });

  testWidgets('Look through steers the camera from the scene view', (
    tester,
  ) async {
    await newCutscene(tester);
    await tapShelf(tester, 'Add shot');
    await tapInShots(tester, 'Look through');
    expect(inShots('Stop looking'), findsOneWidget);

    await flyForward(tester);
    await save(tester);

    final camera = SceneDocument.decode(
      File(p.join(root.path, 'scenes', 'main$sceneExtension'))
          .readAsStringSync(),
    ).scene['camera']!;
    expect(camera.position.distanceTo(Vector3(6, 4, 8)), greaterThan(0.1));
    // Moved, not turned, and the angles read as they were typed.
    expect(camera.rotation.x, closeTo(-20, 1e-6));
    expect(camera.rotation.y, closeTo(35, 1e-6));
    expect(camera.rotation.z, 0);

    // Leaving the workspace lets go of the camera.
    await enterMode(tester, 'scene');
    await enterMode(tester, 'cinematics');
    expect(inShots('Look through'), findsOneWidget);
  });

  testWidgets('a start typed past the next shot moves its card along', (
    tester,
  ) async {
    writeIntro([(0, 2), (2, 2)]);
    await openIntro(tester);

    Finder fieldsOf(int number) => find.descendant(
      of: find.ancestor(of: card(number), matching: find.byType(OrblitSection)),
      matching: find.byType(EditableText),
    );
    final start = fieldsOf(1).first;
    await tester.tap(start);
    await tester.pump();
    await tester.enterText(start, '3');
    await tester.pumpAndSettle();

    // Now the second shot, and still the field being typed in.
    final typing = tester.widget<EditableText>(fieldsOf(2).first);
    expect(typing.controller.text, '3');
    expect(typing.focusNode.hasFocus, isTrue);

    // Saved from the keyboard straight after Enter, which has to leave the
    // shell's shortcuts listening.
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await tester.pumpAndSettle();
    await save(tester);
    expect(savedShots('intro'), [('camera', 2, 4), ('camera', 3, 5)]);
  });
}
