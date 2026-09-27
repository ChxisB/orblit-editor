import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:orblit_ui/orblit_ui.dart';
import 'package:path/path.dart' as p;

import 'history.dart';

/// What the undo stack files an interface's changes under.
///
/// Prefixed the way a clip's are, so no scene can ever be given the same one.
String interfaceSceneId(String path) => 'interface:$path';

/// The elements somebody can put on a canvas.
///
/// A short list on purpose. Every one of these is a real Flutter widget with
/// real layout behind it, and a palette of forty things that mostly wrap each
/// other is a palette nobody reads to the end of.
enum UiElement {
  column('Column', Icons.view_agenda_outlined, 'column', 'gap-2'),
  row('Row', Icons.view_column_outlined, 'row', 'gap-2 items-center'),
  stack('Stack', Icons.layers_outlined, 'stack', ''),
  box('Box', Icons.crop_square, 'box', 'p-4 bg-slate-800 rounded-lg'),
  text('Text', Icons.text_fields, 'text', 'text-base text-slate-100'),
  button(
    'Button',
    Icons.smart_button_outlined,
    'button',
    'px-4 py-2 bg-ember-500 text-white rounded',
  ),
  field('Field', Icons.input, 'field', 'px-3 py-2 bg-slate-900 rounded'),
  image('Image', Icons.image_outlined, 'image', 'w-32 h-32'),
  spacer('Spacer', Icons.expand, 'spacer', '');

  const UiElement(this.label, this.icon, this.type, this.classes);

  final String label;
  final IconData icon;
  final String type;

  /// What it looks like the moment it is added.
  ///
  /// Not nothing: an element with no styling is an invisible element, and an
  /// invisible element added to a canvas reads as a button that did not work.
  final String classes;

  UiNode make() => UiNode(
    type: type,
    classes: classes,
    text: switch (this) {
      UiElement.text => 'Text',
      UiElement.button => 'Button',
      UiElement.field => '',
      _ => null,
    },
  );
}

/// What an element is called in the tree and next to Undo.
///
/// The words when it has any, since "Start game" says more about which
/// button this is than "button" does.
String uiNodeName(UiNode node) {
  final words = node.text?.trim() ?? '';
  return words.isEmpty ? node.type : words;
}

/// A class list with any direction it named taken out.
///
/// The type says `row` now, and a leftover `stack` or `md:row` in the classes
/// is applied over the top of it. The element would keep its old layout and
/// the change would look like it did nothing.
String flowing(String classes) {
  const directions = {'col', 'column', 'stack', 'row'};
  final kept = [
    for (final name in classes.split(RegExp(r'\s+')))
      if (name.isNotEmpty)
        if (!directions.contains(name) &&
            !directions.contains(name.split(':').last))
          name,
  ];
  if (!kept.any((name) => name.startsWith('gap-'))) kept.add('gap-4');
  return kept.join(' ');
}

/// An interface open in the Interface workspace.
class OpenInterface {
  OpenInterface({required this.path, required this.document});

  final String path;

  /// As it is now, edits and all. Only ever replaced whole, by an
  /// [InterfaceEdit].
  UiDocument document;

  /// The undo stack's stamp for this interface when it was last written.
  int savedStamp = 0;

  String get id => interfaceSceneId(path);

  String get name => p.basename(path);
}

/// The interfaces being laid out, and how the one shown is being looked at.
///
/// A notifier rather than state inside a panel: the canvas, the element tree
/// and the design panel all read it, a panel can be closed and opened again
/// without losing the selection, and each edit is a step on the one undo
/// stack the rest of the editor uses.
class InterfaceBench extends ChangeNotifier {
  InterfaceBench({required this.history});

  final History history;

  final List<OpenInterface> _open = [];

  OpenInterface? operator [](String path) {
    final key = p.normalize(path);
    for (final one in _open) {
      if (one.path == key) return one;
    }
    return null;
  }

  String? _shown;

  /// The interface the workspace shows, or null.
  OpenInterface? get shown => _shown == null ? null : this[_shown!];

  UiDocument? get document => shown?.document;

  List<int>? _selected = const [];

  /// The selected element's path from the root, or null for none.
  List<int>? get selected => _selected;

  set selected(List<int>? path) {
    if (listEquals(path, _selected)) return;
    _selected = path;
    notifyListeners();
  }

  List<int>? _hovered;

