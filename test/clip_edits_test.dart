import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/clip_edits.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_scene/orblit_scene.dart' show TransformComponent;
import 'package:vector_math/vector_math_64.dart';

const ChannelAddress height = (
  target: 'lamp',
  bone: null,
  property: 'light.power',
);

const ChannelAddress place = (
  target: '',
  bone: null,
  property: 'transform.position',
);

ClipDocument clipWith(List<Key<double>> keys, {double duration = 2}) =>
    ClipDocument(
      name: 'test',
      duration: duration,
      channels: [
        ClipChannel<double>(
          target: height.target,
          property: height.property,
          kind: ChannelKind.number,
          keys: keys,
        ),
      ],
    );

List<double> timesOf(ClipDocument clip, ChannelAddress address) => [
  for (final key in channelAt(clip, address)!.keys) key.at,
];

List<Object> valuesOf(ClipDocument clip, ChannelAddress address) => [
  for (final key in channelAt(clip, address)!.keys) key.value,
];

void main() {
  group('snapping', () {
    test('lands on the nearest frame of the clip\'s own rate', () {
      expect(snapped(0.51, 30), closeTo(15 / 30, 1e-12));
      expect(snapped(0.51, 24), closeTo(12 / 24, 1e-12));
      expect(snapped(0.51, 0), 0.51);
    });
  });

  group('keying', () {
    test('makes a channel of the kind asked for', () {
      final clip = keyed(
        ClipDocument(name: 'empty', duration: 1),
        place,
        ChannelKind.vector,
        0.5,
        Vector3(1, 2, 3),
      );
      final channel = channelAt(clip, place)!;
      expect(channel, isA<ClipChannel<Vector3>>());
      expect(channel.keys.single.hold, Hold.smooth);
      expect(markOf(clip, place, 0.5), KeyMark.keyed);
      expect(markOf(clip, place, 0.25), KeyMark.animated);
      expect(markOf(clip, height, 0.5), KeyMark.unkeyed);
    });

    test('replaces a key on the same frame and keeps how it travels', () {
      final clip = keyed(
        clipWith([
          const Key(0, 1, hold: Hold.curve, slopeOut: 4),
          const Key(1, 2),
        ]),
        height,
        ChannelKind.number,
        0,
        5.0,
      );
      final key = channelAt(clip, height)!.keys.first;
      expect(key.value, 5);
      expect(key.hold, Hold.curve);
      expect(key.slopeOut, 4);
    });

    test('a new key travels the way the key before it does', () {
      final clip = keyed(
        clipWith([
          const Key(0, 1, hold: Hold.linear),
          const Key(1, 2),
        ]),
        height,
        ChannelKind.number,
        0.5,
        9.0,
      );
      expect(timesOf(clip, height), [0, 0.5, 1]);
      expect(channelAt(clip, height)!.keys[1].hold, Hold.linear);
    });

    test('reads a field as the scene writes it', () {
      expect(kindOfField(3), ChannelKind.number);
      expect(kindOfField(true), ChannelKind.flag);
      expect(kindOfField([1, 2, 3]), ChannelKind.vector);
      expect(kindOfField([1, 2, 3, 1]), isNull);
      expect(kindOfField('#ffffff'), isNull);

      final turn = keyValueOf(
        ChannelKind.rotation,
        'transform.rotation',
        [20, 40, 10],
      )! as Quaternion;
      // Turns a model the way the field does, compared as the matrix a model
      // is drawn with. Not through `Quaternion.rotated`, which turns by the
      // inverse.
      final field = TransformComponent.rotationOf(Vector3(20, 40, 10));
      final key = turn.asRotationMatrix();
      for (var i = 0; i < 9; i++) {
        expect(key.storage[i], closeTo(field.storage[i], 1e-9));
      }
      expect(keyValueOf(ChannelKind.number, 'light.power', 2), 2.0);
    });
  });

  group('deleting', () {
    test('drops the keys, and a channel left with none', () {
      final start = clipWith([const Key(0, 1), const Key(1, 2)]);
      final one = withoutKeys(start, {(channel: height, index: 0)});
      expect(timesOf(one, height), [1]);
      final none = withoutKeys(start, {
        (channel: height, index: 0),
        (channel: height, index: 1),
      });
      expect(none.channels, isEmpty);
    });
  });

  group('moving', () {
    final start = clipWith([
      const Key(0, 1),
      const Key(0.5, 2),
      const Key(1, 3),
    ]);

    test('moves by whole frames and says where the keys went', () {
      final moved = movedKeys(start, {(channel: height, index: 0)}, 0.26);
      // 0.26 s is frame 7.8 at thirty, so eight frames.
      expect(timesOf(moved.clip, height)[0], closeTo(8 / 30, 1e-9));
      expect(moved.keys, {(channel: height, index: 0)});
    });

    test('passing another key reorders them, and the selection follows', () {
      final moved = movedKeys(start, {(channel: height, index: 0)}, 0.8);
      expect(timesOf(moved.clip, height), [0.5, closeTo(0.8, 1e-9), 1]);
      expect(valuesOf(moved.clip, height), [2, 1, 3]);
      expect(moved.keys, {(channel: height, index: 1)});
    });

    test('landing on a key replaces it', () {
      final moved = movedKeys(start, {(channel: height, index: 0)}, 0.5);
      expect(timesOf(moved.clip, height), [0.5, 1]);
      expect(valuesOf(moved.clip, height), [1, 3]);
      expect(moved.keys, {(channel: height, index: 0)});
    });

    test('stops at either end of the clip', () {
      final early = movedKeys(start, {(channel: height, index: 1)}, -3);
      expect(timesOf(early.clip, height), [0, 1]);
      final late = movedKeys(start, {
        (channel: height, index: 1),
        (channel: height, index: 2),
      }, 5);
      // The later key reaches the end, and the other keeps its distance.
      expect(timesOf(late.clip, height), [0, 1.5, 2]);
    });

    test('from the clip as the drag began, so passing over a key and back '
        'leaves it', () {
      final ref = {(channel: height, index: 0)};
      movedKeys(start, ref, 0.5);
      final back = movedKeys(start, ref, 0.1);
      expect(timesOf(back.clip, height), [closeTo(0.1, 1e-9), 0.5, 1]);
    });
  });

  group('holds and slopes', () {
    test('a shaped hold with no shape eases in and out', () {
      final clip = withHold(
        clipWith([const Key(0, 0), const Key(1, 1)]),
        {(channel: height, index: 0)},
        Hold.shaped,
      );
      expect(channelAt(clip, height)!.keys.first.shape, Easing.inOut);
    });

    test('an aligned handle sets both slopes and makes both spans curves', () {
      final clip = withSlope(
        clipWith([const Key(0, 0), const Key(1, 1), const Key(2, 0)]),
        (channel: height, index: 1),
        side: KeySide.leaving,
        part: 0,
        slope: 3,
      );
      final keys = channelAt(clip, height)!.keys;
      expect(keys[1].slopeIn, 3);
      expect(keys[1].slopeOut, 3);
      expect(keys[0].hold, Hold.curve);
      expect(keys[1].hold, Hold.curve);
      expect(keys[2].hold, Hold.smooth);
    });

    test('a broken handle sets its own side only', () {
      final clip = withSlope(
        clipWith([const Key(0, 0), const Key(1, 1), const Key(2, 0)]),
        (channel: height, index: 1),
        side: KeySide.arriving,
        part: 0,
        slope: -2,
        broken: true,
      );
      final keys = channelAt(clip, height)!.keys;
      expect(keys[1].slopeIn, -2);
      expect(keys[1].slopeOut, isNull);
      expect(keys[0].hold, Hold.curve);
      expect(keys[1].hold, Hold.smooth);
    });

    test('one part of a vector\'s slope, the rest as the curve had it', () {
      final clip = ClipDocument(
        name: 'test',
        duration: 2,
        channels: [
          ClipChannel<Vector3>(
            target: place.target,
            property: place.property,
            kind: ChannelKind.vector,
            keys: [
              Key(0, Vector3.zero()),
              Key(1, Vector3(1, 2, 3)),
              Key(2, Vector3(2, 4, 6)),
            ],
          ),
        ],
      );
      final sloped = withSlope(
        clip,
        (channel: place, index: 1),
        side: KeySide.leaving,
        part: 1,
        slope: 10,
      );
      final slope = channelAt(sloped, place)!.keys[1].slopeOut! as Vector3;
      // The worked-out slope through a straight line is the line's.
      expect(slope.x, closeTo(1, 1e-9));
      expect(slope.y, 10);
      expect(slope.z, closeTo(3, 1e-9));

      final reset = withAutoSlopes(sloped, {(channel: place, index: 1)});
      expect(channelAt(reset, place)!.keys[1].slopeOut, isNull);
      expect(channelAt(reset, place)!.keys[1].hold, Hold.curve);
    });
  });
}
