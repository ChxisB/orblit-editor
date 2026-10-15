import 'package:flutter/material.dart' hide Key;
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/retarget_dialog.dart';
import 'package:orblit_editor/src/theme/orblit_theme.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

// The dialog that makes a clip move another model: which models, how their
// bones pair, a map saved and loaded, and the clip it hands back.

RestSkeleton chain(List<String> names) => RestSkeleton(
  names: names,
  parents: [for (var i = 0; i < names.length; i++) i - 1],
  local: [
    for (var i = 0; i < names.length; i++)
      Matrix4.translation(Vector3(0, i == 0 ? 0 : 1, 0)),
  ],
);

ClipDocument nod() => ClipDocument(
  name: 'nod',
  duration: 1,
  channels: [
    for (final bone in ['spine', 'head'])
      ClipChannel<Quaternion>(
        target: '',
        bone: bone,
        property: 'rotation',
        kind: ChannelKind.rotation,
        keys: [
          Key(0, Quaternion.identity(), hold: Hold.linear),
          Key(
            1,
            Quaternion.axisAngle(Vector3(1, 0, 0), 0.5),
            hold: Hold.linear,
          ),
        ],
      ),
  ],
);

final class FakeFiles implements RetargetFiles {
  FakeFiles({
    this.skeletons = const {},
    this.stored = const {},
    this.fails = false,
  });

  final Map<String, RestSkeleton> skeletons;
  final Map<String, BoneMapLoad> stored;
  final bool fails;

  final List<(String, BoneMap)> written = [];

  @override
  List<String> get models => [...skeletons.keys, 'plain.glb']..sort();

  @override
  List<String> get maps => stored.keys.toList();

  @override
  RestSkeleton? skeletonOf(String model) => skeletons[model];

  @override
  BoneMapLoad readMap(String path) =>
      stored[path] ?? (throw const BoneMapFormatException('It is not a map.'));

  @override
  ({String? problem, String? path}) writeMap(String name, BoneMap map) {
    if (fails) return (problem: 'The disk is full.', path: null);
    written.add((name, map));
    return (problem: null, path: 'retargets/$name.obonemap');
  }
}

FakeFiles twoModels() => FakeFiles(
  skeletons: {
    'hero.glb': chain(['hips', 'spine', 'head']),
    'dwarf.glb': chain(['hips', 'spine', 'neck', 'tail']),
  },
);

/// Opens the dialog and answers what it returned once it closes.
Future<List<RetargetMade?>> show(
  WidgetTester tester,
  RetargetFiles files, {
  String? from,
}) async {
  final results = <RetargetMade?>[];
  await tester.binding.setSurfaceSize(const Size(1000, 800));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  await tester.pumpWidget(
    MaterialApp(
      theme: orblitTheme(),
      home: Builder(
        builder: (context) => TextButton(
          onPressed: () async => results.add(
            await showRetargetDialog(
              context,
              clip: nod(),
              files: files,
              from: from,
            ),
          ),
          child: const Text('open'),
        ),
      ),
    ),
  );
  await tester.tap(find.text('open'));
  await tester.pumpAndSettle();
  return results;
}

/// Picks [text] from the drop-down at [dropdown].
Future<void> choose(WidgetTester tester, Finder dropdown, String text) async {
  await tester.tap(dropdown);
  await tester.pumpAndSettle();
  await tester.tap(find.text(text).last);
  await tester.pumpAndSettle();
}

Finder modelPick(String label) => find.descendant(
  of: find.ancestor(of: find.text(label), matching: find.byType(Row)).first,
  matching: find.byType(DropdownButton<String>),
);

Finder pairOf(String bone) => find.byKey(ValueKey('pair/$bone'));

TextButton button(WidgetTester tester, String label) => tester.widget(
  find.ancestor(of: find.text(label), matching: find.byType(TextButton)),
);

