part of 'editor_shell.dart';

// The menus on the top bar. Each is the same shape: a button that
// opens a list, and one entry a command.

/// The Add menu.
class _AddMenu extends StatelessWidget {
  const _AddMenu({
    required this.onAdd,
    required this.onAddShape,
    required this.onAddTerrain,
  });

  final ValueChanged<ObjectKind> onAdd;

  /// Shapes are their own submenu: there are seven of them and they are the
  /// thing somebody reaches for most while blocking a level out.
  final ValueChanged<ShapeKind> onAddShape;

  /// Ground: an object and a file of its own, which is more than picking an
  /// object kind.
  final VoidCallback onAddTerrain;

  static const _items = [
    // Not 'Cube': it is an object that draws a cube until it is given a mesh
    // to draw instead, and there is a real cube one submenu above.
    (ObjectKind.mesh, 'Mesh object', Icons.view_in_ar_outlined),
    (ObjectKind.light, 'Light', Icons.wb_sunny_outlined),
    (ObjectKind.camera, 'Camera', Icons.videocam_outlined),
    (ObjectKind.group, 'Group', Icons.folder_outlined),
    (ObjectKind.weather, 'Weather', Icons.cloud_outlined),
  ];

  @override
  Widget build(BuildContext context) {
    return MenuAnchor(
      style: MenuStyle(
        backgroundColor: WidgetStatePropertyAll(OrblitColors.raised),
        surfaceTintColor: const WidgetStatePropertyAll(Colors.transparent),
        shape: WidgetStatePropertyAll(
          RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(Radii.panel),
            side: const BorderSide(color: OrblitColors.line),
          ),
        ),
      ),
      menuChildren: [
        SubmenuButton(
          menuStyle: MenuStyle(
            backgroundColor: WidgetStatePropertyAll(OrblitColors.raised),
            surfaceTintColor: const WidgetStatePropertyAll(Colors.transparent),
            shape: WidgetStatePropertyAll(
              RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(Radii.panel),
                side: const BorderSide(color: OrblitColors.line),
              ),
            ),
          ),
          leadingIcon: const Icon(
            Icons.category_outlined,
            size: 14,
            color: OrblitColors.inkMid,
          ),
          menuChildren: [
            for (final shape in ShapeKind.values)
              MenuItemButton(
                onPressed: () => onAddShape(shape),
                child: Text(shape.label, style: OrblitText.label),
              ),
          ],
          child: Text('Shape', style: OrblitText.label),
        ),
        const Divider(height: 9, color: OrblitColors.line),
        for (final (kind, label, icon) in _items)
          MenuItemButton(
            onPressed: () => onAdd(kind),
            leadingIcon: Icon(icon, size: 14, color: OrblitColors.inkMid),
            child: Text(label, style: OrblitText.label),
          ),
        MenuItemButton(
          onPressed: onAddTerrain,
          leadingIcon: const Icon(
            Icons.landscape_outlined,
            size: 14,
            color: OrblitColors.inkMid,
          ),
          child: Text('Terrain', style: OrblitText.label),
        ),
      ],
      builder: (context, controller, child) => OrblitButton(
        label: 'Add',
        icon: Icons.add,
        tone: ButtonTone.quiet,
        onPressed: () =>
            controller.isOpen ? controller.close() : controller.open(),
      ),
    );
  }
}

/// New, Save and Save As.
class _SceneMenu extends StatelessWidget {
  const _SceneMenu({
    required this.dirty,
    required this.onSave,
    required this.onSaveAs,
    required this.onNewScene,
    required this.onOpenInCode,
    required this.onReveal,
  });

  final bool dirty;
  final VoidCallback onSave;
  final VoidCallback onSaveAs;

  final VoidCallback onNewScene;

  /// Opens the project folder in whatever code editor is installed.
  final VoidCallback onOpenInCode;

  /// Shows the project folder in the desktop's file browser.
  final VoidCallback onReveal;

