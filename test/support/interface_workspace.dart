import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/interface_bench.dart';
import 'package:orblit_editor/src/editor/interface_mode.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_editor/src/theme/orblit_theme.dart';
import 'package:orblit_ui/orblit_ui.dart';
import 'package:path/path.dart' as p;

/// Where [openInterface] writes the interface it lays out.
String interfacePath(Directory folder) => p.join(folder.path, 'menu.oui');

/// The Interface workspace's panels where the dock puts them, laying out
/// [document].
///
/// Without the rest of the editor, so a test of laying out drives only what
/// lays out. Undo and save are the editor's, so a test calls them on the
/// bench's history and the bench.
Future<InterfaceBench> openInterface(
  WidgetTester tester, {
  required Directory folder,
  required UiDocument document,
  Size window = const Size(1600, 950),
}) async {
  final path = interfacePath(folder);
  File(path).writeAsStringSync(document.toText());
  final history = History(Workspace(folder.path));
  final bench = InterfaceBench(history: history);
  addTearDown(() {
    bench.dispose();
    history.dispose();
  });
  expect(bench.openFile(path), isNull);

  await tester.binding.setSurfaceSize(window);
  addTearDown(() => tester.binding.setSurfaceSize(null));
  await tester.pumpWidget(
    MaterialApp(
      theme: orblitTheme(),
      home: Scaffold(
        body: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            SizedBox(width: 248, child: InterfaceElements(bench: bench)),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  SizedBox(
                    height: 36,
                    child: InterfaceShelf(bench: bench, onNew: () {}),
                  ),
                  Expanded(
                    child: InterfaceCanvas(bench: bench, onNew: () {}),
                  ),
                ],
              ),
            ),
            SizedBox(width: 296, child: InterfaceDesign(bench: bench)),
          ],
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return bench;
}

/// What [openInterface]'s file says once the bench has saved it.
UiDocument savedInterface(InterfaceBench bench, Directory folder) {
  expect(bench.saveAll(), isEmpty);
  final read = UiDocument.read(File(interfacePath(folder)).readAsStringSync());
  expect(read, isNotNull);
  return read!;
}