  List<int>? get hovered => _hovered;

  set hovered(List<int>? path) {
    if (listEquals(path, _hovered)) return;
    _hovered = path;
    notifyListeners();
  }

  UiNode? get element {
    final selected = _selected;
    return selected == null ? null : document?.root.at(selected);
  }

  bool _previewing = false;

  /// Whether the canvas shows what the game shows, with nothing drawn over
  /// it and nothing to select.
  bool get previewing => _previewing;

  set previewing(bool value) {
    if (value == _previewing) return;
    _previewing = value;
    notifyListeners();
  }

  bool _outlines = true;

  bool get outlines => _outlines;

  set outlines(bool value) {
    if (value == _outlines) return;
    _outlines = value;
    notifyListeners();
  }

  bool _guides = true;

  bool get guides => _guides;

  set guides(bool value) {
    if (value == _guides) return;
    _guides = value;
    notifyListeners();
  }

  bool _columns = false;

  /// Whether the column grid is drawn.
  bool get columns => _columns;

  set columns(bool value) {
    if (value == _columns) return;
    _columns = value;
    notifyListeners();
  }

  /// The device being previewed, upright, or null for the canvas's own size.
  ///
  /// Not part of the document. Which device somebody is looking at while they
  /// work is a view, the same way the outlines are. Saving it would mean two
  /// people opening the same file and disagreeing about what it is.
  Size? _device;

  Size? get device => _device;

  set device(Size? size) {
    if (size == _device) return;
    _device = size;
    notifyListeners();
  }

  /// Whether it is being held sideways.
  ///
  /// Kept apart from the device rather than folded into it, so turning a phone
  /// on its side and then picking a tablet gives a tablet on its side. A
  /// single flipped size would forget which way up somebody was working.
  bool _landscape = false;

  bool get landscape => _landscape;

  set landscape(bool value) {
    if (value == _landscape) return;
    _landscape = value;
    notifyListeners();
  }

  /// The screen to lay out on: the device the way it is being held, or null
  /// for the canvas's own size.
  Size? get preview {
    final document = this.document;
    if (document == null || (_device == null && !_landscape)) return null;
    final base =
        _device ?? Size(document.canvas.width, document.canvas.height);
    return _landscape ? Size(base.height, base.width) : base;
  }

  /// Shows the interface at [path], reading it first if it is not open, and
  /// says why it could not be.
  String? openFile(String path) {
    if (this[path] != null) {
      show(path);
      return null;
    }
    final name = p.basename(path);
    final UiDocument? document;
    try {
      document = UiDocument.read(File(path).readAsStringSync());
    } on PathNotFoundException {
      return '$name is not in the project any more.';
    } on FileSystemException catch (error) {
      return '$name could not be read: ${error.message}';
    }
    if (document == null) return '$name is not a readable interface.';
    _open.add(OpenInterface(path: p.normalize(path), document: document));
    show(path);
    return null;
  }

  void show(String path) {
    final key = p.normalize(path);
    if (_shown == key || this[key] == null) return;
    _shown = key;
    _selected = const [];
    _hovered = null;
    notifyListeners();
  }

  bool isUnsaved(OpenInterface open) =>
      history.stampFor(open.id) != open.savedStamp;

  bool get anyUnsaved => _open.any(isUnsaved);

  /// Writes every interface with changes, and says which could not be
  /// written.
  List<String> saveAll() => [
    for (final one in _open)
      if (isUnsaved(one)) ?save(one),
  ];

  /// Writes [open], or says why it could not be.
  String? save(OpenInterface open) {
    try {
      File(open.path).writeAsStringSync(open.document.toText());
    } on FileSystemException catch (error) {
      return 'Could not save ${open.name}: '
          '${error.osError?.message ?? error.message}';
    }
    open.savedStamp = history.stampFor(open.id);
    notifyListeners();
    return null;
  }

  /// Ends a run of edits that merge, such as the words typed into one field
  /// or one drag of a slider.
  void settle() => history.seal();

