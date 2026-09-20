part of 'editor_shell.dart';

// The files an object can be dressed in or wired to: interfaces, built
// scripts, data objects, and an asset dropped onto the scene.

extension _Documents on _EditorShellState {
  /// Opens the project in a code editor, optionally on one file.
  ///
  /// The project folder rather than the single file, so imports resolve and
  /// the type definitions next door are findable.
  void _openInCode([String? file]) {
    final problem = CodeEditor.open(widget.project.directory, file: file);
    if (problem != null) {
      _say(problem);
      return;
    }
    _say(
      file == null
          ? 'Opened the project in ${CodeEditor.available ?? 'your editor'}.'
          : 'Opened ${p.basename(file)} in '
                '${CodeEditor.available ?? 'your editor'}.',
    );
  }

  /// Puts a texture on the selected object.
  ///
  /// The case this is for is an asset pack that ships a model and its colour
  /// map as separate files: the model loads grey, because its file names no
  /// texture, and the fix used to be a round trip through a modelling package
  /// to bind the two and export again. The renderer binds them itself — a
  /// material named on an object overrides whatever its mesh brought — so
  /// this only has to say which texture.
  void _applyTexture(String path) {
    final open = _current;
    final scene = open?.scene;
    if (open == null || scene == null) {
      _say('There is no scene loaded to texture anything in.');
      return;
    }
    final id = _primary;
    final object = id == null ? null : scene[id];
    if (object == null) {
      _say('Select an object first; a texture goes on whatever is selected.');
      return;
    }
    if (!object.isDrawable) {
      _say(
        '${object.name} is a ${object.kind.name}, and has nothing to '
        'draw a texture on.',
      );
      return;
    }
    _run(
      SetMaterialAsset(
        sceneId: open.id,
        id: object.id,
        name: object.name,
        from: object.materialAsset,
        to: _assets.relative(path),
      ),
    );
  }

  void _dropAsset(String path) {
    final kind = AssetKind.of(path);

    if (kind == AssetKind.scene) {
      _openScene(path);
      return;
    }
    if (kind == AssetKind.prefab) {
      _placePrefab(path);
      return;
    }
    if (kind == AssetKind.dataObject) {
      _attachData(path);
      return;
    }
    if (kind == AssetKind.canvas) {
      _putInterfaceOnScene(path);
      return;
    }
    if (kind == AssetKind.texture) {
      _applyTexture(path);
      return;
    }
    if (kind != AssetKind.mesh) {
      _say(
        '${p.basename(path)} is a ${kind.label.toLowerCase()}. '
        'Meshes, prefabs and scenes are what a scene takes; a texture '
        'goes on whatever is selected.',
      );
      return;
    }

    final open = _current;
    final scene = open?.scene;
    if (open == null || scene == null) {
      _say('There is no scene loaded to add to.');
      return;
    }

    final object = SceneObject(
      id: 'o${DateTime.now().microsecondsSinceEpoch}',
      name: _uniqueName(scene, p.basenameWithoutExtension(path)),
      kind: ObjectKind.mesh,
      meshAsset: _assets.relative(path),
    );

    _run(AddObject(object, sceneId: open.id));
    _select(object.id);
  }

  // ---- panels ----

  /// Puts an interface on the open scene.
  ///
  /// Onto the selected canvas if there is one, and onto a new canvas object if
  /// there is not. Dropping a file and being told to make an object first
  /// would be the editor knowing what somebody meant and refusing to do it.
  void _putInterfaceOnScene(String path) {
    final open = _working;
    final scene = open?.scene;
    if (open == null || scene == null) {
      _say('There is no scene loaded to add to.');
      return;
    }

    final relative = _assets.relative(path);
    final selected = _primary == null ? null : scene[_primary!];

    if (selected != null && selected.kind == ObjectKind.canvas) {
      _run(
        SetInterface(
          sceneId: open.id,
          id: selected.id,
          name: selected.name,
          to: relative,
        ),
      );
      _say('${selected.name} now shows ${p.basename(path)}.');
      return;
    }

    final object = SceneObject(
      id: _nextObjectId(),
      name: _uniqueName(scene, p.basenameWithoutExtension(path)),
      kind: ObjectKind.canvas,
      interfaceAsset: relative,
    );
    _run(AddObject(object, sceneId: open.id));
    _select(object.id);
  }

  /// The interface the open scene puts on screen, if any.
  ///
  /// The first visible canvas object, since a screen shows one interface at a
  /// time. Two canvases both visible is a scene saying two things, and picking
  /// the first is at least the one nearest the top of the tree.
  UiDocument? get _sceneInterface {
    final scene = _current?.scene;
    if (scene == null) return null;

    for (final object in scene.objects) {
      if (object.kind != ObjectKind.canvas) continue;
      if (!object.visible || !scene.isShown(object.id)) continue;

      final path = object.interfaceAsset;
      if (path == null) continue;
      return _interfaces.putIfAbsent(path, () => _readInterface(path));
    }
    return null;
  }

  UiDocument? _readInterface(String relative) {
    try {
      final file = File(p.join(widget.project.directory, relative));
      if (!file.existsSync()) return null;
      return UiDocument.read(file.readAsStringSync());
    } on FileSystemException {
      return null;
    }
  }

