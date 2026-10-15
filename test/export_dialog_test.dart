import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_editor/src/editor/export_dialog.dart';
import 'package:orblit_editor/src/editor/export_folder.dart';
import 'package:orblit_editor/src/editor/export_run.dart';
import 'package:orblit_editor/src/editor/export_session.dart';
import 'package:orblit_editor/src/editor/pack_contents.dart';
import 'package:orblit_editor/src/theme/orblit_theme.dart';
import 'package:orblit_editor/src/widgets/controls.dart';

// The export dialog on a fake runner and a fake folder, so that what is
// checked is what it draws and what it asks for, and no cook starts.

ExportedPack _pack(
  String name, {
  String target = 'macos',
  String? base,
  int assets = 3,
  int bytes = 2048,
}) => ExportedPack(
  path: '/project/exports/$name',
  target: target,
  hash: 'hash-of-$name',
  base: base,
  assets: assets,
  removed: 0,
  bytes: bytes,
  modified: DateTime(2026, 10, 15),
);

PackSummary _summary({int assets = 3, int bytes = 4096, int shared = 0}) =>
    PackSummary(
      hash: ContentHash.of(const [1, 2, 3]),
      bytes: bytes,
      assets: assets,
      added: assets,
      replaced: 0,
      blobs: assets,
      sharedBytes: shared,
      removed: 0,
    );

