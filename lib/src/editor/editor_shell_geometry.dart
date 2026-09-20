part of 'editor_shell.dart';

// Geometry leaving and re-entering the editor: exported to a file, rebuilt
// for the renderer, or swapped under an object that keeps its identity.

extension _Geometry on _EditorShellState {
  /// Writes a shape out, into the project's own exports folder.
  ///
  /// Inside the project rather than wherever a file dialog was last pointed:
  /// an export is a thing somebody made and will want again, and a folder
  /// beside the scenes is where they will look for it.
  Future<void> _exportShape(SceneObject object) async {
    final mesh = object.currentMesh;
    if (mesh == null || mesh.isEmpty) {
      _say('There is no geometry to export.');
      return;
    }

    final name = await promptForName(
      context,
      title: 'Export ${object.name}',
      initial: object.name,
      hint: 'Goes in exports/, as .${_format.extension}.',
      action: 'Export',
    );
    if (!mounted || name == null || name.isEmpty) return;
    if (name.contains(p.separator)) {
      _say('A file name cannot contain a path.');
      return;
    }

    final folder = Directory(p.join(widget.project.directory, 'exports'));
    final files = mesh.writeAs(
      _format,
      name: name,
      materials: [for (final one in object.surfaces) one.toGlb()],
    );

    try {
      folder.createSync(recursive: true);
      for (final file in files) {
        File(p.join(folder.path, file.name)).writeAsBytesSync(file.bytes);
      }
    } on FileSystemException catch (error) {
      _say('Could not write the export: ${error.message}');
      return;
    }

    // Both names when there are two: an OBJ without the library it names is a
    // grey model and no clue why.
    _say(
      'Exported ${files.map((one) => one.name).join(' and ')} to '
      'exports/.',
    );
  }

  /// Writes out the geometry of every shape that has changed.
  ///
  /// Called when something changes rather than when something is drawn. Doing
  /// it from the render path meant a shape was only written where there was a
  /// renderer to write it for — so on a platform Filament has not reached, or
  /// in a headless run, the file never appeared at all.
  void _refreshGeometry() {
    // Not clearing the imported-size cache. That cache is keyed on an
    // object's `meshAsset`, which a shape does not have — so clearing it here
    // never made a shape's box any newer, and did make every imported model
    // in the scene read its file from disk again. On every frame of a drag.
    for (final entry in [..._workspace.entries, _workspace.sharedEntry]) {
      final scene = entry.scene;
      if (scene == null) continue;
      for (final object in scene.objects) {
        if (object.kind != ObjectKind.shape) continue;
        _geometry.pathFor(object);
      }
    }
  }

  /// Changes a shape's numbers.
  void _reshape(SceneObject object, Shape shape) {
    final open = _workspace.sceneHolding(object.id);
    if (open == null) return;

    _run(
      SetShape(sceneId: open.id, id: object.id, name: object.name, to: shape),
    );
    _geometry.forget(object.id);
    _refreshGeometry();
  }
}