  /// Changes the shown interface through the undo stack.
  ///
  /// [select] is the selection afterwards, and is the one before when not
  /// given. A [gesture] ties a run of these into one step; say [onlyMoves]
  /// when all it does is move an element.
  void edit(
    String label,
    UiDocument Function(UiDocument document) change, {
    List<int>? select,
    Object? gesture,
    bool onlyMoves = false,
  }) {
    final shown = this.shown;
    if (shown == null) return;
    final to = change(shown.document);
    if (identical(to, shown.document)) return;
    history.run(
      InterfaceEdit(
        bench: this,
        path: shown.path,
        label: label,
        from: shown.document,
        to: to,
        selectedBefore: _selected,
        selectedAfter: select ?? _selected,
        gesture: gesture,
        onlyMoves: onlyMoves,
      ),
    );
  }

  /// Changes the selected element.
  ///
  /// A [gesture] ties the edits into one step while they are made to the
  /// same element, so the words typed into a field are undone together
  /// rather than a letter at a time.
  void editElement(
    String label,
    UiNode Function(UiNode element) change, {
    Object? gesture,
  }) {
    final path = _selected;
    final element = this.element;
    if (path == null || element == null) return;
    edit(
      '$label on ${uiNodeName(element)}',
      (document) => document.copyWith(
        root: document.root.replaceAt(path, change(element)),
      ),
      gesture: gesture == null ? null : (gesture, path.join('.')),
    );
  }

  void setCanvas(String label, UiCanvas canvas, {Object? gesture}) => edit(
    label,
    (document) => document.copyWith(canvas: canvas),
    gesture: gesture,
  );

  /// Adds [what] inside the selection, which is what "add" means when
  /// something is selected and there is nowhere else it could sensibly go.
  void add(UiElement what) {
    final into = _selected ?? const <int>[];
    final parent = document?.root.at(into);
    if (parent == null) return;

    var made = what.make();
    // Placed rather than dropped at the origin when its parent does not lay
    // it out: everything added to a stack landing in the same corner and on
    // top of the last one is not an interface, it is a pile.
    if (parent.type == 'stack') {
      final at = 48.0 + parent.children.length * 24;
      made = made.placeAt(at, at);
    }

    // A container is a decision about where things line up, and the grid is
    // what they line up against. Putting one in and being shown nothing to
    // put it against is where somebody has to go looking for the setting that
    // makes the tool do the obvious thing.
    if (what == UiElement.column || what == UiElement.row) _columns = true;

    edit(
      'Add ${what.label}',
      (document) => document.copyWith(
        root: document.root.insertAt(into, parent.children.length, made),
      ),
      select: [...into, parent.children.length],
    );
  }

  /// Turns the selected container into a row of equal columns.
  ///
  /// The one operation a grid is actually for. Whatever was inside goes into
  /// the first column rather than being thrown away or spread out. Somebody
  /// splitting a screen in two has content on it already, and a split that
  /// emptied it would be a split nobody could use twice.
  void split(int count) {
    final path = _selected;
    final element = this.element;
    if (path == null || element == null) return;

    // Stripped of their places on the way in. A child that keeps `left` and
    // `top` becomes a Positioned, and a Positioned that is no longer in a
    // stack does not lay out badly. It throws, and takes the canvas with it.
    final kept = [for (final child in element.children) child.unplaced];

    final split = element.copyWith(
      type: 'row',
      // Stacked until there is room to sit side by side. Four columns across
      // a phone are four columns nobody can read, and having to think about
      // that every time is how a split becomes something to undo.
      classes: '${flowing(element.classes)} col md:row'.trim(),
      children: [
        for (var i = 0; i < count; i++)
          UiNode(
            // Equal widths only where they are widths. Stacked, `flex-1`
            // would divide the height instead and give four columns a
            // quarter of the screen each.
            type: 'column',
            classes: 'md:flex-1 gap-2',
            children: i == 0 ? kept : const [],
          ),
      ],
    );

    _columns = true;
    edit(
      'Split ${uiNodeName(element)} into $count columns',
      (document) => document.copyWith(
        root: document.root.replaceAt(path, _spanning(split)),
      ),
    );
  }

  /// A placed element given a width to divide.
  ///
  /// Something dropped on a stack has a `left` and nothing else, which leaves
  /// its width unbounded. Equal columns of an unbounded width are not a
  /// layout, they are the reason the canvas went blank. Reaching the far edge
  /// is the honest reading of "split this into columns". Anything that
  /// already says how wide it is keeps what it says.
  static UiNode _spanning(UiNode node) {
    if (node.placed == null) return node;

    final style = UiBuilder().styleOf(node);
    if (style.width != null || style.right != null) return node;

    final rest = [
      for (final declaration in node.css.split(';'))
        if (declaration.trim().isNotEmpty) declaration.trim(),
    ];
    return node.copyWith(css: [...rest, 'right: 0'].join('; '));
  }

