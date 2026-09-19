import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/asset_browser.dart';
import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_editor/src/editor/assets.dart';
import 'package:orblit_editor/src/editor/cook_status.dart';
import 'package:orblit_editor/src/theme/orblit_theme.dart';
import 'package:path/path.dart' as p;

/// A real two-by-two PNG. Written rather than faked, because `Image.file` on
/// a byte of nonsense falls through to the error builder and shows the very
/// icon this is meant to prove is gone — a test that passes because the
/// picture failed to load is a test of nothing.
final _png = base64Decode(
  'iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEklEQVR4nGNg0DgBQhUaPUAEABo6BDmBaztmAAAAAElFTkSuQmCC',
);

void main() {
  late Directory root;

  setUp(() {
    root = Directory.systemTemp.createTempSync('orblit_assets');
  });
  tearDown(() => root.deleteSync(recursive: true));

  Future<void> show(WidgetTester tester, {CookStatusIndex? cookStatus}) async {
    await tester.binding.setSurfaceSize(const Size(1000, 700));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(
      MaterialApp(
        theme: orblitTheme(),
        home: Scaffold(
          body: AssetBrowser(
            tree: AssetTree(root.path),
            cookStatus: cookStatus,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  /// An index holding an answer rather than working one out.
  ///
  /// The working out is tested where it lives, against a real cook and a real
  /// cache. What is this file's business is what the panel draws once it has
  /// an answer, and hashing files to get there would make every one of these
  /// a test of the cook as well.
  CookStatusIndex holding(Map<String, CookState> states) {
    final index = CookStatusIndex(
      projectDirectory: root.path,
      cachePath: p.join(root.path, 'cache'),
    );
    addTearDown(index.dispose);
    index.hold({
      for (final entry in states.entries)
        p.join(root.path, entry.key): entry.value,
    });
    return index;
  }

  /// The dots the panel draws, by colour.
  Iterable<Color?> marks(WidgetTester tester) => tester
      .widgetList<Container>(find.byType(Container))
      .map((container) => container.decoration)
      .whereType<BoxDecoration>()
      .where((decoration) => decoration.shape == BoxShape.circle)
      .map((decoration) => decoration.color);

  group('the asset browser', () {
    testWidgets('points a texture tile at the file itself', (tester) async {
      final path = p.join(root.path, 'colormap.png');
      File(path).writeAsBytesSync(_png);

      await show(tester);

      // What the tile is *for* the file, not what the decoder made of it.
      // Whether an image decodes is Flutter's business and arrives on a real
      // clock the widget tester does not run; asserting the picture appeared
      // would either be flaky or — worse — quietly pass because the fallback
      // had not been delivered either. What is this code's business is that
      // a png is routed to an Image and pointed at the right file.
      final image = tester.widget<Image>(find.byType(Image));

      // Wrapped, because the tile asks for a decode width. That wrapper is
      // the point rather than an implementation detail: a folder of 4K maps
      // decoded at their authored size is a gigabyte of pixels drawn at
      // forty-four points.
      final resized = image.image as ResizeImage;
      expect(resized.width, isNotNull);
      expect((resized.imageProvider as FileImage).file.path, path);
      expect(
        image.errorBuilder,
        isNotNull,
        reason: 'a file that will not decode should fall back to the icon',
      );
    });

    testWidgets('keeps the icon for a texture it cannot decode', (
      tester,
    ) async {
      // A GPU format. Flutter has no decoder for it, and a broken-image box
      // would say less than a glyph that at least names the kind.
      File(p.join(root.path, 'rock.ktx2')).writeAsBytesSync([0xAB, 0x4B, 0x54]);

      await show(tester);

      expect(find.byType(Image), findsNothing);
      expect(find.byIcon(AssetKind.texture.icon), findsOneWidget);
    });

    testWidgets('marks an asset that needs cooking', (tester) async {
      File(p.join(root.path, 'ship.gltf')).writeAsStringSync('{}');

      await show(tester, cookStatus: holding({'ship.gltf': CookState.stale}));

      expect(marks(tester), [OrblitColors.warn]);
    });

    testWidgets('marks a cooked asset differently', (tester) async {
      File(p.join(root.path, 'ship.gltf')).writeAsStringSync('{}');

      await show(tester, cookStatus: holding({'ship.gltf': CookState.cooked}));

      expect(marks(tester), [OrblitColors.good]);
    });

    testWidgets('marks one that would not cook', (tester) async {
      File(p.join(root.path, 'ship.gltf')).writeAsStringSync('{}');

      await show(tester, cookStatus: holding({'ship.gltf': CookState.failed}));

      expect(marks(tester), [OrblitColors.bad]);
    });

    testWidgets('leaves a file with nothing to cook unmarked', (tester) async {
      File(p.join(root.path, 'notes.md')).writeAsStringSync('read me');

      await show(tester, cookStatus: holding({'notes.md': CookState.ignored}));

      expect(marks(tester), isEmpty);
    });

    testWidgets('marks nothing at all without a cook to ask', (tester) async {
      File(p.join(root.path, 'ship.gltf')).writeAsStringSync('{}');

      await show(tester);

      expect(
        marks(tester),
        isEmpty,
        reason: 'a browser is worth having before a project is ever cooked',
      );
    });

    testWidgets('says in the header what is left to do', (tester) async {
      File(p.join(root.path, 'one.gltf')).writeAsStringSync('{}');
      File(p.join(root.path, 'two.gltf')).writeAsStringSync('{}');
      File(p.join(root.path, 'three.gltf')).writeAsStringSync('{}');

      await show(
        tester,
        cookStatus: holding({
          'one.gltf': CookState.stale,
          'two.gltf': CookState.failed,
          'three.gltf': CookState.cooked,
        }),
      );

      expect(find.text('1 would not cook · 1 to cook'), findsOneWidget);
    });

    testWidgets('says nothing when there is nothing to do', (tester) async {
      File(p.join(root.path, 'ship.gltf')).writeAsStringSync('{}');

      await show(tester, cookStatus: holding({'ship.gltf': CookState.cooked}));

      expect(find.textContaining('to cook'), findsNothing);
    });

    testWidgets('puts the state in the tile\'s tooltip', (tester) async {
      File(p.join(root.path, 'ship.gltf')).writeAsStringSync('{}');

      await show(tester, cookStatus: holding({'ship.gltf': CookState.failed}));

      final tooltip = tester
          .widgetList<Tooltip>(find.byType(Tooltip))
          .firstWhere((t) => t.message!.startsWith('ship.gltf'));
      expect(tooltip.message, contains('Would not cook'));
      expect(tooltip.message, contains('importer could not do it'));
    });

    testWidgets('keeps the icon for things that are not pictures', (
      tester,
    ) async {
      File(p.join(root.path, 'player.dart'))
          .writeAsStringSync('void main() {}');

      await show(tester);

      expect(find.byType(Image), findsNothing);
      expect(find.byIcon(AssetKind.script.icon), findsOneWidget);
    });
  });
}