void main() {
  testWidgets('asks for two models before it shows any pairs', (tester) async {
    await show(tester, twoModels());

    expect(find.textContaining('Choose the model'), findsOneWidget);
    expect(button(tester, 'Make clip').onPressed, isNull);
    expect(button(tester, 'Save map').onPressed, isNull);
  });

  testWidgets('starts on the model the clip is for', (tester) async {
    await show(tester, twoModels(), from: 'hero.glb');

    expect(find.text('hero.glb'), findsOneWidget);
    expect(find.textContaining('Choose the model'), findsOneWidget);
  });

  testWidgets('lists a bone of the target and what moves it', (tester) async {
    await show(tester, twoModels(), from: 'hero.glb');
    await choose(tester, modelPick('Move'), 'dwarf.glb');

    expect(find.text('2 of 4 bones are moved by something.'), findsOneWidget);
    expect(find.text('Automatic: spine'), findsOneWidget);
    expect(find.text('Automatic: no match'), findsNWidgets(2));
    expect(button(tester, 'Make clip').onPressed, isNotNull);
  });

  testWidgets('pairs a bone by hand', (tester) async {
    await show(tester, twoModels(), from: 'hero.glb');
    await choose(tester, modelPick('Move'), 'dwarf.glb');

    await choose(tester, pairOf('neck'), 'head');

    expect(find.text('3 of 4 bones are moved by something.'), findsOneWidget);
  });

  testWidgets('leaves a bone alone', (tester) async {
    await show(tester, twoModels(), from: 'hero.glb');
    await choose(tester, modelPick('Move'), 'dwarf.glb');

    await choose(tester, pairOf('spine'), 'Nothing');

    expect(find.text('1 of 4 bones are moved by something.'), findsOneWidget);
  });

  testWidgets('says when a model has no skeleton', (tester) async {
    await show(tester, twoModels(), from: 'hero.glb');
    await choose(tester, modelPick('Move'), 'plain.glb');

    expect(find.text('plain.glb has no skeleton.'), findsOneWidget);
    expect(button(tester, 'Make clip').onPressed, isNull);
  });

  testWidgets('hands back the clip, moved onto the target', (tester) async {
    final results = await show(tester, twoModels(), from: 'hero.glb');
    await choose(tester, modelPick('Move'), 'dwarf.glb');
    await choose(tester, pairOf('neck'), 'head');

    await tester.tap(find.text('Make clip'));
    await tester.pumpAndSettle();

    final made = results.single!;
    expect(made.model, 'dwarf.glb');
    expect(made.clip.name, 'nod (dwarf)');
    expect(
      {for (final channel in made.clip.channels) channel.bone},
      {'spine', 'neck'},
    );
  });

  testWidgets('leaves a bone out of the clip it was told to leave', (
    tester,
  ) async {
    final results = await show(tester, twoModels(), from: 'hero.glb');
    await choose(tester, modelPick('Move'), 'dwarf.glb');
    await choose(tester, pairOf('spine'), 'Nothing');

    await tester.tap(find.text('Make clip'));
    await tester.pumpAndSettle();

    expect({
      for (final channel in results.single!.clip.channels) channel.bone,
    }, isEmpty);
  });

  testWidgets('hands back nothing on cancel', (tester) async {
    final results = await show(tester, twoModels(), from: 'hero.glb');
    await tester.tap(find.text('Cancel'));
    await tester.pumpAndSettle();

    expect(results, [null]);
  });

  group('a bone map', () {
    testWidgets('is saved with the two models and the pairs', (tester) async {
      final files = twoModels();
      await show(tester, files, from: 'hero.glb');
      await choose(tester, modelPick('Move'), 'dwarf.glb');
      await choose(tester, pairOf('neck'), 'head');
      await choose(tester, pairOf('tail'), 'Nothing');

      await tester.tap(find.text('Save map'));
      await tester.pumpAndSettle();

      final (name, map) = files.written.single;
      expect(name, 'hero-to-dwarf');
      expect(map.fromAsset, 'hero.glb');
      expect(map.toAsset, 'dwarf.glb');
      expect(map.bones, {'neck': 'head'});
      expect(map.unpaired, {'tail'});
      expect(find.text('Saved retargets/hero-to-dwarf.obonemap.'), findsOne);
    });

    testWidgets('says why it could not be saved', (tester) async {
      final files = FakeFiles(skeletons: twoModels().skeletons, fails: true);
      await show(tester, files, from: 'hero.glb');
      await choose(tester, modelPick('Move'), 'dwarf.glb');

      await tester.tap(find.text('Save map'));
      await tester.pumpAndSettle();

      expect(
        find.text('Could not save the map: The disk is full.'),
        findsOneWidget,
      );
    });

    testWidgets('is loaded, with its models and its switches', (tester) async {
      final files = FakeFiles(
        skeletons: twoModels().skeletons,
        stored: {
          'retargets/a.obonemap': BoneMapLoad(
            map: BoneMap(
              fromAsset: 'hero.glb',
              toAsset: 'dwarf.glb',
              bones: {'neck': 'head'},
              humanoid: true,
            ),
          ),
        },
      );
      await show(tester, files);

      await choose(tester, modelPick('Bone map'), 'retargets/a.obonemap');

      expect(find.text('Loaded a.obonemap.'), findsOneWidget);
      expect(find.text('hero.glb'), findsOneWidget);
      expect(find.text('dwarf.glb'), findsOneWidget);
      expect(find.text('3 of 4 bones are moved by something.'), findsOneWidget);
    });

    testWidgets('names the pairs of a map made for other models', (
      tester,
    ) async {
      final files = FakeFiles(
        skeletons: twoModels().skeletons,
        stored: {
          'retargets/a.obonemap': BoneMapLoad(
            map: BoneMap(
              fromAsset: 'hero.glb',
              toAsset: 'dwarf.glb',
              bones: {'wing': 'head'},
            ),
          ),
        },
      );
      await show(tester, files);
      await choose(tester, modelPick('Bone map'), 'retargets/a.obonemap');

      expect(
        find.textContaining('names 1 bones these models lack'),
        findsOneWidget,
      );
      // The clip is still made, without the pair.
      expect(button(tester, 'Make clip').onPressed, isNotNull);
    });

    testWidgets('says what was wrong with a file that is not one', (
      tester,
    ) async {
      final files = FakeFiles(skeletons: twoModels().skeletons, stored: {});
      await show(tester, files);
      expect(find.text('None saved yet'), findsOneWidget);
    });
  });
}
