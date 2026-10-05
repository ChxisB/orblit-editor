import 'dart:convert';
import 'dart:io';

import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/launcher/create_view.dart';
import 'package:orblit_editor/src/launcher/project.dart';
import 'package:orblit_editor/src/launcher/projects_view.dart';
import 'package:orblit_editor/src/theme/orblit_theme.dart';
import 'package:path/path.dart' as p;

Widget host(Widget child) => MaterialApp(
      theme: orblitTheme(),
      home: Scaffold(body: child),
    );

Project project(String name, String directory, {Duration ago = Duration.zero}) =>
    Project(
      name: name,
      directory: directory,
      lastOpened: DateTime.now().subtract(ago),
    );

/// The projects page at the size the editor opens at, with every callback a
/// test does not care about left to do nothing.
Future<void> showProjects(
  WidgetTester tester, {
  List<Project> projects = const [],
  ValueChanged<Project>? onOpen,
  ValueChanged<Project>? onForget,
  VoidCallback? onCreate,
  ValueChanged<ProjectTemplate>? onTemplate,
  VoidCallback? onOpenFolder,
}) async {
  await tester.binding.setSurfaceSize(const Size(1280, 800));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  await tester.pumpWidget(host(ProjectsView(
    loading: false,
    projects: projects,
    onOpen: onOpen ?? (_) {},
    onForget: onForget ?? (_) {},
    onCreate: onCreate ?? () {},
    onTemplate: onTemplate ?? (_) {},
    onOpenFolder: onOpenFolder ?? () {},
  )));
}

