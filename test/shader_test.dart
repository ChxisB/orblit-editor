import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/assets.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_editor/src/editor/shader_section.dart';
import 'package:orblit_editor/src/editor/surface_cook.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_editor/src/theme/orblit_theme.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

// An author-written shader in the editor: cooked as it is saved, kept when a
// later save does not compile, worn by an object with values of its own, and
// shown in the inspector as the controls its hints ask for.

const shader = 'assets/pulse.oshader';

final declared = jsonEncode([
  {
    'name': 'tint',
    'type': 'vec4',
    'instance': true,
    'hints': {'source_color': <String>[]},
    'default': [1.0, 0.5, 0.2, 1.0],
  },
  {
    'name': 'speed',
    'type': 'float',
    'instance': true,
    'hints': {
      'hint_range': ['0.0', '10.0', '0.1'],
    },
    'default': 2.0,
  },
  {'name': 'glow', 'type': 'float', 'instance': false, 'default': 1.0},
]);

ShaderCooked good() =>
    ShaderCooked(shader, package: '/cook/pulse.filamat', params: declared);

ShaderCooked bad() =>
    const ShaderCooked(shader, problem: 'assets/pulse.oshader:7: no such name');

SceneObject wearing(String id, [Map<String, Object>? values]) => SceneObject(
  id: id,
  name: id,
  kind: ObjectKind.mesh,
  materialAsset: shader,
  materialValues: values,
);

