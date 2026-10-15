import 'package:flutter/material.dart';
import 'package:path/path.dart' as p;

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'blend_bench.dart';
import 'blend_canvas.dart';
import 'blend_inspector.dart';
import 'blend_model.dart';
import 'dock.dart';

/// The graph of a blend: its states as boxes, its changes as arrows.
const PanelKind blendGraphPanel = PanelKind(
  'blendGraph',
  'Blend',
  Icons.hub_outlined,
);

/// Below this the inspector goes under the graph rather than beside it.
const double _sideBySide = 560;

const double _inspectorWidth = 288;

/// Editing a blend: the graph of its states and changes, with the fields of
/// whatever is chosen beside it.
///
/// Listens to the bench itself, because an edit here changes nothing the
/// rest of the editor shows.
class BlendGraphPanel extends StatefulWidget {
  const BlendGraphPanel({
    super.key,
    required this.bench,
    required this.clips,
    required this.onNew,
  });

  final BlendBench bench;

  /// The names of the project's clips, which a state can play.
  final List<String> Function() clips;

  /// Makes a blend in the project and opens it here.
  final VoidCallback onNew;

  @override
  State<BlendGraphPanel> createState() => _BlendGraphPanelState();
}

class _BlendGraphPanelState extends State<BlendGraphPanel> {
  BlendBench get _bench => widget.bench;

  @override
  void initState() {
    super.initState();
    _bench.addListener(_changed);
  }

  @override
  void didUpdateWidget(BlendGraphPanel old) {
    super.didUpdateWidget(old);
    if (old.bench == _bench) return;
    old.bench.removeListener(_changed);
    _bench.addListener(_changed);
  }

  @override
  void dispose() {
    _bench.removeListener(_changed);
    super.dispose();
  }

  void _changed() {
    if (mounted) setState(() {});
  }

  void _connect(String? from, String to) {
    final at = _bench.blend?.writtenChanges.length;
    if (at == null) return;
    _bench.edit(
      'Add change',
      (blend) => blend.withChangeAdded(from, to),
      pick: ChangePicked(at),
    );
  }

  @override
  Widget build(BuildContext context) {
    final shown = _bench.shown;
    if (shown == null) return _Empty(onNew: widget.onNew);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _Tabs(bench: _bench, onNew: widget.onNew),
        Expanded(
          child: LayoutBuilder(
            builder: (context, constraints) {
              final graph = BlendCanvas(
                key: ValueKey(shown.path),
                blend: shown.blend,
                pick: _bench.pick,
                onPick: (pick) => _bench.pick = pick,
                onConnect: _connect,
              );
              final inspector = BlendInspector(
                key: ValueKey(shown.path),
                bench: _bench,
                clips: widget.clips,
              );
              if (constraints.maxWidth >= _sideBySide) {
                return Row(
                  children: [
                    Expanded(child: graph),
                    const VerticalDivider(width: 1, color: OrblitColors.line),
                    SizedBox(width: _inspectorWidth, child: inspector),
                  ],
                );
              }
              return Column(
                children: [
                  Expanded(flex: 3, child: graph),
                  const Divider(height: 1, color: OrblitColors.line),
                  Expanded(flex: 2, child: inspector),
                ],
              );
            },
          ),
        ),
      ],
    );
  }
}

final class _Empty extends StatelessWidget {
  const _Empty({required this.onNew});

  final VoidCallback onNew;

  @override
  Widget build(BuildContext context) => Column(
    mainAxisAlignment: MainAxisAlignment.center,
    children: [
      const PanelMessage(
        'No blend is open. Open a .oblend file from the project, or make '
        'one. A blend turns what a game says, such as how fast something '
        'moves, into which clip plays.',
      ),
      OrblitButton(label: 'New blend', onPressed: onNew),
    ],
  );
}

/// The open blends, one tab each, with a dot on those with changes.
final class _Tabs extends StatelessWidget {
  const _Tabs({required this.bench, required this.onNew});

  final BlendBench bench;
  final VoidCallback onNew;

  @override
  Widget build(BuildContext context) => Container(
    height: 30,
    decoration: const BoxDecoration(
      border: Border(bottom: BorderSide(color: OrblitColors.line)),
    ),
    child: Row(
      children: [
        Expanded(
          child: ListView(
            scrollDirection: Axis.horizontal,
            children: [
              for (final open in bench.open)
                _Tab(
                  key: ValueKey('blend-tab/${open.path}'),
                  open: open,
                  current: identical(open, bench.shown),
                  unsaved: bench.isUnsaved(open),
                  onTap: () => bench.show(open.path),
                  onClose: () => bench.close(open.path),
                ),
            ],
          ),
        ),
        Tooltip(
          message: 'New blend',
          child: GestureDetector(
            onTap: onNew,
            behavior: HitTestBehavior.opaque,
            child: const Padding(
              padding: EdgeInsets.symmetric(horizontal: Space.sm),
              child: Icon(Icons.add, size: 16, color: OrblitColors.inkMid),
            ),
          ),
        ),
      ],
    ),
  );
}

final class _Tab extends StatelessWidget {
  const _Tab({
    super.key,
    required this.open,
    required this.current,
    required this.unsaved,
    required this.onTap,
    required this.onClose,
  });

  final OpenBlend open;
  final bool current;
  final bool unsaved;
  final VoidCallback onTap;
  final VoidCallback onClose;

  @override
  Widget build(BuildContext context) => GestureDetector(
    onTap: onTap,
    behavior: HitTestBehavior.opaque,
    child: Container(
      padding: const EdgeInsets.only(left: Space.sm, right: 4),
      decoration: BoxDecoration(
        border: Border(
          bottom: BorderSide(
            color: current ? OrblitColors.ember : Colors.transparent,
            width: 2,
          ),
        ),
      ),
      child: Row(
        children: [
          Text(
            '${p.basenameWithoutExtension(open.path)}${unsaved ? ' •' : ''}',
            style: current ? OrblitText.label : OrblitText.caption,
          ),
          GestureDetector(
            onTap: onClose,
            behavior: HitTestBehavior.opaque,
            child: const Padding(
              padding: EdgeInsets.all(5),
              child: Icon(Icons.close, size: 12, color: OrblitColors.inkMid),
            ),
          ),
        ],
      ),
    ),
  );
}
