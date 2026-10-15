import 'dart:math' as math;

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/skeleton_view.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:vector_math/vector_math_64.dart';

// What the view draws of a skeleton and of the poses round the playhead,
// worked out without a view.

ClipDocument clipOf({
  double duration = 1,
  double rate = 10,
  WhenDone whenDone = WhenDone.hold,
}) => ClipDocument(
  name: 'walk',
  duration: duration,
  rate: rate,
  whenDone: whenDone,
);

List<double> times(List<OnionMoment> moments) => [
  for (final moment in moments) moment.at,
];

void expectTimes(List<double> got, List<double> want) {
  expect(got.length, want.length, reason: '$got is not $want');
  for (var i = 0; i < want.length; i++) {
    expect(got[i], closeTo(want[i], 1e-9), reason: '$got is not $want');
  }
}

void main() {
  group('onion moments', () {
    const on = SkeletonSettings(onion: true, before: 2, after: 2, stride: 1);

    test('are one frame apart, earlier first and nearest first', () {
      final moments = onionMoments(clipOf(), 0.5, on);
      expectTimes(times(moments), [0.4, 0.3, 0.6, 0.7]);
      expect([for (final m in moments) m.earlier], [true, true, false, false]);
    });

    test('step by the stride in frames', () {
      final moments = onionMoments(
        clipOf(),
        0.5,
        on.copyWith(stride: 2, before: 1, after: 1),
      );
      expectTimes(times(moments), [0.3, 0.7]);
    });

    test('are none with onion skin off', () {
      expect(
        onionMoments(clipOf(), 0.5, const SkeletonSettings(bones: true)),
        isEmpty,
      );
    });

    test('leave out a ghost that falls off either end of a clip', () {
      expectTimes(times(onionMoments(clipOf(), 0.1, on)), [0, 0.2, 0.3]);
      expectTimes(times(onionMoments(clipOf(), 0.9, on)), [0.8, 0.7, 1]);
    });

    test('wrap round for a clip that loops', () {
      final moments = onionMoments(clipOf(whenDone: WhenDone.loop), 0.1, on);
      // One frame before the start of a loop is the frame before its end.
      expectTimes(times(moments), [0, 0.9, 0.2, 0.3]);
    });

    test('never ghost the playhead, or one moment twice, on a short loop', () {
      final moments = onionMoments(
        clipOf(duration: 0.2, whenDone: WhenDone.loop),
        0,
        on,
      );
      expectTimes(times(moments), [0.1]);
    });

    test('are none for a clip with no length or no rate', () {
      expect(onionMoments(clipOf(rate: 0), 0, on), isEmpty);
      expect(onionMoments(clipOf(duration: 0), 0, on), isEmpty);
    });
  });

  group('bone lines', () {
    Matrix4 at(double x, double y, double z) => Matrix4.compose(
      Vector3(x, y, z),
      Quaternion.identity(),
      Vector3.all(1),
    );

    final spine = RestSkeleton(
      names: ['hips', 'spine', 'head'],
      parents: [-1, 0, 1],
      local: [at(0, 1, 0), at(0, 0.5, 0), at(0, 0.4, 0)],
    );

    void expectNear(Vector3 got, Vector3 want) {
      expect((got - want).length, lessThan(1e-9), reason: '$got is not $want');
    }

    test(
      'join each bone to the joint it hangs from, and a root to nothing',
      () {
        final lines = boneLines(spine, const {});
        expect(lines, hasLength(2));
        expectNear(lines[0].$1, Vector3(0, 1, 0));
        expectNear(lines[0].$2, Vector3(0, 1.5, 0));
        expectNear(lines[1].$1, Vector3(0, 1.5, 0));
        expectNear(lines[1].$2, Vector3(0, 1.9, 0));
      },
    );

    test('follow a pose', () {
      // A quarter turn about Z at the spine lays the head along -X.
      final lines = boneLines(spine, {
        'spine': BoneLocal(
          rotation: Quaternion.axisAngle(Vector3(0, 0, 1), math.pi / 2),
        ),
      });
      expectNear(lines[0].$2, Vector3(0, 1.5, 0));
      expectNear(lines[1].$2, Vector3(-0.4, 1.5, 0));
    });
  });
}