void main() {
  late Directory project;
  late List<ShaderCooked> next;
  late int cooks;
  late List<String> told;
  late SurfaceCooks surfaces;

  /// Saves the shader, so the next refresh sees a file that has changed.
  void save(String text) => File('${project.path}/$shader')
    ..createSync(recursive: true)
    ..writeAsStringSync(text);

  setUp(() {
    project = Directory.systemTemp.createTempSync('orblit_shader');
    addTearDown(() => project.deleteSync(recursive: true));
    next = [good()];
    cooks = 0;
    told = [];
    surfaces = SurfaceCooks(
      projectDirectory: project.path,
      onProblem: told.add,
      cook: (_) async {
        cooks++;
        return next;
      },
    );
    save('shader_type spatial;');
  });

  group('cooking the shaders of a project', () {
    test(
      'an object draws the cook with its defaults, then its values',
      () async {
        await surfaces.refresh();

        final plain = surfaces.surfaceFor(wearing('a'))!;
        expect(plain.package, '/cook/pulse.filamat');
        expect(plain.values['speed'], [2.0]);
        expect(plain.values['tint'], [1.0, 0.5, 0.2, 1.0]);

        final stated = surfaces.surfaceFor(wearing('b', {'speed': 4}))!;
        expect(stated.values['speed'], [4]);
        expect(stated.values['tint'], [1.0, 0.5, 0.2, 1.0]);
      },
    );

    test('nothing is cooked again until a shader changes', () async {
      await surfaces.refresh();
      await surfaces.refresh();
      expect(cooks, 1);

      save('shader_type spatial; // edited');
      await surfaces.refresh();
      expect(cooks, 2);
    });

    test('a save that does not compile leaves the last good surface', () async {
      await surfaces.refresh();

      next = [bad()];
      save('shader_type spatial; oops');
      await surfaces.refresh();

      expect(surfaces.surfaceFor(wearing('a')), isNotNull);
      expect(surfaces.problemWith(shader), contains('pulse.oshader:7'));
      expect(told, ['assets/pulse.oshader:7: no such name']);

      // The same complaint on the next save is not told again.
      save('shader_type spatial; oops again');
      await surfaces.refresh();
      expect(told, hasLength(1));

      next = [good()];
      save('shader_type spatial;');
      await surfaces.refresh();
      expect(surfaces.problemWith(shader), isNull);
    });

    test(
      'a shader nobody cooked draws nothing, and one deleted goes',
      () async {
        expect(surfaces.surfaceFor(wearing('a')), isNull);

        await surfaces.refresh();
        expect(surfaces.surfaceFor(wearing('a')), isNotNull);

        next = [];
        File('${project.path}/$shader').deleteSync();
        await surfaces.refresh();
        expect(surfaces.surfaceFor(wearing('a')), isNull);
      },
    );

    test('a cook that cannot start is told, and the editor goes on', () async {
      surfaces = SurfaceCooks(
        projectDirectory: project.path,
        onProblem: told.add,
        cook: (_) async => throw const FileSystemException('no matc'),
      );
      await surfaces.refresh();
      expect(told.single, startsWith('Could not cook the shaders'));
    });
  });

  group('an object wearing a shader', () {
    test('keeps its values through the scene file', () {
      final scene = EditorScene([
        wearing('a', {
          'speed': 4,
          'tint': [0.0, 1.0, 0.0, 1.0],
        }),
      ]);

      final back = SceneDocument.decode(SceneDocument.encode(scene)).scene;

      expect(back['a']!.wearsSurface, isTrue);
      expect(back['a']!.materialValues, {
        'speed': 4,
        'tint': [0.0, 1.0, 0.0, 1.0],
      });
    });

    test('does not write values for a picture put on after it', () {
      final object = wearing('a', {'speed': 4})
        ..materialAsset = 'assets/colormap.png';

      final back = SceneDocument.decode(
        SceneDocument.encode(EditorScene([object])),
      ).scene;

      expect(back['a']!.materialValues, isEmpty);
    });

    test('a copy has values of its own', () {
      final object = wearing('a', {'speed': 4});
      final copy = object.copyAs(id: 'b');
      copy.materialValues = {'speed': 9};
      expect(object.materialValues['speed'], 4);
    });

    test(
      'is drawn with its surface, and one that states nothing shares',
      () async {
        await surfaces.refresh();
        final scene = EditorScene([
          wearing('a'),
          wearing('b'),
          wearing('c', {'speed': 5}),
        ]);

        final drawn = scene.toRenderScene(
          OrblitCamera(position: Vector3(0, 2, 6), target: Vector3.zero()),
          projectRoot: project.path,
          surfaceOf: surfaces.surfaceFor,
        );

        final keys = [for (final object in drawn.objects) object.material];
        expect(keys.every((key) => key != null), isTrue);
        expect(keys[0], keys[1]);
        expect(keys[2], isNot(keys[0]));
        expect(drawn.materials.where((m) => m.surface != null), hasLength(2));
      },
    );

    test('keeps the mesh it came with until the shader is cooked', () {
      final drawn = EditorScene([wearing('a')]).toRenderScene(
        OrblitCamera(position: Vector3(0, 2, 6), target: Vector3.zero()),
        projectRoot: project.path,
        surfaceOf: surfaces.surfaceFor,
      );
      expect(drawn.objects.single.material, isNull);
      expect(drawn.materials.where((m) => m.surface != null), isEmpty);
    });
  });

  group('the starters in the project browser', () {
    // The one place the cook runs for real, in the isolate the editor uses.
    final matc = Platform.environment['ORBLIT_MATC'];

    test(
      'both cook with the real matc, and offer their inspector controls',
      () async {
        File('${project.path}/$shader').deleteSync();
        for (final (name, what) in [
          ('lit', NewAsset.shader),
          ('unlit', NewAsset.unlitShader),
        ]) {
          File('${project.path}/assets/$name.oshader')
              .writeAsStringSync(what.starter!);
        }
        final real = SurfaceCooks(
          projectDirectory: project.path,
          onProblem: told.add,
        );

        await real.refresh();

        expect(told, isEmpty);
        for (final name in ['lit', 'unlit']) {
          final cooked = real.cookedOf('assets/$name.oshader')!;
          expect(File(cooked.package).existsSync(), isTrue, reason: name);
          expect(
            cooked.params.where((param) => param.instance && param.isColour),
            hasLength(1),
            reason: name,
          );
        }
        expect(
          real.surfaceFor(wearing('a')..materialAsset = 'assets/unlit.oshader'),
          isNotNull,
        );
      },
      skip: matc == null ? 'ORBLIT_MATC is not set' : false,
    );
  });

  group('the shader section', () {
    late EditorScene scene;
    late History history;

    Future<void> show(WidgetTester tester) async {
      await tester.binding.setSurfaceSize(const Size(400, 900));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final section = shaderSection(cooks: surfaces);
      final target = InspectorTarget(
        sceneId: 'a',
        scene: scene,
        object: scene['a']!,
        history: history,
      );
      expect(section.appliesTo(target), isTrue);
      await tester.pumpWidget(
        MaterialApp(
          theme: orblitTheme(),
          home: Scaffold(
            body: ListenableBuilder(
              listenable: history,
              builder: (_, _) => section.build(target),
            ),
          ),
        ),
      );
    }

    setUp(() {
      scene = EditorScene([wearing('a')]);
      history = History(
        Workspace('/project')
          ..add(SceneEntry(id: 'a', name: 'A', scene: scene)),
      );
    });

    testWidgets('says the shader is cooking until it has been', (tester) async {
      await show(tester);
      expect(find.text('Cooking the shader.'), findsOneWidget);
    });

    testWidgets(
      'offers a colour for source_color and a slider for hint_range',
      (tester) async {
        await tester.runAsync(surfaces.refresh);
        await show(tester);

        expect(find.byType(ColourRow), findsOneWidget);
        expect(find.text('tint'), findsOneWidget);
        expect(find.byType(Slider), findsOneWidget);
        expect(find.text('speed'), findsOneWidget);
        // Set for the shader as a whole, so not offered for each object.
        expect(find.text('glow'), findsNothing);
      },
    );

    testWidgets('a slider edit is one step, and undo puts the value back', (
      tester,
    ) async {
      await tester.runAsync(surfaces.refresh);
      await show(tester);

      await tester.drag(find.byType(Slider), const Offset(60, 0));
      await tester.pumpAndSettle();

      final stated = scene['a']!.materialValues['speed']! as double;
      expect(stated, isNot(2.0));
      expect(history.labels.last, 'Set a speed');

      history.undo();
      await tester.pumpAndSettle();
      expect(scene['a']!.materialValues, isEmpty);
    });

    testWidgets('shows the problem at the author line when a save fails', (
      tester,
    ) async {
      await tester.runAsync(() async {
        await surfaces.refresh();
        next = [bad()];
        save('shader_type spatial; oops');
        await surfaces.refresh();
      });
      await show(tester);

      expect(find.textContaining('pulse.oshader:7'), findsOneWidget);
      // The last good cook still offers its controls.
      expect(find.byType(Slider), findsOneWidget);
    });
  });
}
