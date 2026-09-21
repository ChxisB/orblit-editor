import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/gizmo.dart';
import 'package:orblit_editor/src/editor/gizmo_registry.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/snapping.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_editor/src/editor/viewport_input.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

void main() {
  group('the order', () {
    late List<String> seen;

    /// A stage that notes everything it is shown and takes what it is told.
    ViewportStage stage(String name, {Set<ViewportPhase> takes = const {}}) =>
        (
          name: name,
          input: (gesture) {
            seen.add('$name ${gesture.phase.name}');
            return takes.contains(gesture.phase);
          },
        );

    ViewportGesture at(ViewportPhase phase) =>
        ViewportGesture(phase, Offset.zero);

    setUp(() => seen = []);

    test('the first stage to take a gesture is the only one to see it', () {
      final order = ViewportInputOrder(
        () => [
          stage('tool'),
          stage('gizmo', takes: {ViewportPhase.tap}),
          stage('selection', takes: {ViewportPhase.tap}),
        ],
      );

      expect(order.handle(at(ViewportPhase.tap)), 'gizmo');
      expect(seen, ['tool tap', 'gizmo tap']);
    });

    test('nothing taking a gesture says so', () {
      final order = ViewportInputOrder(() => [stage('tool')]);
      expect(order.handle(at(ViewportPhase.tap)), isNull);
    });

    test('a drag stays with the stage that took its start', () {
      var stages = [
        stage('tool', takes: {ViewportPhase.dragStart}),
        stage('gizmo', takes: {ViewportPhase.dragStart}),
      ];
      final order = ViewportInputOrder(() => stages);

      expect(order.handle(at(ViewportPhase.dragStart)), 'tool');
      expect(order.holding, 'tool');

      // Whatever the order has become since, the rest of the drag is the
      // tool's: half a brush stroke is not something to hand to the handles.
      stages = [stages.last];
      order
        ..handle(at(ViewportPhase.dragUpdate))
        ..handle(at(ViewportPhase.dragEnd));

      expect(seen, ['tool dragStart', 'tool dragUpdate', 'tool dragEnd']);
      expect(order.holding, isNull);
    });

    test('a drag nobody took goes nowhere', () {
      final order = ViewportInputOrder(() => [stage('selection')]);
      order.handle(at(ViewportPhase.dragStart));
      seen.clear();

      expect(order.handle(at(ViewportPhase.dragUpdate)), isNull);
      expect(order.handle(at(ViewportPhase.dragEnd)), isNull);
      expect(seen, isEmpty);
    });

    test('a hover taken ahead is a leave behind', () {
      final order = ViewportInputOrder(
        () => [
          stage('tool'),
          stage('gizmo', takes: {ViewportPhase.hover}),
          stage('selection', takes: {ViewportPhase.hover}),
        ],
      );

      expect(order.handle(at(ViewportPhase.hover)), 'gizmo');
      // The stage ahead looked and passed; the one behind is told the pointer
      // is not over anything of its own, so its highlight goes out.
      expect(seen, ['tool hover', 'gizmo hover', 'selection leave']);
    });

    test('leaving the view is a leave for everybody', () {
      final order = ViewportInputOrder(
        () => [stage('tool'), stage('gizmo'), stage('selection')],
      );
      order.leave();
      expect(seen, ['tool leave', 'gizmo leave', 'selection leave']);
    });

    test('a cancelled drag is cancelled where it was', () {
      Offset? where;
      final order = ViewportInputOrder(
        () => [
          (
            name: 'tool',
            input: (gesture) {
              where = gesture.at;
              return true;
            },
          ),
        ],
      );
      order
        ..handle(const ViewportGesture(ViewportPhase.dragStart, Offset(1, 1)))
        ..handle(const ViewportGesture(ViewportPhase.dragUpdate, Offset(4, 5)))
        ..cancel();

      expect(where, const Offset(4, 5));
      expect(order.holding, isNull);
    });

    test('a drag whose end went missing is cancelled by the next', () {
      final order = ViewportInputOrder(
        () => [
          stage('tool', takes: {ViewportPhase.dragStart}),
        ],
      );
      order
        ..handle(at(ViewportPhase.dragStart))
        ..handle(at(ViewportPhase.dragStart));

      expect(seen, ['tool dragStart', 'tool dragCancel', 'tool dragStart']);
    });
  });

  group('a scene view', () {
    late Workspace workspace;
    late History history;
    late SceneObject object;
    late Rect surface;
    late List<String?> picked;
    late int orbits;
    final camera = OrbitCamera(yaw: 0.6, pitch: 0.4, distance: 8);

    setUp(() {
      object = SceneObject(
        id: 'a',
        name: 'A',
        kind: ObjectKind.shape,
        position: Vector3(0, 0.5, 0),
        shape: Shape.of(ShapeKind.cube),
      );
      workspace = Workspace('/project')
        ..add(SceneEntry(id: 's', name: 'S', scene: EditorScene([object])));
      history = History(workspace);
      picked = [];
      orbits = 0;
    });

    Widget view({ViewportInput? tool, List<GizmoType> gizmos = const []}) =>
        MaterialApp(
          home: Scaffold(
            body: SizedBox(
              width: 800,
              height: 600,
              child: SceneViewport(
                workspace: workspace,
                camera: camera,
                onCameraChanged: (_) => orbits++,
                history: history,
                snapping: Snapping(on: false),
                primary: object.id,
                selected: {object.id},
                onPick: (id, {required add}) => picked.add(id),
                modeInput: tool,
                gizmos: gizmos,
              ),
            ),
          ),
        );

    Future<void> pump(
      WidgetTester tester, {
      ViewportInput? tool,
      List<GizmoType> gizmos = const [],
    }) async {
      await tester.pumpWidget(view(tool: tool, gizmos: gizmos));
      await tester.pump();
      surface = tester.getRect(
        find
            .descendant(
              of: find.byType(SceneViewport),
              matching: find.byType(Stack),
            )
            .first,
      );
    }

    /// Where the Y handle is on screen.
    Offset handle() {
      final projection = ViewportProjection(camera: camera, size: surface.size);
      final gizmo = Gizmo(
        mode: GizmoMode.move,
        pivot: object.position,
        projection: projection,
      );
      return surface.topLeft +
          projection.project(
            gizmo.pivot + GizmoAxis.y.direction * (gizmo.length * 0.8),
          )!;
    }

    /// A corner of the view with nothing in it.
    Offset nowhere() => surface.topLeft + const Offset(700, 80);

    Future<void> drag(WidgetTester tester, Offset from) async {
      final gesture = await tester.startGesture(
        from,
        kind: PointerDeviceKind.mouse,
      );
      for (var i = 0; i < 6; i++) {
        await gesture.moveBy(const Offset(0, -6));
        await tester.pump();
      }
      await gesture.up();
      await tester.pump();
    }

    testWidgets('a drag on a handle moves what it is on', (tester) async {
      await pump(tester);
      await drag(tester, handle());

      expect(object.position.y, greaterThan(0.5));
      expect(history.canUndo, isTrue);
    });

    testWidgets('a tool takes a drag before the handles do', (tester) async {
      final phases = <ViewportPhase>[];
      await pump(
        tester,
        tool: (gesture) {
          phases.add(gesture.phase);
          return gesture.phase == ViewportPhase.dragStart;
        },
      );
      await drag(tester, handle());

      // The whole drag went to the tool, and none of it reached the handles
      // it started on.
      expect(phases, contains(ViewportPhase.dragStart));
      expect(phases, contains(ViewportPhase.dragUpdate));
      expect(phases.last, ViewportPhase.dragEnd);
      expect(object.position.y, 0.5);
      expect(history.canUndo, isFalse);
    });

    testWidgets('a tool that passes lets the handles have it', (tester) async {
      await pump(tester, tool: (_) => false);
      await drag(tester, handle());

      expect(object.position.y, greaterThan(0.5));
    });

    testWidgets('a drag nothing else wants turns the view', (tester) async {
      await pump(tester);
      await drag(tester, nowhere());

      expect(orbits, greaterThan(0));
      expect(object.position.y, 0.5);
    });

    testWidgets('a tool is given a way into the world', (tester) async {
      ViewportGesture? given;
      await pump(
        tester,
        tool: (gesture) {
          given = gesture;
          return true;
        },
      );
      await tester.tapAt(nowhere());
      await tester.pump();

      expect(given!.phase, ViewportPhase.tap);
      expect(given!.projection!.size, surface.size);
      expect(picked, isEmpty, reason: 'the tool took the click');
    });

    testWidgets('a registered gizmo is drawn and asked before the selection', (
      tester,
    ) async {
      GizmoTarget? asked;
      await pump(
        tester,
        gizmos: [
          GizmoType(
            name: 'probe',
            appliesTo: (target) => target.object.kind == ObjectKind.shape,
            overlay: (_) => const Positioned(
              top: 0,
              left: 0,
              child: Text('probe'),
            ),
            input: (target, gesture) {
              asked = target;
              return gesture.phase == ViewportPhase.tap;
            },
          ),
        ],
      );

      expect(find.text('probe'), findsOneWidget);

      await tester.tapAt(nowhere());
      await tester.pump();

      expect(asked!.object.id, object.id);
      expect(asked!.selected, {object.id});
      expect(picked, isEmpty, reason: 'the gizmo took the click');
    });

    testWidgets('a gizmo that does not apply is neither drawn nor asked', (
      tester,
    ) async {
      var asked = false;
      await pump(
        tester,
        gizmos: [
          GizmoType(
            name: 'probe',
            appliesTo: (target) => target.object.kind == ObjectKind.light,
            overlay: (_) => const Positioned(
              top: 0,
              left: 0,
              child: Text('probe'),
            ),
            input: (_, _) => asked = true,
          ),
        ],
      );

      expect(find.text('probe'), findsNothing);

      await tester.tapAt(nowhere());
      await tester.pump();

      expect(asked, isFalse);
      expect(picked, [null], reason: 'the click reached the selection');
    });

    testWidgets('two gizmos under one name is a mistake', (tester) async {
      await tester.pumpWidget(
        view(gizmos: [GizmoType(name: 'transform', appliesTo: (_) => true)]),
      );
      expect(tester.takeException(), isStateError);
    });
  });
}