void main() {
  late List<ExportedPack> held;
  late List<ExportRequest> asked;
  late Completer<void>? hold;
  late List<TargetExport> answer;
  late List<String> revealed;

  setUp(() {
    held = [];
    asked = [];
    hold = null;
    revealed = [];
    answer = [];
  });

  Future<void> show(
    WidgetTester tester, {
    Size size = const Size(1440, 900),
    Future<PackContents> Function(String, String)? reader,
  }) async {
    await tester.binding.setSurfaceSize(size);
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(
      MaterialApp(
        theme: orblitTheme(),
        home: Scaffold(
          body: Builder(
            builder: (context) => TextButton(
              onPressed: () => showDialog<void>(
                context: context,
                barrierDismissible: false,
                builder: (_) => ExportDialog(
                  projectDirectory: '/project',
                  onReveal: revealed.add,
                  sessionFor: (directory) => ExportSession(
                    projectDirectory: directory,
                    lister: (_) async => ExportListing(packs: held),
                    reader:
                        reader ??
                        (_, _) async => const PackContents(
                          packs: [],
                          rows: [],
                          removed: [],
                        ),
                    runner: (request, {onTarget}) async {
                      asked.add(request);
                      await hold?.future;
                      for (final done in answer) {
                        onTarget?.call(done);
                      }
                      return answer;
                    },
                  ),
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

  Finder chip(String name) => find.widgetWithText(FilterChip, name);

  Finder button(String label) =>
      find.widgetWithText(OrblitButton, label, skipOffstage: false);

  bool enabled(WidgetTester tester, Finder finder) =>
      tester.widget<OrblitButton>(finder).onPressed != null;

  testWidgets('an empty folder says a whole pack goes first', (tester) async {
    await show(tester);

    expect(find.textContaining('Nothing exported yet'), findsOneWidget);
    expect(button('Export'), findsWidgets);
  });

  testWidgets('lists what the exports folder holds', (tester) async {
    held = [
      _pack('game.macos.opack', bytes: 3 * 1024 * 1024),
      _pack('game.macos.abc123.opack', base: 'x', assets: 1),
    ];
    await show(tester);

    expect(find.text('game.macos.opack'), findsOneWidget);
    expect(find.text('game.macos.abc123.opack'), findsOneWidget);
    expect(find.textContaining('whole · 3 assets'), findsOneWidget);
    expect(find.textContaining('patch · 1 asset ·'), findsOneWidget);
  });

  testWidgets('says a file that did not open and still lists the rest', (
    tester,
  ) async {
    held = [_pack('game.macos.opack')];
    await tester.binding.setSurfaceSize(const Size(1440, 900));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    final session = ExportSession(
      projectDirectory: '/project',
      lister: (_) async => ExportListing(
        packs: held,
        unreadable: const ['bad.opack: not a pack'],
      ),
    );
    await tester.pumpWidget(
      MaterialApp(
        theme: orblitTheme(),
        home: Scaffold(
          body: ExportDialog(
            projectDirectory: '/project',
            onReveal: revealed.add,
            sessionFor: (_) => session,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('game.macos.opack'), findsOneWidget);
    expect(find.text('bad.opack: not a pack'), findsOneWidget);
  });

  testWidgets('exports the targets that are chosen', (tester) async {
    answer = [
      TargetExport(
        target: 'macos',
        outcome: ExportOutcome.packed,
        path: '/project/exports/game.macos.opack',
        summary: _summary(assets: 2, bytes: 2 * 1024 * 1024, shared: 1024),
        cooked: '2 cooked',
      ),
    ];
    await show(tester);
    for (final name in ['macos', 'windows', 'linux', 'ios', 'android', 'web']) {
      final chosen = tester.widget<FilterChip>(chip(name)).selected;
      if (chosen != (name == 'macos')) await tester.tap(chip(name));
    }
    await tester.pump();

    await tester.tap(button('Export').last);
    await tester.pumpAndSettle();

    expect(asked.single.targets, ['macos']);
    expect(asked.single.patchOf, isNull);
    expect(find.text('game.macos.opack · 2 assets · 2.0 MB'), findsOneWidget);
    expect(find.textContaining('Cook: 2 cooked.'), findsOneWidget);
  });

  testWidgets('a patch locks the target and says what it changed', (
    tester,
  ) async {
    held = [_pack('game.android.opack', target: 'android')];
    answer = [
      TargetExport(
        target: 'android',
        outcome: ExportOutcome.packed,
        path: '/project/exports/game.android.abc.opack',
        patchOf: 'game.android.opack',
        summary: PackSummary(
          hash: ContentHash.of(const [4]),
          bytes: 900,
          assets: 3,
          added: 1,
          replaced: 2,
          blobs: 3,
          sharedBytes: 0,
          removed: 1,
        ),
        cooked: '3 cooked',
      ),
    ];
    await show(tester);

    await tester.tap(find.byType(DropdownButton<ExportedPack?>));
    await tester.pumpAndSettle();
    await tester.tap(find.text('A patch of game.android.opack').last);
    await tester.pumpAndSettle();

    expect(find.text('Export patch'), findsOneWidget);
    expect(tester.widget<FilterChip>(chip('macos')).onSelected, isNull);
    expect(tester.widget<FilterChip>(chip('android')).selected, isTrue);

    await tester.tap(find.text('Export patch'));
    await tester.pumpAndSettle();

    expect(asked.single.patchOf, '/project/exports/game.android.opack');
    expect(asked.single.targets, ['android']);
    expect(
      find.textContaining(
        'Patch of game.android.opack: 1 new, 2 replaced, 1 removed.',
      ),
      findsOneWidget,
    );
  });

  testWidgets('cannot be closed while it runs', (tester) async {
    hold = Completer<void>();
    answer = [
      const TargetExport(target: 'macos', outcome: ExportOutcome.packed),
    ];
    await show(tester);
    final close = find.widgetWithText(TextButton, 'Close');

    await tester.tap(button('Export').last);
    await tester.pump();

    expect(tester.widget<TextButton>(close).onPressed, isNull);
    expect(enabled(tester, button('Export').last), isFalse);
    expect(find.textContaining('Cooking and packing'), findsOneWidget);

    hold!.complete();
    await tester.pumpAndSettle();

    expect(tester.widget<TextButton>(close).onPressed, isNotNull);
    await tester.tap(close);
    await tester.pumpAndSettle();
    expect(find.byType(ExportDialog), findsNothing);
  });

  testWidgets('names the assets that did not cook and the reason', (
    tester,
  ) async {
    answer = [
      const TargetExport(
        target: 'macos',
        outcome: ExportOutcome.cookFailed,
        message: 'Nothing was packed for macos.',
        cooked: '1 failed',
        failures: [ExportFailure('props/rock.gltf', 'not valid JSON')],
      ),
    ];
    await show(tester);

    await tester.tap(button('Export').last);
    await tester.pumpAndSettle();

    expect(find.text('Nothing was packed for macos.'), findsOneWidget);
    expect(find.text('props/rock.gltf: not valid JSON'), findsOneWidget);
    expect(find.text('Contents'), findsOneWidget, reason: 'the view switch');
  });

  testWidgets('shows what a pack serves, with the pack each asset is in', (
    tester,
  ) async {
    held = [_pack('game.macos.abc.opack', base: 'x')];
    await show(
      tester,
      reader: (directory, path) async => const PackContents(
        packs: ['game.macos.opack', 'game.macos.abc.opack'],
        rows: [
          ContentsRow(
            id: 'props/rock.gltf',
            bytes: 2048,
            hash: 'aaaaaaaaaaaa',
            pack: 'game.macos.abc.opack',
          ),
          ContentsRow(
            id: 'props/ship.gltf',
            bytes: 100,
            hash: 'bbbbbbbbbbbb',
            pack: 'game.macos.opack',
          ),
        ],
        removed: ['old.gltf'],
      ),
    );

    await tester.tap(find.text('game.macos.abc.opack'));
    await tester.pumpAndSettle();

    expect(find.text('props/rock.gltf'), findsOneWidget);
    expect(find.text('props/ship.gltf'), findsOneWidget);
    expect(find.text('game.macos.opack'), findsOneWidget);
    expect(
      find.textContaining('2 assets · 2 KB · 2 packs · 1 taken away'),
      findsOneWidget,
    );
  });

  testWidgets('says why a broken chain cannot be read', (tester) async {
    held = [_pack('game.macos.abc.opack', base: 'x')];
    await show(
      tester,
      reader: (_, _) async =>
          throw const ExportChainBroken('no pack is that one'),
    );

    await tester.tap(find.text('game.macos.abc.opack'));
    await tester.pumpAndSettle();

    expect(find.text('no pack is that one'), findsOneWidget);
  });

  testWidgets('shows the exports folder on request', (tester) async {
    await show(tester);

    await tester.tap(find.text('Show exports folder'));

    expect(revealed, ['/project/exports']);
  });

  for (final size in const [Size(960, 620), Size(800, 600)]) {
    testWidgets('fits a window ${size.width.toInt()} wide without overflow', (
      tester,
    ) async {
      held = [for (var i = 0; i < 8; i++) _pack('game.macos.$i.opack')];
      answer = [
        for (final target in ['macos', 'windows', 'linux'])
          TargetExport(
            target: target,
            outcome: ExportOutcome.cookFailed,
            message: 'Nothing was packed for $target.',
            cooked: '1 failed',
            failures: const [ExportFailure('a.gltf', 'bad')],
          ),
      ];
      await show(tester, size: size);

      await tester.tap(button('Export').last);
      await tester.pumpAndSettle();
      await tester.tap(button('Contents').first);
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
    });
  }
}
