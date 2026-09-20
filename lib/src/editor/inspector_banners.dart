part of 'inspector.dart';

// What the inspector says above the fields: where the object came from,
// what it is linked to, and why there may be no fields to show.

/// Says this object came from a prefab, and offers the three things anybody
/// wants to do about it.
///
/// At the top, above the fields, because it changes what editing a field
/// *means*: a change here is a change to one lamp post until it is applied,
/// and then it is a change to every lamp post.
class _PrefabBand extends StatelessWidget {
  const _PrefabBand({
    required this.source,
    this.onApply,
    this.onRevert,
    this.onUnpack,
  });

  /// The prefab's path, relative to the project.
  final String source;

  final VoidCallback? onApply;
  final VoidCallback? onRevert;
  final VoidCallback? onUnpack;

  @override
  Widget build(BuildContext context) {
    final name = p.basename(source);

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: Space.md,
        vertical: Space.sm,
      ),
      decoration: const BoxDecoration(
        color: OrblitColors.raised,
        border: Border(bottom: BorderSide(color: OrblitColors.lineSoft)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              const Icon(
                Icons.widgets_outlined,
                size: 13,
                color: OrblitColors.ember,
              ),
              const SizedBox(width: Space.sm),
              Expanded(
                child: Tooltip(
                  message: source,
                  child: Text(
                    name,
                    overflow: TextOverflow.ellipsis,
                    style: OrblitText.label.copyWith(color: OrblitColors.ember),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: Space.sm),
          Row(
            children: [
              _PrefabAction(
                label: 'Apply',
                tooltip:
                    'Save this back to the prefab, and update its other '
                    'instances. They keep where they stand and what they are '
                    'called; everything else comes from the prefab.',
                onPressed: onApply,
              ),
              const SizedBox(width: Space.xs),
              _PrefabAction(
                label: 'Revert',
                tooltip:
                    'Throw away the changes made to this one and take '
                    'the prefab again.',
                onPressed: onRevert,
              ),
              const SizedBox(width: Space.xs),
              _PrefabAction(
                label: 'Unpack',
                tooltip:
                    'Break the link. This becomes an ordinary object and '
                    'stops following the prefab.',
                onPressed: onUnpack,
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _PrefabAction extends StatelessWidget {
  const _PrefabAction({
    required this.label,
    required this.tooltip,
    required this.onPressed,
  });

  final String label;
  final String tooltip;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Tooltip(
        message: tooltip,
        waitDuration: const Duration(milliseconds: 400),
        child: OrblitButton(
          label: label,
          tone: ButtonTone.quiet,
          expand: true,
          onPressed: onPressed,
        ),
      ),
    );
  }
}

/// One data object an object points at.
class _DataLink extends StatelessWidget {
  const _DataLink({required this.path, this.onOpen, this.onRemove});

  final String path;
  final VoidCallback? onOpen;
  final VoidCallback? onRemove;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        children: [
          const Icon(
            Icons.dataset_outlined,
            size: 13,
            color: OrblitColors.inkDim,
          ),
          const SizedBox(width: Space.sm),
          Expanded(
            child: Tooltip(
              message: path,
              child: GestureDetector(
                onTap: onOpen,
                child: Text(
                  p.basename(path),
                  overflow: TextOverflow.ellipsis,
                  style: OrblitText.label.copyWith(color: OrblitColors.ink),
                ),
              ),
            ),
          ),
          if (onRemove != null)
            Tooltip(
              message: 'Stop using this here',
              child: GestureDetector(
                onTap: onRemove,
                child: const Padding(
                  padding: EdgeInsets.all(Space.xs),
                  child: Icon(
                    Icons.close,
                    size: 12,
                    color: OrblitColors.inkDim,
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}

/// Says that the fields below belong to one of several selected things.
class _MultipleNotice extends StatelessWidget {
  const _MultipleNotice({required this.count, required this.name});

  final int count;
  final String name;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: Space.md,
        vertical: Space.sm,
      ),
      color: OrblitColors.emberWash,
      child: Row(
        children: [
          const Icon(
            Icons.layers_outlined,
            size: 13,
            color: OrblitColors.ember,
          ),
          const SizedBox(width: Space.sm),
          Expanded(
            child: Text(
              '$count selected · editing $name',
              overflow: TextOverflow.ellipsis,
              style: OrblitText.caption.copyWith(color: OrblitColors.ember),
            ),
          ),
        ],
      ),
    );
  }
}

/// The object's icon and its name, which is editable in place.
class _Header extends StatefulWidget {
  const _Header({
    required this.name,
    required this.icon,
    required this.onRename,
    required this.onRenameDone,
    this.editable = true,
  });

  final String name;
  final IconData icon;
  final ValueChanged<String> onRename;
  final VoidCallback onRenameDone;
  final bool editable;

  @override
  State<_Header> createState() => _HeaderState();
}

class _HeaderState extends State<_Header> {
  late final TextEditingController _controller = TextEditingController(
    text: widget.name,
  );
  final FocusNode _focus = FocusNode();

  @override
  void initState() {
    super.initState();
    // Renaming merges into one undo step while the field has focus, and seals
    // when it loses it — so undo returns to the old name, not to a prefix of
    // the new one.
    _focus.addListener(() {
      if (!_focus.hasFocus) widget.onRenameDone();
    });
  }

  @override
  void didUpdateWidget(_Header oldWidget) {
    super.didUpdateWidget(oldWidget);
    // An undo changes the name behind the field's back; without this the box
    // would go on showing what was typed.
    if (widget.name != _controller.text && !_focus.hasFocus) {
      _controller.text = widget.name;
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    _focus.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        Space.md,
        Space.sm,
        Space.md,
        Space.sm,
      ),
      child: Row(
        children: [
          Container(
            width: 22,
            height: 22,
            decoration: BoxDecoration(
              color: OrblitColors.emberWash,
              borderRadius: BorderRadius.circular(4),
            ),
            child: Icon(widget.icon, size: 13, color: OrblitColors.ember),
          ),
          const SizedBox(width: Space.sm),
          Expanded(
            child: TextField(
              controller: _controller,
              focusNode: _focus,
              readOnly: !widget.editable,
              style: OrblitText.title.copyWith(fontSize: 13.5),
              cursorColor: OrblitColors.ember,
              decoration: const InputDecoration(
                isDense: true,
                border: InputBorder.none,
                contentPadding: EdgeInsets.symmetric(vertical: 4),
              ),
              onChanged: (value) {
                if (value.trim().isEmpty) return;
                widget.onRename(value.trim());
              },
              onSubmitted: (_) => widget.onRenameDone(),
            ),
          ),
        ],
      ),
    );
  }
}