  @override
  Widget build(BuildContext context) {
    return MenuAnchor(
      style: MenuStyle(
        backgroundColor: WidgetStatePropertyAll(OrblitColors.raised),
        surfaceTintColor: const WidgetStatePropertyAll(Colors.transparent),
        shape: WidgetStatePropertyAll(
          RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(Radii.panel),
            side: const BorderSide(color: OrblitColors.line),
          ),
        ),
      ),
      menuChildren: [
        MenuItemButton(
          onPressed: onNewScene,
          leadingIcon: const Icon(
            Icons.note_add_outlined,
            size: 14,
            color: OrblitColors.inkMid,
          ),
          child: Text('New scene', style: OrblitText.label),
        ),
        MenuItemButton(
          onPressed: onSave,
          leadingIcon: const Icon(
            Icons.save_outlined,
            size: 14,
            color: OrblitColors.inkMid,
          ),
          child: Text('Save', style: OrblitText.label),
        ),
        MenuItemButton(
          onPressed: onSaveAs,
          leadingIcon: const Icon(
            Icons.drive_file_move_outline,
            size: 14,
            color: OrblitColors.inkMid,
          ),
          child: Text('Save as…', style: OrblitText.label),
        ),
        const Divider(height: 9, color: OrblitColors.line),
        MenuItemButton(
          onPressed: onOpenInCode,
          leadingIcon: const Icon(
            Icons.code,
            size: 14,
            color: OrblitColors.inkMid,
          ),
          // Named after what is installed, so it says where it is going
          // rather than promising an editor that is not there.
          child: Text(
            'Open project in ${CodeEditor.available ?? 'VS Code'}',
            style: OrblitText.label,
          ),
        ),
        MenuItemButton(
          onPressed: onReveal,
          leadingIcon: const Icon(
            Icons.folder_open_outlined,
            size: 14,
            color: OrblitColors.inkMid,
          ),
          child: Text(
            Platform.isMacOS ? 'Show in Finder' : 'Show project folder',
            style: OrblitText.label,
          ),
        ),
      ],
      builder: (context, controller, child) => OrblitButton(
        // The dot is the unsaved marker, in the place somebody looks for it.
        label: dirty ? 'Scene •' : 'Scene',
        icon: Icons.description_outlined,
        tone: ButtonTone.quiet,
        onPressed: () =>
            controller.isOpen ? controller.close() : controller.open(),
      ),
    );
  }
}

/// Cut, copy, paste and duplicate.
///
/// Worth a menu rather than only shortcuts: copying between scenes is the
/// only way to move an object from one to another, and nobody discovers a
/// keystroke that is not written down anywhere.
class _EditMenu extends StatelessWidget {
  const _EditMenu({
    required this.selectionCount,
    required this.clipboard,
    required this.onCopy,
    required this.onCut,
    required this.onPaste,
    required this.onDuplicate,
  });

  final int selectionCount;
  final String clipboard;
  final VoidCallback onCopy;
  final VoidCallback onCut;
  final VoidCallback onPaste;
  final VoidCallback onDuplicate;

  @override
  Widget build(BuildContext context) {
    return MenuAnchor(
      style: MenuStyle(
        backgroundColor: WidgetStatePropertyAll(OrblitColors.raised),
        surfaceTintColor: const WidgetStatePropertyAll(Colors.transparent),
        shape: WidgetStatePropertyAll(
          RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(Radii.panel),
            side: const BorderSide(color: OrblitColors.line),
          ),
        ),
      ),
      menuChildren: [
        _item(
          selectionCount > 1 ? 'Cut $selectionCount objects' : 'Cut',
          commandShortcutLabel('X'),
          Icons.content_cut,
          selectionCount > 0 ? onCut : null,
        ),
        _item(
          selectionCount > 1 ? 'Copy $selectionCount objects' : 'Copy',
          commandShortcutLabel('C'),
          Icons.content_copy,
          selectionCount > 0 ? onCopy : null,
        ),
        _item(
          clipboard.isEmpty ? 'Paste' : 'Paste $clipboard',
          commandShortcutLabel('V'),
          Icons.content_paste,
          onPaste,
        ),
        _item(
          'Duplicate',
          commandShortcutLabel('D'),
          Icons.copy_all,
          selectionCount > 0 ? onDuplicate : null,
        ),
      ],
      builder: (context, controller, child) => OrblitButton(
        label: 'Edit',
        icon: Icons.content_copy,
        tone: ButtonTone.quiet,
        onPressed: () =>
            controller.isOpen ? controller.close() : controller.open(),
      ),
    );
  }

  Widget _item(
    String label,
    String shortcut,
    IconData icon,
    VoidCallback? onPressed,
  ) {
    final enabled = onPressed != null;
    return MenuItemButton(
      onPressed: onPressed,
      leadingIcon: Icon(
        icon,
        size: 14,
        color: enabled ? OrblitColors.inkMid : OrblitColors.line,
      ),
      trailingIcon: Text(
        shortcut,
        style: OrblitText.mono.copyWith(
          fontSize: 11,
          color: enabled ? OrblitColors.inkDim : OrblitColors.line,
        ),
      ),
      child: Text(
        label,
        style: OrblitText.label.copyWith(
          color: enabled ? OrblitColors.ink : OrblitColors.line,
        ),
      ),
    );
  }
}

