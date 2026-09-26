import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/commands.dart';
import 'package:orblit_editor/src/editor/gizmo.dart';
import 'package:orblit_editor/src/editor/gizmo_registry.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/editor/joint_gizmo.dart';
import 'package:orblit_editor/src/editor/joint_section.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:vector_math/vector_math_64.dart' hide Colors;

// A joint on an object: carried through the editor, holding the bodies its
// place in the tree says it holds, and drawn with its frame and limits.

/// A door on a hinge at its edge, an arm with a forearm under it, and a lamp
/// with nothing to hold.
EditorScene scene({doc.JointComponent? hinge, doc.JointComponent? elbow}) =>
    EditorScene([
      SceneObject(
        id: 'door',
        name: 'Door',
        kind: ObjectKind.mesh,
        position: Vector3(0, 1, 0),
        components: {doc.SceneComponents.body: doc.BodyComponent()},
      ),
      SceneObject(
        id: 'hinge',
        name: 'Hinge',
        kind: ObjectKind.group,
        parentId: 'door',
        position: Vector3(-0.5, 0, 0),
        components: {doc.SceneComponents.joint: ?hinge},
      ),
      SceneObject(
        id: 'arm',
        name: 'Arm',
        kind: ObjectKind.mesh,
        position: Vector3(3, 1, 0),
        components: {doc.SceneComponents.body: doc.BodyComponent()},
      ),
      SceneObject(
        id: 'forearm',
        name: 'Forearm',
        kind: ObjectKind.mesh,
        parentId: 'arm',
        position: Vector3(0, -1, 0),
        components: {
          doc.SceneComponents.body: doc.BodyComponent(),
          doc.SceneComponents.joint: ?elbow,
        },
      ),
      SceneObject(id: 'lamp', name: 'Lamp', kind: ObjectKind.group),
    ]);

EditorScene reopened(EditorScene scene) =>
    SceneDocument.decode(SceneDocument.encode(scene)).scene;

