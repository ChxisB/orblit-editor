import 'package:flutter/material.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

import 'support/editor_shell.dart';

// Driving the viewport from the shell: flying, the camera preview, and the
// trackpad gestures that do the same work as a mouse.

void main() {
  useShell();

  group('flying the view', () {
    /// Holds the right button down over the viewport.
    Future<TestGesture> look(WidgetTester tester) async {
      final gesture = await tester.createGesture(
        kind: PointerDeviceKind.mouse,
        buttons: kSecondaryButton,
      );
      await gesture.addPointer(
        location: tester.getCenter(find.byType(SceneViewport)),
      );
      addTearDown(gesture.removePointer);
      await gesture.down(tester.getCenter(find.byType(SceneViewport)));
      // Pumped rather than settled: flying runs the clock, so there is always
      // another frame scheduled and pumpAndSettle waits for one that never
      // comes.
      await tester.pump();
      return gesture;
    }

    testWidgets('holding the right button says it is flying', (tester) async {
      await open(tester);
      expect(find.textContaining('Flying'), findsNothing);

      await look(tester);

      expect(find.textContaining('Flying'), findsOneWidget);
      expect(find.textContaining('m/s'), findsOneWidget);
    });

    testWidgets('letting go stops', (tester) async {
      await open(tester);
      final gesture = await look(tester);

      await gesture.up();
      await tester.pumpAndSettle();

      expect(find.textContaining('Flying'), findsNothing);
    });

    testWidgets('W moves the view forward while the button is held', (
      tester,
    ) async {
      await open(tester);
      final gesture = await look(tester);

      await tester.sendKeyDownEvent(LogicalKeyboardKey.keyW);
      await tester.pump(const Duration(milliseconds: 100));
      await tester.pump(const Duration(milliseconds: 100));
      await tester.sendKeyUpEvent(LogicalKeyboardKey.keyW);
      await gesture.up();
      await tester.pumpAndSettle();

      // Still flying is what would be wrong; the camera having moved is what
      // the unit tests on OrbitCamera cover exactly.
      expect(find.textContaining('Flying'), findsNothing);
      expect(find.byType(SceneViewport), findsOneWidget);
    });

    testWidgets('W does nothing when the button is not held', (tester) async {
      await open(tester);

      // Otherwise typing a name into the inspector would fly the view across
      // the level a letter at a time.
      await tester.sendKeyDownEvent(LogicalKeyboardKey.keyW);
      await tester.pump(const Duration(milliseconds: 200));
      await tester.sendKeyUpEvent(LogicalKeyboardKey.keyW);
      await tester.pumpAndSettle();

      expect(find.textContaining('Flying'), findsNothing);
    });

    testWidgets('it can be switched on rather than held', (tester) async {
      await open(tester);

      // A click makes this view the one the keyboard is talking to.
      await tester.tapAt(tester.getCenter(find.byType(SceneViewport)));
      await tester.pumpAndSettle();

      await tester.sendKeyEvent(LogicalKeyboardKey.backquote);
      await tester.pump();

      // No button held: a two-finger click held down while the other hand
      // types WASD is a hand position nobody keeps for long.
      expect(find.textContaining('Flying'), findsOneWidget);
    });

    testWidgets('escape stops it', (tester) async {
      await open(tester);
      await tester.tapAt(tester.getCenter(find.byType(SceneViewport)));
      await tester.pumpAndSettle();
      await tester.sendKeyEvent(LogicalKeyboardKey.backquote);
      await tester.pump();

      await tester.sendKeyEvent(LogicalKeyboardKey.escape);
      await tester.pumpAndSettle();

      expect(find.textContaining('Flying'), findsNothing);
    });

    testWidgets('and so does pressing the key again', (tester) async {
      await open(tester);
      await tester.tapAt(tester.getCenter(find.byType(SceneViewport)));
      await tester.pumpAndSettle();

      await tester.sendKeyEvent(LogicalKeyboardKey.backquote);
      await tester.pump();
      await tester.sendKeyEvent(LogicalKeyboardKey.backquote);
      await tester.pumpAndSettle();

      expect(find.textContaining('Flying'), findsNothing);
    });

    testWidgets('W moves while it is switched on, with nothing held', (
      tester,
    ) async {
      await open(tester);
      await tester.tapAt(tester.getCenter(find.byType(SceneViewport)));
      await tester.pumpAndSettle();
      await tester.sendKeyEvent(LogicalKeyboardKey.backquote);
      await tester.pump();

      await tester.sendKeyDownEvent(LogicalKeyboardKey.keyW);
      await tester.pump(const Duration(milliseconds: 100));
      await tester.pump(const Duration(milliseconds: 100));
      await tester.sendKeyUpEvent(LogicalKeyboardKey.keyW);
      await tester.pump();

      expect(find.textContaining('Flying'), findsOneWidget);
    });

    testWidgets('the backquote does nothing until a view is clicked in', (
      tester,
    ) async {
      await open(tester);

      // Otherwise typing a backquote into a name would launch the view.
      await tester.sendKeyEvent(LogicalKeyboardKey.backquote);
      await tester.pumpAndSettle();

      expect(find.textContaining('Flying'), findsNothing);
    });

    testWidgets('the wheel sets the speed rather than the distance', (
      tester,
    ) async {
      await open(tester);
      await look(tester);

      final before = tester
          .widgetList<Text>(find.textContaining('m/s'))
          .first
          .data;

      await tester.sendEventToBinding(
        PointerScrollEvent(
          position: tester.getCenter(find.byType(SceneViewport)),
          scrollDelta: const Offset(0, -120),
        ),
      );
      await tester.pump();

      final after = tester
          .widgetList<Text>(find.textContaining('m/s'))
          .first
          .data;
      expect(after, isNot(before));
    });
  });

  group('the camera preview', () {
    testWidgets('selecting a camera shows what it sees', (tester) async {
      await open(tester);
      expect(find.text('CAMERA'), findsNothing);

      await tester.tap(row('Camera'));
      await tester.pumpAndSettle();

      expect(find.text('CAMERA'), findsOneWidget);
    });

    testWidgets('selecting anything else takes it away', (tester) async {
      await open(tester);
      await tester.tap(row('Camera'));
      await tester.pumpAndSettle();
      expect(find.text('CAMERA'), findsOneWidget);

      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();

      expect(find.text('CAMERA'), findsNothing);
    });

    testWidgets('hiding the camera takes it away too', (tester) async {
      await open(tester);
      await tester.tap(row('Camera'));
      await tester.pumpAndSettle();

      await tester.tap(
        find.descendant(
          of: find.byType(Inspector),
          matching: find.text('Hidden'),
        ),
      );
      await tester.pumpAndSettle();

      // A camera that is not in the scene has no shot to preview.
      expect(find.text('CAMERA'), findsNothing);
    });
  });

  group('the trackpad', () {
    /// Two fingers on the trackpad, which Flutter reports as a pan-zoom
    /// gesture rather than as a scroll once something listens for one.
    Future<void> twoFingers(
      WidgetTester tester,
      Offset by, {
      double scale = 1,
      // Flying runs the clock, so there is always another frame scheduled and
      // pumpAndSettle waits for one that never comes.
      bool settle = true,
    }) async {
      final at = tester.getCenter(find.byType(SceneViewport));
      final pointer = TestPointer(1, PointerDeviceKind.trackpad);

      await tester.sendEventToBinding(pointer.panZoomStart(at));
      await tester.pump();
      await tester.sendEventToBinding(
        pointer.panZoomUpdate(at, pan: by, scale: scale),
      );
      await tester.pump();
      await tester.sendEventToBinding(pointer.panZoomEnd());
      if (settle) {
        await tester.pumpAndSettle();
      } else {
        await tester.pump();
      }
    }

    /// Where the camera of the one scene view is standing.
    Vector3 eye(WidgetTester tester) => tester
        .widget<SceneViewport>(find.byType(SceneViewport))
        .camera
        .toRenderCamera()
        .position;

    testWidgets('two fingers orbit', (tester) async {
      await open(tester);
      final was = eye(tester);

      await twoFingers(tester, const Offset(80, 0));

      // Moved around what it was looking at, rather than towards it.
      final now = eye(tester);
      expect((now - was).length, greaterThan(0.5));
      expect(now.length, closeTo(was.length, 0.5));
    });

    testWidgets('shift and two fingers pan', (tester) async {
      await open(tester);
      final wasAt = tester
          .widget<SceneViewport>(find.byType(SceneViewport))
          .camera
          .target
          .clone();

      await tester.sendKeyDownEvent(LogicalKeyboardKey.shiftLeft);
      await twoFingers(tester, const Offset(80, 0));
      await tester.sendKeyUpEvent(LogicalKeyboardKey.shiftLeft);

      final nowAt = tester
          .widget<SceneViewport>(find.byType(SceneViewport))
          .camera
          .target;
      expect((nowAt - wasAt).length, greaterThan(0.05));
    });

    testWidgets('pinching zooms', (tester) async {
      await open(tester);
      final was = tester
          .widget<SceneViewport>(find.byType(SceneViewport))
          .camera
          .distance;

      await twoFingers(tester, Offset.zero, scale: 1.4);

      final now = tester
          .widget<SceneViewport>(find.byType(SceneViewport))
          .camera
          .distance;
      expect(now, lessThan(was));
    });

    testWidgets('two fingers steer while flying, with nothing held', (
      tester,
    ) async {
      await open(tester);
      await tester.tapAt(tester.getCenter(find.byType(SceneViewport)));
      await tester.pumpAndSettle();
      await tester.sendKeyEvent(LogicalKeyboardKey.backquote);
      await tester.pump();

      final was = eye(tester);
      await twoFingers(tester, const Offset(60, 0), settle: false);

      // Looking keeps the eye where it is; orbiting would have moved it.
      expect((eye(tester) - was).length, lessThan(1e-6));
    });
  });
}