  /// The drag in progress: what ties its frames into one step, and what it
  /// is moving.
  ({Object gesture, List<int> path})? _drag;

  /// Moves an element while it is being dragged.
  ///
  /// The whole drag is one step. Without that an undo would walk back
  /// through every frame of the movement, which is a hundred presses to put
  /// something back where it was.
  void move(List<int> path, Offset to) {
    final element = document?.root.at(path);
    if (element == null) return;
    final drag = _drag ??= (gesture: Object(), path: path);

    // The fraction is kept while the pointer is down. Rounding every frame
    // throws away a fraction of a pixel each time, and a slow drag ends up
    // behind the pointer by however long somebody took over it.
    edit(
      'Move ${uiNodeName(element)}',
      (document) => document.copyWith(
        root: document.root.replaceAt(
          path,
          element.placeAt(to.dx, to.dy, round: false),
        ),
      ),
      gesture: drag.gesture,
      onlyMoves: true,
    );
  }

  /// Lands the dragged element on a whole pixel, as the end of the drag's
  /// one step.
  ///
  /// A file full of positions to fourteen decimal places is a file whose diff
  /// is unreadable, and the difference is not visible on any screen.
  void moveDone() {
    final drag = _drag;
    _drag = null;
    final element = drag == null ? null : document?.root.at(drag.path);
    final at = element?.placed;
    if (drag != null && element != null && at != null) {
      edit(
        'Move ${uiNodeName(element)}',
        (document) => document.copyWith(
          root: document.root.replaceAt(
            drag.path,
            element.placeAt(at.left, at.top),
          ),
        ),
        gesture: drag.gesture,
        onlyMoves: true,
      );
    }
    history.seal();
  }

  void remove(List<int> path) {
    final element = document?.root.at(path);
    if (path.isEmpty || element == null) return;
    edit(
      'Remove ${uiNodeName(element)}',
      (document) => document.copyWith(root: document.root.removeAt(path)),
      select: path.sublist(0, path.length - 1),
    );
  }

  void removeSelected() {
    if (_selected case final path?) remove(path);
  }

  /// Where an [InterfaceEdit] puts an interface, forwards or back.
  ///
  /// Shows the interface it puts, so undoing a step in one somebody has
  /// moved away from is not a change they cannot see.
  void _put(String path, UiDocument document, List<int>? selected) {
    final open = this[path];
    if (open == null) return;
    open.document = document;
    if (_shown != path) {
      _shown = path;
      _hovered = null;
    }
    // The selection can be left pointing at something that has gone.
    _selected = selected != null && document.root.at(selected) == null
        ? const []
        : selected;
    if (_hovered case final hovered? when document.root.at(hovered) == null) {
      _hovered = null;
    }
    notifyListeners();
  }
}

/// One change to an interface.
///
/// Holds the document from before and the document from after. Both are
/// whole and neither ever changes, so undoing is putting one back. An edit
/// shares every subtree it did not touch, so a step costs the spine of one
/// edit rather than a copy of the interface.
class InterfaceEdit extends EditorCommand {
  InterfaceEdit({
    required this.bench,
    required this.path,
    required this.label,
    required this.from,
    required this.to,
    required this.selectedBefore,
    required this.selectedAfter,
    this.gesture,
    this.onlyMoves = false,
  });

  final InterfaceBench bench;

  final String path;

  @override
  final String label;

  final UiDocument from;

  /// Not final: a merged drag rewrites where it ends up.
  UiDocument to;

  final List<int>? selectedBefore;

  List<int>? selectedAfter;

  /// What ties a run of these together, or null for one alone.
  final Object? gesture;

  @override
  final bool onlyMoves;

  @override
  String get sceneId => interfaceSceneId(path);

  @override
  Object? get mergeKey => gesture == null ? null : (path, gesture);

  @override
  void apply(SceneHost host) => bench._put(path, to, selectedAfter);

  @override
  void revert(SceneHost host) => bench._put(path, from, selectedBefore);

  @override
  void absorb(EditorCommand later) {
    if (later is! InterfaceEdit) return;
    to = later.to;
    selectedAfter = later.selectedAfter;
  }
}
