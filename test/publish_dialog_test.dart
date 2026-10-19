import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/store/publish_dialog.dart';
import 'package:orblit_editor/src/store/publish_session.dart';
import 'package:orblit_editor/src/theme/orblit_theme.dart';
import 'package:orblit_editor/src/widgets/controls.dart';
import 'package:path/path.dart' as p;

import 'support/kit_folder.dart';

// The publish dialog over a real temporary project. These are widget tests:
// nothing here ran in a window.

late Directory project;

Future<void> prepare(
  WidgetTester tester, {
  Size size = const Size(1440, 900),
}) async {
  await tester.binding.setSurfaceSize(size);
  addTearDown(() => tester.binding.setSurfaceSize(null));
  project = Directory.systemTemp.createTempSync('orblit_publish_dialog_');
  addTearDown(() => project.deleteSync(recursive: true));
  writeKitFolder(p.join(project.path, 'assets/kits/crates'));
  await tester.pumpWidget(
    MaterialApp(
      theme: orblitTheme(),
      home: Scaffold(
        body: Builder(
          builder: (context) => TextButton(
            onPressed: () => showPublishDialog(
              context,
              PublishSession(
                projectDirectory: project.path,
                clock: () => DateTime.utc(2026, 10, 19),
              ),
            ),
            child: const Text('open'),
          ),
        ),
      ),
    ),
  );
  await tester.tap(find.text('open'));
  await tester.pumpAndSettle();
}

const String urlHint = 'https://example.com/crates-1.0.0.opack';

Finder field(String hint) => find.byWidgetPredicate(
  (widget) => widget is TextField && widget.decoration?.hintText == hint,
);

Future<void> fillIn(WidgetTester tester, {String url = hostedAt}) async {
  await tester.enterText(field('assets/kits/crates'), 'assets/kits/crates');
  await tester.enterText(field(urlHint), url);
  await tester.enterText(field('What changed.'), 'First release.');
}

Finder publishButton() => find.widgetWithText(OrblitButton, 'Publish');

/// Waits in real time for work on the disk, which a pump does not wait for.
Future<void> until(WidgetTester tester, Finder finder) async {
  for (var i = 0; i < 300 && finder.evaluate().isEmpty; i++) {
    await tester.runAsync(
      () => Future<void>.delayed(const Duration(milliseconds: 10)),
    );
    await tester.pump();
  }
  expect(finder, findsWidgets);
}

void main() {
  testWidgets('packs a folder and shows the entry to copy', (tester) async {
    await prepare(tester);
    await fillIn(tester);
    await tester.tap(publishButton());
    await until(tester, find.textContaining('Packed crates-1.0.0.opack'));

    expect(find.textContaining('"version": "1.0.0"'), findsOneWidget);
    expect(find.textContaining('"released": "2026-10-19"'), findsOneWidget);
    expect(find.text('ENTRY FOR BRASS-LAMP/CRATES'), findsOneWidget);
    expect(find.textContaining('Upload the file'), findsOneWidget);
    expect(
      File(p.join(project.path, 'exports/kits/crates-1.0.0.opack'))
          .existsSync(),
      isTrue,
    );
  });

  testWidgets('copies the entry to the clipboard', (tester) async {
    await prepare(tester);
    String? copied;
    tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
      SystemChannels.platform,
      (call) async {
        if (call.method == 'Clipboard.setData') {
          copied = (call.arguments as Map)['text'] as String;
        }
        return null;
      },
    );
    addTearDown(
      () => tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
        SystemChannels.platform,
        null,
      ),
    );
    await fillIn(tester);
    await tester.tap(publishButton());
    await until(tester, find.text('Copy'));

    await tester.tap(find.text('Copy'));
    await tester.pump();
    expect(copied, contains('"url": "$hostedAt"'));
    expect(find.text('Copied'), findsOneWidget);
  });

  testWidgets('says what is wrong and keeps what was typed', (tester) async {
    await prepare(tester);
    await fillIn(tester, url: 'https://example.com/');
    await tester.tap(publishButton());
    await until(tester, find.textContaining('name of the kit file'));

    String typed(String hint) =>
        tester.widget<TextField>(field(hint)).controller!.text;
    expect(typed('assets/kits/crates'), 'assets/kits/crates');
    expect(typed('What changed.'), 'First release.');
    expect(tester.widget<OrblitButton>(publishButton()).onPressed, isNotNull);
    expect(Directory(p.join(project.path, 'exports')).existsSync(), isFalse);
  });

  testWidgets('fits the smallest window the editor allows', (tester) async {
    await prepare(tester, size: const Size(960, 620));
    await fillIn(tester, url: 'https://example.com/');
    await tester.tap(publishButton());
    await until(tester, find.textContaining('name of the kit file'));
    expect(tester.takeException(), isNull);

    await tester.enterText(field(urlHint), hostedAt);
    await tester.tap(publishButton());
    await until(tester, find.textContaining('Packed crates-1.0.0.opack'));
    expect(tester.takeException(), isNull);
  });

  testWidgets('keeps the button in view when a kit has many problems', (
    tester,
  ) async {
    await prepare(tester, size: const Size(960, 620));
    for (var i = 0; i < 12; i++) {
      File(p.join(project.path, 'assets/kits/crates', 'd$i', '.DS_Store'))
        ..parent.createSync(recursive: true)
        ..writeAsStringSync('x');
    }
    await fillIn(tester);
    await tester.tap(publishButton());
    await until(tester, find.textContaining('.DS_Store'));
    expect(tester.takeException(), isNull);
    expect(tester.getRect(publishButton()).bottom, lessThanOrEqualTo(620));
  });
}