void main() {
  group('the projects list', () {
    testWidgets('invites you to make one when there are none', (tester) async {
      var created = false;
      await showProjects(tester, onCreate: () => created = true);

      expect(find.text('No projects yet'), findsOneWidget);

      await tester.tap(find.text('New project'));
      await tester.pump();
      expect(created, isTrue);
    });

    testWidgets('shows a project with its path and when it was opened',
        (tester) async {
      await showProjects(tester, projects: [
        project('Sunset Valley', '/tmp/sunset', ago: const Duration(hours: 2)),
      ]);

      expect(find.text('Sunset Valley'), findsOneWidget);
      expect(find.text('/tmp/sunset'), findsOneWidget);
      expect(find.text('Opened 2h ago'), findsOneWidget);
    });

    testWidgets('marks a project whose folder has gone', (tester) async {
      await showProjects(tester, projects: [
        project('Gone', '/tmp/definitely-not-here-9182'),
      ]);

      // Shown rather than hidden, so somebody who moved a folder sees why it
      // will not open instead of wondering where their work went.
      expect(find.text('moved'), findsOneWidget);
    });

    testWidgets('narrows to the projects whose name has what was typed',
        (tester) async {
      await showProjects(tester, projects: [
        project('Sunset Valley', '/tmp/sunset'),
        project('Runner', '/tmp/runner'),
      ]);

      await tester.enterText(find.byType(TextField), '  SUN ');
      await tester.pump();
      expect(find.text('Sunset Valley'), findsOneWidget);
      expect(find.text('Runner'), findsNothing);

      await tester.enterText(find.byType(TextField), 'zzz');
      await tester.pump();
      expect(find.text('Nothing matches'), findsOneWidget);
      expect(find.text('No projects yet'), findsNothing);

      await tester.enterText(find.byType(TextField), '');
      await tester.pump();
      expect(find.text('Sunset Valley'), findsOneWidget);
      expect(find.text('Runner'), findsOneWidget);
    });

    testWidgets('opens a project from its card', (tester) async {
      Project? opened;
      await showProjects(
        tester,
        projects: [project('Runner', '/tmp/runner')],
        onOpen: (project) => opened = project,
      );

      await tester.tap(find.text('Runner'));

      expect(opened?.name, 'Runner');
    });

    testWidgets('forgets a project from the button on its card, not opens it',
        (tester) async {
      Project? opened;
      Project? forgotten;
      await showProjects(
        tester,
        projects: [project('Runner', '/tmp/runner')],
        onOpen: (project) => opened = project,
        onForget: (project) => forgotten = project,
      );

      // The button is only there while the pointer is over the card.
      expect(find.byTooltip('Remove from this list'), findsNothing);
      final pointer = await tester.createGesture(kind: PointerDeviceKind.mouse);
      await pointer.addPointer(location: Offset.zero);
      addTearDown(pointer.removePointer);
      await pointer.moveTo(tester.getCenter(find.text('Runner')));
      await tester.pump();

      await tester.tap(find.byTooltip('Remove from this list'));

      expect(forgotten?.name, 'Runner');
      expect(opened, isNull);
    });
  });

  group('starting a project', () {
    testWidgets('has a card for every template, and a tap names it',
        (tester) async {
      ProjectTemplate? chosen;
      await showProjects(tester, onTemplate: (template) => chosen = template);

      for (final template in ProjectTemplate.values) {
        expect(find.text(template.label), findsOneWidget);
      }

      await tester.tap(find.text(ProjectTemplate.thirdPerson.label));
      expect(chosen, ProjectTemplate.thirdPerson);
    });

    testWidgets('can open a folder that already holds a project',
        (tester) async {
      var asked = false;
      await showProjects(tester, onOpenFolder: () => asked = true);

      await tester.tap(find.text('Open a folder'));

      expect(asked, isTrue);
    });
  });

  group('creating a project', () {
    testWidgets('shows where the folder will land before making it',
        (tester) async {
      await tester.pumpWidget(host(CreateView(
        store: ProjectStore(),
        onCancel: () {},
        onCreated: (_) {},
        onFailed: (_, _) {},
      )));
      await tester.pumpAndSettle();

      // The default name, turned into the folder name it will become.
      expect(
        find.textContaining('untitled-project'),
        findsOneWidget,
        reason: 'a name is for people and a path is for filesystems, so the '
            'translation between them should not be a surprise',
      );
    });

    testWidgets('offers every template, with one chosen', (tester) async {
      await tester.pumpWidget(host(CreateView(
        store: ProjectStore(),
        onCancel: () {},
        onCreated: (_) {},
        onFailed: (_, _) {},
      )));
      await tester.pumpAndSettle();

      for (final template in ProjectTemplate.values) {
        expect(find.text(template.label), findsOneWidget);
      }
    });

    testWidgets('starts with the template that was asked for', (tester) async {
      await tester.pumpWidget(host(CreateView(
        store: ProjectStore(),
        template: ProjectTemplate.empty,
        onCancel: () {},
        onCreated: (_) {},
        onFailed: (_, _) {},
      )));
      await tester.pumpAndSettle();

      // The chosen card is the one washed in ember.
      Finder card(ProjectTemplate template) => find.ancestor(
            of: find.text(template.label),
            matching: find.byWidgetPredicate((widget) =>
                widget is AnimatedContainer &&
                (widget.decoration as BoxDecoration?)?.color ==
                    OrblitColors.emberWash),
          );
      expect(card(ProjectTemplate.empty), findsOneWidget);
      expect(card(ProjectTemplate.scene), findsNothing);
    });

    testWidgets('refuses a nameless project rather than making one',
        (tester) async {
      String? complaint;
      await tester.pumpWidget(host(CreateView(
        store: ProjectStore(),
        onCancel: () {},
        onCreated: (_) {},
        onFailed: (title, _) => complaint = title,
      )));
      await tester.pumpAndSettle();

      await tester.enterText(find.byType(TextField).first, '   ');
      await tester.tap(find.text('Create project'));
      await tester.pump();

      expect(complaint, 'A project needs a name');
    });
  });

  group('a project on disk', () {
    late Directory temp;

    setUp(() => temp = Directory.systemTemp.createTempSync('orblit_test'));
    tearDown(() => temp.deleteSync(recursive: true));

    test('is a folder with a project file, scenes and assets', () async {
      final store = ProjectStore();
      // Written directly rather than through create(), which also touches the
      // recents list and would need a platform directory this test has no
      // business creating.
      final directory = Directory(p.join(temp.path, 'my-game'))
        ..createSync(recursive: true);
      final created = Project(
        name: 'My Game',
        directory: directory.path,
        lastOpened: DateTime.now(),
      );
      File(p.join(directory.path, projectFileName))
          .writeAsStringSync(jsonEncode(created.toJson()));

      final reopened = store.open(directory.path);
      expect(reopened, isNotNull);
      expect(reopened!.name, 'My Game');
      expect(reopened.exists, isTrue);
    });

    test('a folder without a project file opens as nothing', () {
      expect(ProjectStore().open(temp.path), isNull);
    });

    test('a corrupt project file is refused, not half-read', () {
      File(p.join(temp.path, projectFileName)).writeAsStringSync('{not json');
      expect(ProjectStore().open(temp.path), isNull);
    });

    test('a path under home is shortened for display', () {
      final home = Platform.environment['HOME'];
      if (home == null) return;
      expect(
        project('X', p.join(home, 'dev', 'game')).displayPath,
        '~/dev/game',
      );
    });
  });
}