void main() {
  group('a joint in a scene file', () {
    test('comes back as it was saved', () {
      final hinge = doc.JointComponent(
        limits: const {doc.JointAxis.aboutX: doc.JointRange(-10, 95)},
        speed: 30,
        strength: 5,
        breakingTorque: 400,
      );

      final back = reopened(scene(hinge: hinge));

      expect(jointOf(back['hinge']!)!.toJson(), hinge.toJson());
      expect(jointOf(back['door']!), isNull);
    });
  });

  group('what a joint holds', () {
    test('under a body, that body to the world', () {
      expect(jointEndsIn(scene(), 'hinge'), (body: 'door', holder: null));
    });

    test('on a body under another, the one to the other', () {
      expect(jointEndsIn(scene(), 'forearm'), (body: 'forearm', holder: 'arm'));
    });

    test('nothing with no body at or above it', () {
      expect(jointEndsIn(scene(), 'lamp'), (body: null, holder: null));
    });

    test('a body middle is where its body is drawn', () {
      expect(bodyMiddleIn(scene(), 'forearm'), Vector3(3, 0, 0));
      expect(bodyMiddleIn(scene(), 'lamp'), isNull);
    });
  });

  group('the joint section', () {
    InspectorTarget targetOf(EditorScene scene, String id) => InspectorTarget(
      sceneId: 'a',
      scene: scene,
      object: scene[id]!,
      history: History(
        Workspace('/project')
          ..add(SceneEntry(id: 'a', name: 'A', scene: scene)),
      ),
    );

    test('is offered on a body and under one, and not elsewhere', () {
      final section = jointSection();
      final plain = scene();

      expect(section.appliesTo(targetOf(plain, 'door')), isTrue);
      expect(section.appliesTo(targetOf(plain, 'hinge')), isTrue);
      expect(section.appliesTo(targetOf(plain, 'lamp')), isFalse);
    });

    test('is still shown for a joint a file put where it holds nothing', () {
      final lamp = scene()
        ..['lamp']!.components[doc.SceneComponents.joint] =
            doc.JointComponent();
      expect(jointSection().appliesTo(targetOf(lamp, 'lamp')), isTrue);
    });
  });

  group('editing a joint', () {
    test('adding one is undone by taking it off again', () {
      final door = scene();
      final history = History(
        Workspace('/project')..add(SceneEntry(id: 'a', name: 'A', scene: door)),
      );

      history.run(
        SetObjectComponent(
          sceneId: 'a',
          id: 'hinge',
          label: 'Add joint',
          type: doc.SceneComponents.joint,
          from: null,
          to: doc.JointComponent(),
        ),
      );
      expect(jointOf(door['hinge']!), isNotNull);

      history.undo();
      expect(jointOf(door['hinge']!), isNull);
    });
  });

  group('the joint gizmo', () {
    const size = Size(800, 600);
    final projection = ViewportProjection(camera: OrbitCamera(), size: size);

    GizmoTarget targetOf(EditorScene scene, Set<String> selected) =>
        GizmoTarget(
          object: scene[selected.first]!,
          scene: scene,
          selected: selected,
          projection: projection,
        );

    /// Where the joint's lines land on screen.
    Future<Rect> drawn(
      WidgetTester tester,
      EditorScene scene, [
      String id = 'hinge',
    ]) async {
      await tester.pumpWidget(
        Directionality(
          textDirection: TextDirection.ltr,
          child: SizedBox.fromSize(
            size: size,
            child: Stack(
              children: [
                jointGizmo().overlay!(targetOf(scene, {id})),
              ],
            ),
          ),
        ),
      );
      late Rect bounds;
      expect(
        find.byType(CustomPaint),
        paints..something((method, arguments) {
          if (method != #drawPath) return false;
          bounds = (arguments[0] as Path).getBounds();
          return true;
        }),
      );
      return bounds;
    }

    test('is there for a selection with a joint, and only then', () {
      final door = scene(hinge: doc.JointComponent());
      final gizmo = jointGizmo();

      expect(gizmo.appliesTo(targetOf(door, {'door'})), isFalse);
      expect(gizmo.appliesTo(targetOf(door, {'hinge'})), isTrue);
      expect(gizmo.appliesTo(targetOf(door, {'door', 'hinge'})), isTrue);
    });

    testWidgets('joins the joint to the body it holds', (tester) async {
      final bounds = await drawn(tester, scene(hinge: doc.JointComponent()));
      final middle = projection.project(Vector3(0, 1, 0))!;

      expect(bounds.inflate(1).contains(middle), isTrue);
    });

    testWidgets('joins a forearm to the arm it hangs from', (tester) async {
      final bounds = await drawn(
        tester,
        scene(elbow: doc.JointComponent(kind: doc.JointKind.point)),
        'forearm',
      );
      final arm = projection.project(Vector3(3, 1, 0))!;

      expect(bounds.inflate(1).contains(arm), isTrue);
    });

    testWidgets('a limited hinge draws the arc it may turn through', (
      tester,
    ) async {
      // A hinge at the door's edge, turning about its x axis: free, a whole
      // ring round it; limited to a few degrees up from y, a sliver.
      final free = await drawn(tester, scene(hinge: doc.JointComponent()));
      final limited = await drawn(
        tester,
        scene(
          hinge: doc.JointComponent(
            limits: const {doc.JointAxis.aboutX: doc.JointRange(0, 10)},
          ),
        ),
      );

      expect(free.bottom, greaterThan(limited.bottom + 1));
    });

    testWidgets('a cone opens with its swing', (tester) async {
      Future<Rect> coneOf(double swing) => drawn(
        tester,
        scene(
          hinge: doc.JointComponent(kind: doc.JointKind.cone, swing: swing),
        ),
      );
      final narrow = await coneOf(15);
      final wide = await coneOf(60);

      expect(wide.height, greaterThan(narrow.height));
    });

    testWidgets("a slider's travel is metres in the world", (tester) async {
      Future<Rect> sliderOf(double reach) => drawn(
        tester,
        scene(
          hinge: doc.JointComponent(
            kind: doc.JointKind.slider,
            limits: {doc.JointAxis.alongX: doc.JointRange(-reach, reach)},
          ),
        ),
      );
      final short = await sliderOf(0.5);
      final long = await sliderOf(3);
      final end = projection.project(Vector3(-3.5, 1, 0))!;

      expect(long.width, greaterThan(short.width));
      expect(long.inflate(1).contains(end), isTrue);
    });
  });
}
