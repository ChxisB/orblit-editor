import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:orblit_ui/orblit_ui.dart';
import 'package:path/path.dart' as p;

import '../platform/command_shortcuts.dart';
import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'inspector.dart' show FieldRow, SliderRow;
import 'ui_canvas.dart';

part 'ui_editor_tree.dart';
part 'ui_editor_side.dart';
part 'ui_editor_controls.dart';

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

/// Laying out an interface.
///
/// A screen rather than a panel in the shell. A canvas wants the whole window
/// — it is a design surface at a fixed size, and squeezing one into the space
/// left over beside a 3D viewport gives a view too small to lay anything out
/// in and a viewport nobody is looking at.
class UiEditor extends StatefulWidget {
  const UiEditor({
    super.key,
    required this.path,
    required this.document,
    this.onProblem,
  });

  /// Where the `.oui` lives.
  final String path;

  final UiDocument document;

  final ValueChanged<String>? onProblem;

  @override
  State<UiEditor> createState() => _UiEditorState();
}

class _UiEditorState extends State<UiEditor> {
  late UiDocument _document = widget.document;

  /// Whole documents, one per step.
  ///
  /// A tree is immutable and an edit shares every subtree it did not touch, so
  /// a step costs the spine of one edit rather than a copy of the interface.
  /// That is what makes "keep the last hundred" the simple answer here and a
  /// per-field command the complicated one.
  final List<UiDocument> _done = [];
  final List<UiDocument> _undone = [];

  List<int>? _selected = const [];
  List<int>? _hovered;

  bool _previewing = false;
  bool _outlines = true;
  bool _guides = true;
  bool _columns = false;
  bool _dirty = false;

  /// The device being previewed, upright, or null for the canvas's own size.
  ///
  /// Not part of the document. Which device somebody is looking at while they
  /// work is a view, the same way the outlines are — saving it would mean two
  /// people opening the same file and disagreeing about what it is.
  Size? _device;

  /// Whether it is being held sideways.
  ///
  /// Kept apart from the device rather than folded into it, so turning a phone
  /// on its side and then picking a tablet gives a tablet on its side. A
  /// single flipped size would forget which way up somebody was working.
  bool _landscape = false;

  /// The screen to lay out on: the device the way it is being held.
  Size? get _preview {
    final base =
        _device ?? Size(_document.canvas.width, _document.canvas.height);
    if (_device == null && !_landscape) return null;
    return _landscape ? Size(base.height, base.width) : base;
  }

  /// The document as a drag started, so a whole drag is one undo step rather
  /// than one per frame of it.
  UiDocument? _before;

  UiNode? get _element =>
      _selected == null ? null : _document.root.at(_selected!);

  void _change(UiDocument next, {List<int>? select}) {
    setState(() {
      _done.add(_document);
      if (_done.length > 200) _done.removeAt(0);
      _undone.clear();
      _document = next;
      _dirty = true;
      if (select != null) _selected = select;
      // The selection can be left pointing at something that has gone.
      if (_selected != null && _document.root.at(_selected!) == null) {
        _selected = const [];
      }
    });
  }

  void _undo() {
    if (_done.isEmpty) return;
    setState(() {
      _undone.add(_document);
      _document = _done.removeLast();
      _dirty = true;
      if (_selected != null && _document.root.at(_selected!) == null) {
        _selected = const [];
      }
    });
  }

  void _redo() {
    if (_undone.isEmpty) return;
    setState(() {
      _done.add(_document);
      _document = _undone.removeLast();
      _dirty = true;
      if (_selected != null && _document.root.at(_selected!) == null) {
        _selected = const [];
      }
    });
  }

  void _save() {
    try {
      File(widget.path).writeAsStringSync(_document.toText());
    } on FileSystemException catch (error) {
      widget.onProblem?.call(
        'Could not save ${p.basename(widget.path)}: '
        '${error.osError?.message ?? error.message}',
      );
      return;
    }
    setState(() => _dirty = false);
  }

  void _add(UiElement what) {
    // Inside the selection, which is what "add" means when something is
    // selected and there is nowhere else it could sensibly go.
    final into = _selected ?? const <int>[];
    final parent = _document.root.at(into);
    if (parent == null) return;

    var made = what.make();
    // Placed rather than dropped at the origin when its parent does not lay
    // it out: everything added to a stack landing in the same corner and on
    // top of the last one is not an interface, it is a pile.
    if (parent.type == 'stack') {
      final at = 48.0 + parent.children.length * 24;
      made = made.placeAt(at, at);
    }

    _change(
      _document.copyWith(
        root: _document.root.insertAt(into, parent.children.length, made),
      ),
      select: [...into, parent.children.length],
    );

    // A container is a decision about where things line up, and the grid is
    // what they line up against. Putting one in and being shown nothing to
    // put it against is where somebody has to go looking for the setting that
    // makes the tool do the obvious thing.
    if (what == UiElement.column || what == UiElement.row) {
      setState(() => _columns = true);
    }
  }

