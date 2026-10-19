import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_editor/src/store/install_session.dart';
import 'package:orblit_editor/src/store/kit_install_dialog.dart';
import 'package:orblit_editor/src/store/store_dialog.dart';
import 'package:orblit_editor/src/store/store_session.dart';
import 'package:orblit_editor/src/store/store_settings.dart';
import 'package:orblit_editor/src/theme/orblit_theme.dart';
import 'package:orblit_editor/src/widgets/controls.dart';
import 'package:path/path.dart' as p;

import 'support/store_fixture.dart';

// The store and install dialogs on a fake network and a real temporary
// project. These are widget tests: nothing here ran in a window.

const String into = 'assets/kits/crates';

late Directory scratch;
late StoreFixture fixture;
late String project;
late List<MapTransport> made;

String inProject(String name) => p.join(project, name);

InstallSession installFor(KitRow row, KitRelease release) => InstallSession(
  row: row,
  release: release,
  projectDirectory: project,
  downloads: p.join(scratch.path, 'downloads'),
  transport: fixture.transport(),
  backoff: Duration.zero,
);

/// A project with a file of the kit's already in it, and a store with the
/// kit.
Future<void> prepare(
  WidgetTester tester, {
  Size size = const Size(1440, 900),
}) async {
  await tester.binding.setSurfaceSize(size);
  addTearDown(() => tester.binding.setSurfaceSize(null));
  scratch = Directory.systemTemp.createTempSync('orblit_store_dialog_');
  addTearDown(() => scratch.deleteSync(recursive: true));
  fixture = (await tester.runAsync(() => StoreFixture.build(scratch)))!;
  project = p.join(scratch.path, 'project');
  File(inProject('$into/models/crate.obj'))
    ..parent.createSync(recursive: true)
    ..writeAsStringSync('my own crate');
  made = [];
}

Future<void> showOver(WidgetTester tester, WidgetBuilder dialog) async {
  await tester.pumpWidget(
    MaterialApp(
      theme: orblitTheme(),
      home: Scaffold(
        body: Builder(
          builder: (context) => TextButton(
            onPressed: () =>
                showDialog<void>(context: context, builder: dialog),
            child: const Text('open'),
          ),
        ),
      ),
    ),
  );
  await tester.tap(find.text('open'));
  await tester.pumpAndSettle();
}

Future<void> openStore(WidgetTester tester, {String engine = '0.35.0'}) {
  final session = StoreSession(
    settings: StoreSettings(File(p.join(scratch.path, 'store.json'))),
    engine: engine,
    transport: () {
      final transport = fixture.transport();
      made.add(transport);
      return transport;
    },
  );
  return showOver(
    tester,
    (_) => StoreDialog(session: session, installFor: installFor),
  );
}

/// An install dialog over a kit that is downloaded and checked.
Future<void> openInstall(WidgetTester tester) async {
  final (row, release) = (await tester.runAsync(fixture.listing))!;
  final session = installFor(row, release);
  await tester.runAsync(session.start);
  await showOver(tester, (_) => InstallDialog(session: session));
}

/// Waits in real time for work on the disk, which a pump does not wait for.
///
/// The pump moves the app's clock too, because a ranged read of a kit file
/// waits a few milliseconds of it to join its requests.
Future<void> until(WidgetTester tester, Finder finder) async {
  for (var i = 0; i < 300 && finder.evaluate().isEmpty; i++) {
    await tester.runAsync(
      () => Future<void>.delayed(const Duration(milliseconds: 10)),
    );
    await tester.pump(const Duration(milliseconds: 10));
  }
  expect(finder, findsWidgets);
}

Finder installButton() => find.widgetWithText(OrblitButton, 'Install');

bool installable(WidgetTester tester) =>
    tester.widget<OrblitButton>(installButton()).onPressed != null;

Future<void> untilInstallable(WidgetTester tester) async {
  for (var i = 0; i < 300 && !installable(tester); i++) {
    await tester.runAsync(
      () => Future<void>.delayed(const Duration(milliseconds: 10)),
    );
    await tester.pump();
  }
  expect(installable(tester), isTrue);
}

List<bool?> ticks(WidgetTester tester) => [
  for (final box in tester.widgetList<Checkbox>(find.byType(Checkbox)))
    box.value,
];

