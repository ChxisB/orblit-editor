import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:path/path.dart' as p;

import 'support/editor_shell.dart';

// Dragging a mesh or a texture out of the browser and into the scene.

void main() {
  useShell();

  testWidgets('dragging a mesh out of the browser puts it in the scene', (
    tester,
  ) async {
    Directory(p.join(root.path, 'assets')).createSync();
    File(p.join(root.path, 'assets', 'crate.glb')).writeAsBytesSync([1, 2]);

    await open(tester);
    await openFolder(tester, 'assets');

    final tile = find.descendant(
      of: find.byType(GridView),
      matching: find.text('crate.glb'),
    );
    expect(tile, findsOneWidget);

    final gesture = await tester.startGesture(tester.getCenter(tile));
    await tester.pump(const Duration(milliseconds: 200));
    await gesture.moveTo(tester.getCenter(find.byType(SceneViewport)));
    await tester.pump();
    await gesture.up();
    await tester.pumpAndSettle();

    // Named after the file, and recorded as referencing it.
    expect(row('crate'), findsOneWidget);
    expect(find.textContaining('Add crate'), findsOneWidget);
  });

  Future<void> dropTexture(WidgetTester tester) async {
    Directory(p.join(root.path, 'assets')).createSync();
    File(p.join(root.path, 'assets', 'rock.png')).writeAsBytesSync([1]);

    await open(tester);
    await openFolder(tester, 'assets');

    final tile = find.descendant(
      of: find.byType(GridView),
      matching: find.text('rock.png'),
    );
    final gesture = await tester.startGesture(tester.getCenter(tile));
    await tester.pump(const Duration(milliseconds: 200));
    await gesture.moveTo(tester.getCenter(find.byType(SceneViewport)));
    await tester.pump();
    await gesture.up();
    await tester.pumpAndSettle();
  }

  testWidgets('a texture dragged in with nothing selected says what to do', (
    tester,
  ) async {
    await dropTexture(tester);

    expect(find.textContaining('Select an object first'), findsOneWidget);
    // And nothing was added: a texture is not a thing that stands on its own.
    expect(row('rock'), findsNothing);
  });

  testWidgets('a texture dragged in goes on the selected object', (
    tester,
  ) async {
    Directory(p.join(root.path, 'assets')).createSync();
    File(p.join(root.path, 'assets', 'rock.png')).writeAsBytesSync([1]);

    await open(tester);
    await tester.tap(row('Crate'));
    await tester.pumpAndSettle();
    await openFolder(tester, 'assets');

    final tile = find.descendant(
      of: find.byType(GridView),
      matching: find.text('rock.png'),
    );
    final gesture = await tester.startGesture(tester.getCenter(tile));
    await tester.pump(const Duration(milliseconds: 200));
    await gesture.moveTo(tester.getCenter(find.byType(SceneViewport)));
    await tester.pump();
    await gesture.up();
    await tester.pumpAndSettle();

    // The inspector, still on the crate, now names the texture.
    expect(find.text('Texture'), findsOneWidget);
    expect(find.textContaining('rock.png'), findsWidgets);
    expect(find.textContaining('Select an object first'), findsNothing);
  });

  testWidgets('F frames the selection', (tester) async {
    await open(tester);
    await tester.tap(row('Ground'));
    await tester.pumpAndSettle();

    final before = tester
        .widget<SceneViewport>(find.byType(SceneViewport))
        .camera;

    await tester.sendKeyEvent(LogicalKeyboardKey.keyF);
    await tester.pumpAndSettle();

    final after = tester
        .widget<SceneViewport>(find.byType(SceneViewport))
        .camera;

    // The ground is wide, so framing it pulls the camera back.
    expect(after.distance, isNot(before.distance));
    expect(
      after.yaw,
      before.yaw,
      reason: 'framing should not change the angle',
    );
  });
}
