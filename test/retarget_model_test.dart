import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/retarget_model.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:vector_math/vector_math_64.dart';

// What the retarget table says of a pair of skeletons, without a dialog.

RestSkeleton chain(List<String> names) => RestSkeleton(
  names: names,
  parents: [for (var i = 0; i < names.length; i++) i - 1],
  local: [
    for (var i = 0; i < names.length; i++)
      Matrix4.translation(Vector3(0, i == 0 ? 0 : 1, 0)),
  ],
);

PairRow rowOf(PairTable table, String bone) =>
    table.rows.singleWhere((row) => row.bone == bone);

void main() {
  final hero = chain(['hips', 'spine', 'head']);
  final dwarf = chain(['hips', 'spine', 'neck', 'tail']);

  group('the pair table', () {
    test('has a row for every bone of the target, in its order', () {
      final table = pairTable(from: hero, to: dwarf, map: BoneMap());
      expect([for (final row in table.rows) row.bone], dwarf.names);
    });

    test('pairs by name and leaves the rest without a driver', () {
      final table = pairTable(from: hero, to: dwarf, map: BoneMap());

      expect(rowOf(table, 'spine').driver, 'spine');
      expect(rowOf(table, 'spine').how, PairHow.auto);
      expect(rowOf(table, 'neck').driver, isNull);
      expect(table.driven, 2);
    });

    test('says what was chosen by hand', () {
      final table = pairTable(
        from: hero,
        to: dwarf,
        map: BoneMap().withPair('neck', 'head'),
      );

      expect(rowOf(table, 'neck').driver, 'head');
      expect(rowOf(table, 'neck').how, PairHow.hand);
      expect(table.driven, 3);
    });

    test('leaves a bone alone when a name would have paired it', () {
      final table = pairTable(
        from: hero,
        to: dwarf,
        map: BoneMap().withUnpaired('spine'),
      );

      expect(rowOf(table, 'spine').driver, isNull);
      expect(rowOf(table, 'spine').how, PairHow.none);
    });

    test('names what a skeleton lacks and hands back a map without it', () {
      final map = BoneMap(
        bones: {'neck': 'head', 'wing': 'head', 'tail': 'horn'},
        unpaired: {'fin'},
      );
      final table = pairTable(from: hero, to: dwarf, map: map);

      expect(table.stale, ['fin', 'tail', 'wing']);
      expect(table.map.bones, {'neck': 'head'});
      expect(table.map.unpaired, isEmpty);
      // The map given is not touched, so saving it keeps what was written.
      expect(map.bones, hasLength(3));
    });

    test('is what retargeting does with the map it hands back', () {
      final clip = ClipDocument(
        name: 'nod',
        duration: 1,
        channels: [
          ClipChannel<Quaternion>(
            target: '',
            bone: 'head',
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
      final table = pairTable(
        from: hero,
        to: dwarf,
        map: BoneMap().withPair('neck', 'head'),
      );

      final made = table.map.apply(clip, from: hero, to: dwarf);
      expect(
        {for (final channel in made.clip.channels) channel.bone},
        {'neck'},
      );
    });
  });

  group('a retargeted clip\'s name', () {
    test('is the clip and the model it moves', () {
      expect(retargetedName('Walk', 'models/dwarf.glb'), 'Walk (dwarf)');
    });

    test('reads a model with no folder, or a Windows separator', () {
      expect(retargetedName('Walk', 'dwarf.gltf'), 'Walk (dwarf)');
      expect(retargetedName('Walk', r'models\dwarf.glb'), 'Walk (dwarf)');
    });
  });

  group('a draft', () {
    test('keeps what it was not told to change', () {
      final draft = RetargetDraft(from: 'a.glb', map: BoneMap());
      final next = draft.copyWith(to: 'b.glb');

      expect(next.from, 'a.glb');
      expect(next.to, 'b.glb');
      expect(next.map, same(draft.map));
    });
  });
}