void main() {
  testWidgets('sends nothing until the person goes online', (tester) async {
    await prepare(tester);
    await openStore(tester);
    expect(find.textContaining('Nothing has been sent'), findsOneWidget);
    expect(
      find.textContaining('https://orblitengine.com/store/'),
      findsWidgets,
    );
    expect(made, isEmpty);

    await tester.tap(find.text('Go online'));
    await tester.pumpAndSettle();
    expect(made, hasLength(1));
    expect(find.text('Crates'), findsOneWidget);
    expect(find.text('Scripts'), findsOneWidget);
  });

  testWidgets('says that no kit matches the words typed', (tester) async {
    await prepare(tester);
    await openStore(tester);
    await tester.tap(find.text('Go online'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField).first, 'nothing like it');
    await tester.pump();
    expect(find.text('Crates'), findsNothing);
    expect(find.textContaining('No kit matches'), findsOneWidget);
  });

  testWidgets('hides a kit for another engine until asked', (tester) async {
    await prepare(tester);
    await openStore(tester, engine: '0.50.0');
    await tester.tap(find.text('Go online'));
    await tester.pumpAndSettle();
    expect(find.text('Crates'), findsNothing);
    expect(
      find.textContaining('other engine versions are hidden'),
      findsOneWidget,
    );

    await tester.tap(find.byTooltip('Show kits for other engines'));
    await tester.pump();
    expect(find.text('Crates'), findsOneWidget);
    expect(find.text('Scripts · Other engine'), findsOneWidget);
  });

  testWidgets('shows the notes, the files and the scripts flag', (
    tester,
  ) async {
    await prepare(tester);
    await openStore(tester);
    await tester.tap(find.text('Go online'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Crates'));
    await tester.pumpAndSettle();
    expect(find.textContaining('The first release.'), findsOneWidget);
    expect(find.textContaining('Contains scripts'), findsOneWidget);
    // The kit file is not touched until the person asks for its files.
    expect(find.text('models/crate.obj'), findsNothing);
    expect(made.single.sent.where((r) => r.url == crateUrl), isEmpty);

    await tester.tap(find.text('Show files'));
    await until(tester, find.text('Files (3)'.toUpperCase()));
    expect(made.single.sent.where((r) => r.url == crateUrl), isNotEmpty);
    expect(find.text('models/crate.obj'), findsOneWidget);
    // The list is lazy, so the last of it comes into view by scrolling.
    await tester.drag(find.byType(CustomScrollView), const Offset(0, -400));
    await tester.pump();
    expect(find.text('scripts/spin.js'), findsOneWidget);

    await tester.tap(find.text('All kits'));
    await tester.pumpAndSettle();
    expect(find.text('Crates'), findsOneWidget);
  });

  testWidgets('fits the smallest window the editor allows', (tester) async {
    await prepare(tester, size: const Size(960, 620));
    await openStore(tester);
    await tester.tap(find.text('Go online'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Crates'));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
  });

  testWidgets('opens the install dialog from a kit and lists its scripts', (
    tester,
  ) async {
    await prepare(tester);
    await openStore(tester);
    await tester.tap(find.text('Go online'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Crates'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Install…'));
    await until(tester, find.textContaining('This kit holds 1 script.'));
    expect(find.textContaining('never runs them'), findsOneWidget);
    final inDialog = find.descendant(
      of: find.byType(InstallDialog),
      matching: find.text('scripts/spin.js'),
    );
    expect(inDialog, findsNWidgets(2));
    // Nothing is written by looking.
    expect(Directory(inProject('$into/scripts')).existsSync(), isFalse);
  });

  testWidgets('leaves the file of the project alone unless it is ticked', (
    tester,
  ) async {
    await prepare(tester);
    await openInstall(tester);
    await until(tester, find.text('replaces your file'));
    expect(ticks(tester), [false, true, true]);

    await tester.tap(installButton());
    await until(tester, find.textContaining('Installed 2 files'));
    expect(
      File(inProject('$into/models/crate.obj')).readAsStringSync(),
      'my own crate',
    );
    expect(File(inProject('$into/scripts/spin.js')).existsSync(), isTrue);
    expect(File(inProject(kitLockName)).existsSync(), isTrue);
  });

  testWidgets('replaces the file of the project when it is ticked', (
    tester,
  ) async {
    await prepare(tester);
    await openInstall(tester);
    await until(tester, find.text('replaces your file'));
    await tester.tap(find.byType(Checkbox).first);
    await tester.pump();
    expect(ticks(tester), [true, true, true]);

    await tester.tap(installButton());
    await until(tester, find.textContaining('Installed 3 files'));
    expect(
      File(inProject('$into/models/crate.obj')).readAsStringSync(),
      'a crate',
    );
  });

  testWidgets('refuses a folder a kit may not go into', (tester) async {
    await prepare(tester);
    await openInstall(tester);
    await until(tester, find.text('replaces your file'));
    await tester.enterText(find.byType(TextField), '.git/hooks');
    await tester.pump();
    expect(find.textContaining('starts with a dot'), findsOneWidget);
    expect(tester.widget<OrblitButton>(installButton()).onPressed, isNull);
    expect(Directory(inProject('.git')).existsSync(), isFalse);
  });

  testWidgets('keeps the choices when the write fails', (tester) async {
    await prepare(tester);
    File(inProject(kitLockName)).writeAsStringSync('{not json');
    await openInstall(tester);
    await until(tester, find.text('replaces your file'));
    await tester.tap(find.byType(Checkbox).at(1));
    await tester.pump();
    expect(ticks(tester), [false, false, true]);

    await tester.tap(installButton());
    await until(tester, find.textContaining('kits.lock'));
    await untilInstallable(tester);
    expect(ticks(tester), [false, false, true]);
    expect(Directory(inProject('$into/scripts')).existsSync(), isFalse);
  });

  testWidgets('reads a new folder only after a pause in the typing', (
    tester,
  ) async {
    await prepare(tester);
    await openInstall(tester);
    await until(tester, find.text('replaces your file'));
    await tester.enterText(find.byType(TextField), 'assets/other');
    // 200 ms of the app's time, with the disk given time to answer, so a read
    // that had started would have finished.
    for (var i = 0; i < 20; i++) {
      await tester.runAsync(
        () => Future<void>.delayed(const Duration(milliseconds: 10)),
      );
      await tester.pump(const Duration(milliseconds: 10));
    }
    expect(installable(tester), isFalse);

    await tester.pump(const Duration(milliseconds: 150));
    await untilInstallable(tester);
    expect(find.text('replaces your file'), findsNothing);
  });

  testWidgets('does not install when every file is unticked', (tester) async {
    await prepare(tester);
    await openInstall(tester);
    await until(tester, find.text('replaces your file'));
    await tester.tap(find.byType(Checkbox).at(1));
    await tester.tap(find.byType(Checkbox).at(2));
    await tester.pump();
    expect(ticks(tester), [false, false, false]);
    expect(tester.widget<OrblitButton>(installButton()).onPressed, isNull);
  });
}