  /// Opens a canvas for laying out.
  ///
  /// A screen of its own rather than a panel. A canvas is a design surface at
  /// a fixed size, and one squeezed into the space beside a 3D viewport is a
  /// view too small to lay anything out in next to a viewport nobody is
  /// looking at.
  Future<void> _openInterface(String path) async {
    final File file = File(path);
    if (!file.existsSync()) {
      _say('${p.basename(path)} is not in the project any more.');
      return;
    }

    final document = UiDocument.read(file.readAsStringSync());
    if (document == null) {
      _say('${p.basename(path)} is not a readable interface.');
      return;
    }

    await Navigator.of(context).push<void>(
      MaterialPageRoute(
        builder: (context) =>
            UiEditor(path: path, document: document, onProblem: _say),
      ),
    );
    // Read again: the scene is showing what was on disk before it was edited.
    if (mounted) setState(() => _interfaces.clear());
  }

  // ---- scripts ----

  /// Compiles a C++ script and says what the compiler said.
  ///
  /// A success is a line in the status bar; a failure is a panel, because a
  /// compiler error is several lines long and the first of them is rarely the
  /// useful one.
  Future<void> _buildScript(String path) async {
    final name = p.basename(path);
    _say('Building $name…');

    // Off the frame: a compile is a second or two, and a frozen editor for
    // that long reads as a crash.
    final built = await Future(() => _builder.build(path));
    if (!mounted) return;

    if (built.ok) {
      _say(
        'Built $name.${built.output.isEmpty ? '' : ' With warnings.'}',
        level: built.output.isEmpty ? LogLevel.info : LogLevel.warning,
        detail: built.output,
      );
      if (built.output.isEmpty) return;
    } else {
      _say('$name did not build.', level: LogLevel.error, detail: built.output);
    }

    await showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: OrblitColors.surface,
        title: Text(
          built.ok ? '$name built, with warnings' : '$name did not build',
          style: OrblitText.title,
        ),
        content: SizedBox(
          width: 640,
          height: 320,
          child: SingleChildScrollView(
            child: SelectableText(
              built.output.isEmpty
                  ? 'The compiler said nothing.'
                  : built.output,
              style: OrblitText.mono.copyWith(fontSize: 11.5),
            ),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('Close'),
          ),
        ],
      ),
    );
  }

  // ---- data objects ----

  /// Points the selection at a data object.
  ///
  /// Everything selected, not just one: attaching the same settings to forty
  /// crates is the case this exists for, and doing it one at a time forty
  /// times is not an improvement on typing the number forty times.
  void _attachData(String path) {
    final open = _working;
    final scene = open?.scene;
    if (open == null || scene == null) return;

    final relative = _assets.relative(path);
    if (_data[relative] == null) {
      _say('${p.basename(path)} is not a readable data object.');
      return;
    }

    final wanted = [
      for (final id in _selected)
        if (scene[id] case final object?)
          if (!object.data.contains(relative)) object,
    ];

    if (wanted.isEmpty) {
      _say(
        _selected.isEmpty
            ? 'Select something first, then drop ${p.basename(path)} on it.'
            : 'Already using ${p.basename(path)}.',
      );
      return;
    }

    for (final object in wanted) {
      _run(
        SetDataLinks(
          sceneId: open.id,
          id: object.id,
          name: object.name,
          paths: [...object.data, relative],
          what: 'Add ${p.basenameWithoutExtension(path)}',
        ),
      );
    }
    _say(
      wanted.length == 1
          ? 'Added ${p.basename(path)} to ${wanted.single.name}.'
          : 'Added ${p.basename(path)} to ${wanted.length} objects.',
    );
  }

  void _detachData(String id, String relative) {
    final open = _workspace.sceneHolding(id);
    final object = open?.scene?[id];
    if (open == null || object == null) return;

    _run(
      SetDataLinks(
        sceneId: open.id,
        id: id,
        name: object.name,
        paths: [
          for (final path in object.data)
            if (path != relative) path,
        ],
        what: 'Remove ${p.basenameWithoutExtension(relative)}',
      ),
    );
  }

  /// Shows a data object in the inspector, from wherever it was named.
  void _showData(String relative) {
    if (_data[relative] == null) {
      _say('$relative is not in the project any more.');
      return;
    }
    setState(() => _dataAsset = relative);
  }

  /// Writes the bindings a script reads a data object through.
  ///
  /// Both languages from the one declaration, which is the piece that makes
  /// three front ends feel like one: a field renamed here breaks the build of
  /// everything that reads it, in TypeScript and in C++, rather than quietly
  /// returning nothing at runtime. The alternative is a string key and hope.
  void _exportBindings(String relative) {
    final data = _data[relative];
    if (data == null) return;

    final name = p.basenameWithoutExtension(relative);
    final folder = p.join(widget.project.directory, p.dirname(relative));

    final written = <String>[];
    for (final one in [
      (file: '$name.d.ts', text: data.toTypeScript(name)),
      (file: '$name.h', text: data.toCpp(name)),
    ]) {
      final path = p.join(folder, one.file);
      try {
        File(path).writeAsStringSync(one.text);
      } on FileSystemException catch (error) {
        _say(
          'Could not write ${one.file}: '
          '${error.osError?.message ?? error.message}',
          level: LogLevel.error,
        );
        return;
      }
      written.add(one.file);
    }
    _say('Wrote ${written.join(' and ')}.');
  }

  // ---- prefabs ----
}