/// Where the panels are, and whether they can be moved.
///
/// An arrangement somebody has settled into is worth keeping, and a layout
/// that can always be pulled apart eventually is — by a drag that was meant to
/// be something else. So it locks. And the two arrangements worth one press
/// are here, because building a four-view layout by dragging is a minute of
/// somebody's time every time they want one.
class _ViewMenu extends StatelessWidget {
  const _ViewMenu({
    required this.layout,
    required this.onLayout,
    required this.panels,
  });

  final DockLayout layout;
  final ValueChanged<DockLayout> onLayout;

  /// The panels that can be opened: whatever is registered, in the order it
  /// was registered.
  final List<PanelType> panels;

  @override
  Widget build(BuildContext context) {
    return MenuAnchor(
      style: MenuStyle(
        backgroundColor: WidgetStatePropertyAll(OrblitColors.raised),
        surfaceTintColor: const WidgetStatePropertyAll(Colors.transparent),
        shape: WidgetStatePropertyAll(
          RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(Radii.panel),
            side: const BorderSide(color: OrblitColors.line),
          ),
        ),
      ),
      menuChildren: [
        MenuItemButton(
          onPressed: () => onLayout(layout.copyWith(locked: !layout.locked)),
          leadingIcon: Icon(
            layout.locked ? Icons.lock_outline : Icons.lock_open_outlined,
            size: 14,
            color: layout.locked ? OrblitColors.ember : OrblitColors.inkMid,
          ),
          child: Text(
            layout.locked ? 'Unlock the layout' : 'Lock the layout',
            style: OrblitText.label,
          ),
        ),
        const Divider(height: 9, color: OrblitColors.line),
        MenuItemButton(
          onPressed: () =>
              onLayout(DockLayout.standard().copyWith(locked: layout.locked)),
          leadingIcon: const Icon(
            Icons.view_quilt_outlined,
            size: 14,
            color: OrblitColors.inkMid,
          ),
          child: Text('One view', style: OrblitText.label),
        ),
        MenuItemButton(
          onPressed: () =>
              onLayout(DockLayout.fourViews().copyWith(locked: layout.locked)),
          leadingIcon: const Icon(
            Icons.grid_view_outlined,
            size: 14,
            color: OrblitColors.inkMid,
          ),
          child: Text('Four views', style: OrblitText.label),
        ),
        const Divider(height: 9, color: OrblitColors.line),
        // Opening one that is already open shows it rather than adding a
        // second, which is why every one of these can be pressed at any time.
        for (final type in panels)
          MenuItemButton(
            onPressed: () => onLayout(layout.add(type.panel)),
            leadingIcon: Icon(
              type.kind.icon,
              size: 14,
              color: layout.holds(type.panel.id)
                  ? OrblitColors.ember
                  : OrblitColors.inkMid,
            ),
            child: Text(type.kind.label, style: OrblitText.label),
          ),
      ],
      builder: (context, controller, child) => OrblitButton(
        label: layout.locked ? 'View •' : 'View',
        icon: Icons.dashboard_outlined,
        tone: ButtonTone.quiet,
        onPressed: () =>
            controller.isOpen ? controller.close() : controller.open(),
      ),
    );
  }
}
