import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/assets.dart';
import 'package:orblit_editor/src/editor/model_skeletons.dart';
import 'package:orblit_editor/src/editor/retarget_files.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:path/path.dart' as p;

import 'support/skinned_glb.dart';

// What the retarget dialog finds in a project folder and writes back to it.

void main() {
  late Directory root;
  late ProjectRetargetFiles files;

  setUp(() {
    root = Directory.systemTemp.createTempSync('orblit_retarget');
    files = ProjectRetargetFiles(
      assets: AssetTree(root.path),
      skeletons: ModelSkeletons(root.path),
    );
  });
  tearDown(() => root.deleteSync(recursive: true));

  void put(String path, List<int> bytes) {
    final file = File(p.join(root.path, path));
    file.parent.createSync(recursive: true);
    file.writeAsBytesSync(bytes);
  }

  group('models', () {
    test('are the glTF files anywhere in the project', () {
      put('hero.glb', chainGlb(['hips']));
      put('models/dwarf.gltf', const []);
      put('models/rock.obj', const []);
      put('notes.txt', const []);

      expect(files.models..sort(), ['hero.glb', 'models/dwarf.gltf']);
    });

    test('read their skeleton, and answer null for none', () {
      put('hero.glb', chainGlb(['hips', 'spine']));
      put(
        'plain.glb',
        glb({
          'asset': {'version': '2.0'},
        }),
      );

      expect(files.skeletonOf('hero.glb')!.names, ['hips', 'spine']);
      expect(files.skeletonOf('plain.glb'), isNull);
      expect(files.skeletonOf('missing.glb'), isNull);
    });
  });

  group('bone maps', () {
    final map = BoneMap(
      fromAsset: 'hero.glb',
      toAsset: 'dwarf.glb',
      bones: {'neck': 'head'},
      unpaired: {'tail'},
    );

    test('are written under retargets and read back', () {
      final made = files.writeMap('hero-to-dwarf', map);

      expect(made.problem, isNull);
      expect(made.path, 'retargets/hero-to-dwarf.obonemap');
      expect(files.maps, ['retargets/hero-to-dwarf.obonemap']);
      final read = files.readMap(made.path!);
      expect(read.problems, isEmpty);
      expect(read.map.encode(), map.encode());
    });

    test('are never written over another', () {
      final first = files.writeMap('hero-to-dwarf', map);
      final second = files.writeMap('hero-to-dwarf', map);

      expect(second.path, isNot(first.path));
      expect(files.maps, hasLength(2));
    });

    test('say so when a file is not one', () {
      put('retargets/junk.obonemap', 'not json'.codeUnits);

      expect(
        () => files.readMap('retargets/junk.obonemap'),
        throwsA(isA<BoneMapFormatException>()),
      );
    });

    test('say so when a file is not there', () {
      expect(
        () => files.readMap('retargets/gone.obonemap'),
        throwsA(isA<BoneMapFormatException>()),
      );
    });
  });

  test('the asset browser knows a bone map', () {
    expect(AssetKind.of('retargets/a.obonemap'), AssetKind.boneMap);
  });
}
