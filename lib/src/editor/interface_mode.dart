import 'package:flutter/material.dart';
import 'package:orblit_ui/orblit_ui.dart';

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'dock.dart';
import 'editor_mode.dart';
import 'inspector.dart' show FieldRow, SliderRow;
import 'interface_bench.dart';
import 'ui_canvas.dart';

part 'interface_tree.dart';
part 'interface_side.dart';
part 'interface_controls.dart';

/// The canvas being laid out, in the middle of the Interface workspace.
const PanelKind interfaceCanvasPanel = PanelKind(
  'interfaceCanvas',
  'Canvas',
  Icons.web_asset,
);

/// The elements on the canvas, as a tree.
const PanelKind interfaceElementsPanel = PanelKind(
  'interfaceElements',
  'Elements',
  Icons.account_tree_outlined,
);

/// What to add, the selected element's properties and the canvas's.
const PanelKind interfaceDesignPanel = PanelKind(
  'interfaceDesign',
  'Design',
  Icons.tune,
);

/// Laying out the screens a game shows over its scene.
///
/// [onEnter] opens something to lay out when nothing is open yet, and
/// [onNew] makes a new interface.
EditorMode interfaceMode({
  required InterfaceBench bench,
  required VoidCallback onNew,
  required VoidCallback onEnter,
}) => EditorMode(
  name: 'interface',
  label: 'Interface',
  icon: Icons.web_asset,
  layout: interfaceLayout,
  tools: (_) => InterfaceShelf(bench: bench, onNew: onNew),
  onEnter: onEnter,
);

/// The canvas in the middle, where the scene view is in the other
/// workspaces, since a screen is laid out at a fixed size and wants all the
/// room there is. The elements are where the scene's tree is, and only the
/// console is under the view: nothing else there says anything about a
/// screen.
DockLayout interfaceLayout() => DockLayout.columns(
  const DockGroup(
    id: 'centre',
    panels: [DockPanel(id: 'canvas', kind: interfaceCanvasPanel)],
  ),
  left: const [DockPanel(id: 'elements', kind: interfaceElementsPanel)],
  bottom: const [DockPanel(id: 'console', kind: PanelKind.console)],
  right: const [DockPanel(id: 'design', kind: interfaceDesignPanel)],
  revision: 1,
);

/// The canvas, laid out at the size being previewed.
///
/// With nothing open it offers a new interface, since an empty grey panel
/// in the middle of the window tells a newcomer nothing about what goes
/// there.
class InterfaceCanvas extends StatelessWidget {
  const InterfaceCanvas({super.key, required this.bench, required this.onNew});

  final InterfaceBench bench;
  final VoidCallback onNew;

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: bench,
    builder: (context, _) {
      final document = bench.document;
      if (document == null) return _NothingOpen(onNew: onNew);
      final previewing = bench.previewing;
      return UiCanvasView(
        document: document,
        previewSize: bench.preview,
        selected: previewing ? null : bench.selected,
        hovered: previewing ? null : bench.hovered,
        designing: !previewing,
        showOutlines: bench.outlines,
        showGuides: bench.guides,
        showColumns: bench.columns && !previewing,
        onSelect: (path) => bench.selected = path,
        onHover: (path) => bench.hovered = path,
        onMove: bench.move,
        onMoved: bench.moveDone,
      );
    },
  );
}

/// The elements on the shown canvas, as a tree.
class InterfaceElements extends StatelessWidget {
  const InterfaceElements({super.key, required this.bench});

  final InterfaceBench bench;

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: bench,
    builder: (context, _) {
      final document = bench.document;
      if (document == null) {
        return const _Message('The elements of an open interface show here.');
      }
      return _Tree(bench: bench, root: document.root);
    },
  );
}

/// What to add, the selected element's properties and the canvas's.
class InterfaceDesign extends StatelessWidget {
  const InterfaceDesign({super.key, required this.bench});

  final InterfaceBench bench;

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: bench,
    builder: (context, _) {
      final document = bench.document;
      if (document == null) {
        return const _Message(
          'What can go on an interface, and how it looks, shows here.',
        );
      }
      return _Side(bench: bench, document: document);
    },
  );
}

/// The Interface workspace's shelf: a new interface, the one open, the size
/// it is laid out at and what is drawn over it.
///
/// Listens to the undo stack as well as the bench, because whether the
/// interface has changes is the stack's answer.
class InterfaceShelf extends StatelessWidget {
  const InterfaceShelf({super.key, required this.bench, required this.onNew});

  final InterfaceBench bench;
  final VoidCallback onNew;

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: Listenable.merge([bench, bench.history]),
    builder: (context, _) {
      final shown = bench.shown;
      return SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(
          children: [
            OrblitButton(
              label: 'New interface',
              icon: Icons.add,
              tone: ButtonTone.quiet,
              onPressed: onNew,
            ),
            if (shown != null) ...[
              const SizedBox(width: Space.md),
              Text(
                shown.name + (bench.isUnsaved(shown) ? ' •' : ''),
                style: OrblitText.label.copyWith(color: OrblitColors.ink),
              ),
              const SizedBox(width: Space.md),
              _Showing(document: shown.document, preview: bench.preview),
              const SizedBox(width: Space.lg),
              ..._toggles(),
            ],
          ],
        ),
      );
    },
  );

  List<Widget> _toggles() {
    final previewing = bench.previewing;
    return [
      // What the game shows, with nothing over it. The one control that
      // answers "is this guide going to be in my screenshot".
      _Toggle(
        label: 'Preview',
        icon: Icons.play_arrow_outlined,
        on: previewing,
        onChanged: (value) => bench.previewing = value,
      ),
      const SizedBox(width: Space.xs),
      // Named after what they actually take away. "Outlines" that leaves
      // a canvas frame and a safe area on screen reads as a toggle that
      // did nothing.
      _Toggle(
        label: 'Element outlines',
        icon: Icons.select_all,
        on: bench.outlines && !previewing,
        onChanged: previewing ? null : (value) => bench.outlines = value,
      ),
      const SizedBox(width: Space.xs),
      _Toggle(
        label: 'Canvas guides',
        icon: Icons.crop_free,
        on: bench.guides && !previewing,
        onChanged: previewing ? null : (value) => bench.guides = value,
      ),
      const SizedBox(width: Space.xs),
      _Toggle(
        label: 'Column grid',
        icon: Icons.view_week_outlined,
        on: bench.columns && !previewing,
        onChanged: previewing ? null : (value) => bench.columns = value,
      ),
    ];
  }
}

/// The canvas panel with nothing open: what an interface is, and the one
/// button that makes one.
class _NothingOpen extends StatelessWidget {
  const _NothingOpen({required this.onNew});

  final VoidCallback onNew;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(Space.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.web_asset, size: 32, color: OrblitColors.inkDim),
            const SizedBox(height: Space.md),
            const Text(
              'No interface open',
              style: OrblitText.title,
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: Space.xs),
            const Text(
              'An interface is what the game shows over the scene, such as '
              'a menu, a score or a button. Make one here, or open one from '
              'Project.',
              style: OrblitText.caption,
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: Space.md),
            OrblitButton(
              label: 'New interface',
              icon: Icons.add,
              tone: ButtonTone.primary,
              onPressed: onNew,
            ),
          ],
        ),
      ),
    );
  }
}

class _Message extends StatelessWidget {
  const _Message(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(Space.lg),
        child: Text(
          text,
          style: OrblitText.caption,
          textAlign: TextAlign.center,
        ),
      ),
    );
  }
}