  /// Turns the selected container into a row of equal columns.
  ///
  /// The one operation a grid is actually for. Whatever was inside goes into
  /// the first column rather than being thrown away or spread out — somebody
  /// splitting a screen in two has content on it already, and a split that
  /// emptied it would be a split nobody could use twice.
  void _split(int count) {
    final path = _selected;
    final element = path == null ? null : _document.root.at(path);
    if (path == null || element == null) return;

    // Stripped of their places on the way in. A child that keeps `left` and
    // `top` becomes a Positioned, and a Positioned that is no longer in a
    // stack does not lay out badly — it throws, and takes the canvas with it.
    final kept = [for (final child in element.children) child.unplaced];

    final split = element.copyWith(
      type: 'row',
      // Stacked until there is room to sit side by side. Four columns across
      // a phone are four columns nobody can read, and having to think about
      // that every time is how a split becomes something to undo.
      classes: '${_flowing(element.classes)} col md:row'.trim(),
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

    _change(
      _document.copyWith(
        root: _document.root.replaceAt(path, _spanning(split)),
      ),
      select: path,
    );
    setState(() => _columns = true);
  }

  /// A placed element given a width to divide.
  ///
  /// Something dropped on a stack has a `left` and nothing else, which leaves
  /// its width unbounded — and equal columns of an unbounded width are not a
  /// layout, they are the reason the canvas went blank. Reaching the far edge
  /// is the honest reading of "split this into columns"; anything that already
  /// says how wide it is keeps what it says.
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

  /// Moves an element while it is being dragged.
  ///
  /// The whole drag is one step. Without this an undo would walk back through
  /// every frame of the movement, which is a hundred presses to put something
  /// back where it was.
  void _move(List<int> path, Offset to) {
    final element = _document.root.at(path);
    if (element == null) return;

    // The fraction is kept while the pointer is down. Rounding every frame
    // throws away a fraction of a pixel each time, and a slow drag ends up
    // behind the pointer by however long somebody took over it.
    final next = _document.copyWith(
      root: _document.root.replaceAt(
        path,
        element.placeAt(to.dx, to.dy, round: false),
      ),
    );

    if (_before == null) {
      _before = _document;
      _change(next);
    } else {
      // Already recorded: replace the document without pushing another step.
      setState(() {
        _document = next;
        _dirty = true;
      });
    }
  }

  /// Lands the dragged element on a whole pixel.
  ///
  /// A file full of positions to fourteen decimal places is a file whose diff
  /// is unreadable, and the difference is not visible on any screen.
  void _moveDone() {
    _before = null;

    final path = _selected;
    final element = path == null ? null : _document.root.at(path);
    final at = element?.placed;
    if (path == null || element == null || at == null) return;
    if (at.left == at.left.roundToDouble() &&
        at.top == at.top.roundToDouble()) {
      return;
    }

    setState(() {
      _document = _document.copyWith(
        root: _document.root.replaceAt(path, element.placeAt(at.left, at.top)),
      );
    });
  }

  void _remove(List<int> path) {
    if (path.isEmpty) return;
    _change(
      _document.copyWith(root: _document.root.removeAt(path)),
      select: path.sublist(0, path.length - 1),
    );
  }

  void _edit(UiNode Function(UiNode) change) {
    final path = _selected;
    final element = path == null ? null : _document.root.at(path);
    if (path == null || element == null) return;
    _change(
      _document.copyWith(root: _document.root.replaceAt(path, change(element))),
    );
  }

  Future<void> _leave() async {
    if (!_dirty) {
      if (mounted) Navigator.of(context).pop();
      return;
    }

    final answer = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: OrblitColors.surface,
        title: Text(
          'Save ${p.basename(widget.path)}?',
          style: OrblitText.title,
        ),
        content: Text(
          'It has changes that are not on disk.',
          style: OrblitText.body,
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop('cancel'),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop('discard'),
            child: const Text('Discard'),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop('save'),
            child: const Text('Save'),
          ),
        ],
      ),
    );

    if (!mounted || answer == 'cancel' || answer == null) return;
    if (answer == 'save') _save();
    if (mounted) Navigator.of(context).pop();
  }

  @override
  Widget build(BuildContext context) {
    return Shortcuts(
      shortcuts: {
        commandShortcut(LogicalKeyboardKey.keyS): _SaveIntent(),
        commandShortcut(LogicalKeyboardKey.keyZ): _UndoIntent(),
        commandShortcut(LogicalKeyboardKey.keyZ, shift: true): _RedoIntent(),
        if (!commandIsMeta)
          const SingleActivator(LogicalKeyboardKey.keyY, control: true):
              _RedoIntent(),
        const SingleActivator(LogicalKeyboardKey.delete): _DeleteIntent(),
        const SingleActivator(LogicalKeyboardKey.backspace): _DeleteIntent(),
      },
      child: Actions(
        actions: {
          _SaveIntent: CallbackAction<_SaveIntent>(
            onInvoke: (_) {
              _save();
              return null;
            },
          ),
          _UndoIntent: CallbackAction<_UndoIntent>(
            onInvoke: (_) {
              _undo();
              return null;
            },
          ),
          _RedoIntent: CallbackAction<_RedoIntent>(
            onInvoke: (_) {
              _redo();
              return null;
            },
          ),
          _DeleteIntent: CallbackAction<_DeleteIntent>(
            onInvoke: (_) {
              if (_selected != null) _remove(_selected!);
              return null;
            },
          ),
        },
        child: Focus(
          autofocus: true,
          child: Scaffold(
            backgroundColor: OrblitColors.ground,
            body: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                _toolbar(),
                Expanded(
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      _Tree(
                        root: _document.root,
                        selected: _selected,
                        hovered: _hovered,
                        onSelect: (path) => setState(() => _selected = path),
                        onHover: (path) => setState(() => _hovered = path),
                        onRemove: _remove,
                      ),
                      Expanded(
                        child: UiCanvasView(
                          document: _document,
                          previewSize: _preview,
                          selected: _previewing ? null : _selected,
                          hovered: _previewing ? null : _hovered,
                          designing: !_previewing,
                          showOutlines: _outlines,
                          showGuides: _guides,
                          showColumns: _columns && !_previewing,
                          onSelect: (path) => setState(() => _selected = path),
                          onHover: (path) => setState(() => _hovered = path),
                          onMove: _move,
                          onMoved: _moveDone,
                        ),
                      ),
                      _Side(
                        document: _document,
                        element: _element,
                        device: _device,
                        landscape: _landscape,
                        preview: _preview,
                        onCanvas: (canvas) =>
                            _change(_document.copyWith(canvas: canvas)),
                        onElement: _edit,
                        onAdd: _add,
                        onSplit: _split,
                        onDevice: (size) => setState(() => _device = size),
                        onLandscape: (value) =>
                            setState(() => _landscape = value),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _toolbar() {
    return Container(
      height: 44,
      padding: const EdgeInsets.symmetric(horizontal: Space.sm),
      decoration: const BoxDecoration(
        color: OrblitColors.surface,
        border: Border(bottom: BorderSide(color: OrblitColors.line)),
      ),
      child: Row(
        children: [
          OrblitButton(
            label: p.basename(widget.path) + (_dirty ? ' •' : ''),
            icon: Icons.chevron_left,
            tone: ButtonTone.quiet,
            onPressed: _leave,
          ),
          const SizedBox(width: Space.md),
          OrblitButton(
            label: 'Save',
            icon: Icons.save_outlined,
            tone: ButtonTone.quiet,
            onPressed: _dirty ? _save : null,
          ),
          const SizedBox(width: Space.xs),
          OrblitButton(
            label: 'Undo',
            icon: Icons.undo,
            tone: ButtonTone.quiet,
            onPressed: _done.isEmpty ? null : _undo,
          ),
          OrblitButton(
            label: 'Redo',
            icon: Icons.redo,
            tone: ButtonTone.quiet,
            onPressed: _undone.isEmpty ? null : _redo,
          ),
          const SizedBox(width: Space.md),
          _Showing(document: _document, preview: _preview),
          const Spacer(),
          // What the game shows, with nothing over it. The one control that
          // answers "is this guide going to be in my screenshot".
          _Toggle(
            label: 'Preview',
            icon: Icons.play_arrow_outlined,
            on: _previewing,
            onChanged: (value) => setState(() => _previewing = value),
          ),
          const SizedBox(width: Space.xs),
          // Named after what they actually take away. "Outlines" that leaves
          // a canvas frame and a safe area on screen reads as a toggle that
          // did nothing.
          _Toggle(
            label: 'Element outlines',
            icon: Icons.select_all,
            on: _outlines && !_previewing,
            onChanged: _previewing
                ? null
                : (value) => setState(() => _outlines = value),
          ),
          const SizedBox(width: Space.xs),
          _Toggle(
            label: 'Canvas guides',
            icon: Icons.crop_free,
            on: _guides && !_previewing,
            onChanged: _previewing
                ? null
                : (value) => setState(() => _guides = value),
          ),
          const SizedBox(width: Space.xs),
          _Toggle(
            label: 'Column grid',
            icon: Icons.view_week_outlined,
            on: _columns && !_previewing,
            onChanged: _previewing
                ? null
                : (value) => setState(() => _columns = value),
          ),
        ],
      ),
    );
  }
}

class _SaveIntent extends Intent {
  const _SaveIntent();
}

class _UndoIntent extends Intent {
  const _UndoIntent();
}

class _RedoIntent extends Intent {
  const _RedoIntent();
}

class _DeleteIntent extends Intent {
  const _DeleteIntent();
}
