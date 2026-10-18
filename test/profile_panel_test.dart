import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/profile_panel.dart';
import 'package:orblit_profile/orblit_profile.dart';

// What the panel says about a profiler, with a clock the test winds by hand.

void main() {
  late int micros;
  late int bytes;
  late Profiler profiler;

  setUp(() {
    micros = 0;
    bytes = 100 * 1048576;
    profiler = Profiler(clock: () => micros, heap: () => bytes);
  });

  // A 20 ms frame in which `update` took 4 ms.
  void drawFrame() {
    micros += 16000;
    final started = profiler.begin();
    micros += 4000;
    profiler.end('update', started);
    profiler.frame();
    bytes += 1048576;
  }

  Future<void> show(WidgetTester tester, Profiler shown) => tester.pumpWidget(
    MaterialApp(
      home: Scaffold(body: ProfilePanel(profiler: shown)),
    ),
  );

  testWidgets('reads frames, spans, gauges, counts and memory', (tester) async {
    profiler.frame();
    for (var i = 0; i < 5; i++) {
      drawFrame();
    }
    profiler
      ..gauge('objects', 12)
      ..count('meshes', 3);

    await show(tester, profiler);

    expect(find.textContaining('50 fps · mean 20.0 ms'), findsOneWidget);
    expect(find.textContaining('mean 4.0 ms · max 4.0 ms'), findsOneWidget);
    expect(find.text('objects'), findsOneWidget);
    expect(find.text('12.0'), findsOneWidget);
    expect(find.text('meshes'), findsOneWidget);
    expect(find.textContaining('MB'), findsOneWidget);
    await show(tester, const Profiler.off());
  });

  testWidgets('reads again after the interval', (tester) async {
    profiler.frame();
    await show(tester, profiler);
    expect(find.text('none yet'), findsOneWidget);

    drawFrame();
    await tester.pump(const Duration(seconds: 1));

    expect(find.text('none yet'), findsNothing);
    expect(find.textContaining('50 fps'), findsOneWidget);
    await show(tester, const Profiler.off());
  });

  testWidgets('says a growing heap grows, in megabytes a minute', (
    tester,
  ) async {
    profiler.frame();
    for (var i = 0; i < 4; i++) {
      drawFrame();
      micros += 1000000;
      profiler.frame();
      bytes += 1048576;
    }

    await show(tester, profiler);

    expect(find.textContaining('MB/min'), findsOneWidget);
    expect(find.textContaining('+'), findsOneWidget);
    await show(tester, const Profiler.off());
  });

  testWidgets('says so when profiling is off', (tester) async {
    await show(tester, const Profiler.off());

    expect(find.text('Profiling is off.'), findsOneWidget);
  });
}
