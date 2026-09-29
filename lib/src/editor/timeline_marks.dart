part of 'timeline.dart';

// The thin strips between the ruler and the keys: the clip's marks, and a
// cutscene's shots.

/// One strip under the ruler, named on the left where the track list is and
/// in step with the sheet on the right.
final class _Strip extends StatelessWidget {
  const _Strip({required this.label, required this.child, this.action});

  final String label;
  final Widget child;

  /// A button beside the name, such as the one that adds a mark.
  final Widget? action;

  @override
  Widget build(BuildContext context) => Container(
    height: rowHeight,
    decoration: const BoxDecoration(
      border: Border(bottom: BorderSide(color: OrblitColors.lineSoft)),
    ),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Container(
          width: _trackWidth,
          padding: const EdgeInsets.symmetric(horizontal: Space.sm),
          decoration: const BoxDecoration(
            border: Border(right: BorderSide(color: OrblitColors.lineSoft)),
          ),
          child: Row(
            children: [
              Expanded(child: Text(label, style: OrblitText.caption)),
              ?action,
            ],
          ),
        ),
        Expanded(child: child),
      ],
    ),
  );
}

/// The clip's marks, each a flag at its moment, and the button that adds one
/// where the playhead is.
///
/// A mark is how a clip tells the game something happened, such as a
/// footstep landing. One named after a cutscene starts it.
final class _MarkStrip extends StatelessWidget {
  const _MarkStrip({required this.bench, required this.clip});

  final ClipBench bench;
  final ClipDocument clip;

  @override
  Widget build(BuildContext context) => _Strip(
    label: 'Marks',
    action: _Action(
      icon: Icons.add,
      tooltip: 'Add a mark at the playhead',
      onTap: () => _addMark(context),
    ),
    child: LayoutBuilder(
      builder: (context, constraints) {
        final axis = TimeAxis(clip, width: constraints.maxWidth);
        return Stack(
          clipBehavior: Clip.hardEdge,
          children: [
            for (final (index, mark) in clip.marks.indexed)
              Positioned(
                left: axis.xOf(mark.at) - 6,
                top: 0,
                bottom: 0,
                child: _MarkFlag(bench: bench, index: index, mark: mark),
              ),
          ],
        );
      },
    ),
  );

  Future<void> _addMark(BuildContext context) async {
    final at = bench.frame;
    final name = await promptForName(
      context,
      title: 'Add a mark',
      initial: 'Mark',
      hint: 'Name it after a cutscene to start that cutscene here.',
      action: 'Add',
    );
    if (name == null || name.isEmpty) return;
    bench.edit(
      'Add mark $name',
      (clip) => clip.copyWith(marks: [...clip.marks, Mark(at, name)]),
    );
  }
}

/// One mark: a flag with its name, and a menu to rename, move or delete it.
final class _MarkFlag extends StatelessWidget {
  const _MarkFlag({
    required this.bench,
    required this.index,
    required this.mark,
  });

  final ClipBench bench;

  /// Where it is in the clip's marks, which is what an edit replaces.
  final int index;

  final Mark mark;

  @override
  Widget build(BuildContext context) => MenuAnchor(
    style: orblitMenuStyle,
    menuChildren: [
      MenuItemButton(
        onPressed: () => _rename(context),
        child: const Text('Rename', style: OrblitText.label),
      ),
      MenuItemButton(
        onPressed: () => _replace(
          'Move mark ${mark.name}',
          Mark(bench.frame, mark.name, payload: mark.payload),
        ),
        child: const Text('Move to the playhead', style: OrblitText.label),
      ),
      MenuItemButton(
        onPressed: () => _replace('Delete mark ${mark.name}', null),
        child: const Text('Delete', style: OrblitText.label),
      ),
    ],
    builder: (context, controller, _) => Tooltip(
      message: '${mark.name} at ${secondsLabel(mark.at)}',
      child: MouseRegion(
        cursor: SystemMouseCursors.click,
        child: GestureDetector(
          onTap: () =>
              controller.isOpen ? controller.close() : controller.open(),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.flag, size: 12, color: OrblitColors.warn),
              Text(mark.name, style: OrblitText.caption),
            ],
          ),
        ),
      ),
    ),
  );

  Future<void> _rename(BuildContext context) async {
    final name = await promptForName(
      context,
      title: 'Rename mark',
      initial: mark.name,
      action: 'Rename',
    );
    if (name == null || name.isEmpty || name == mark.name) return;
    _replace(
      'Rename mark ${mark.name}',
      Mark(mark.at, name, payload: mark.payload),
    );
  }

  /// Puts [to] where this mark is, or takes the mark out for null.
  void _replace(String label, Mark? to) => bench.edit(
    label,
    (clip) => clip.copyWith(
      marks: [
        for (final (i, one) in clip.marks.indexed)
          if (i != index) one else ?to,
      ],
    ),
  );
}

/// A cutscene's shots, each a bar from where it starts to where it ends,
/// named after the camera it looks through. Where two overlap the view
/// blends from one to the other, and the bars show it by overlapping too.
final class _ShotStrip extends StatelessWidget {
  const _ShotStrip({required this.clip, required this.shots});

  final ClipDocument clip;
  final List<CutsceneShot> shots;

  @override
  Widget build(BuildContext context) => _Strip(
    label: 'Shots',
    child: LayoutBuilder(
      builder: (context, constraints) {
        final axis = TimeAxis(clip, width: constraints.maxWidth);
        return Stack(
          clipBehavior: Clip.hardEdge,
          children: [
            for (final shot in shots)
              Positioned(
                left: axis.xOf(shot.start),
                width: shot.duration * axis.perSecond,
                top: 3,
                bottom: 3,
                child: _ShotBar(shot: shot),
              ),
          ],
        );
      },
    ),
  );
}

final class _ShotBar extends StatelessWidget {
  const _ShotBar({required this.shot});

  final CutsceneShot shot;

  @override
  Widget build(BuildContext context) => Tooltip(
    message:
        '${shot.camera}, ${secondsLabel(shot.start)} to '
        '${secondsLabel(shot.end)}',
    child: Container(
      padding: const EdgeInsets.symmetric(horizontal: Space.xs),
      alignment: Alignment.centerLeft,
      decoration: BoxDecoration(
        // See-through, so where two shots overlap reads as the blend it is.
        color: OrblitColors.ember.withValues(alpha: 0.3),
        borderRadius: BorderRadius.circular(Radii.control),
        border: Border.all(color: OrblitColors.emberDeep),
      ),
      child: Text(
        shot.camera,
        maxLines: 1,
        overflow: TextOverflow.clip,
        style: OrblitText.caption,
      ),
    ),
  );
}
