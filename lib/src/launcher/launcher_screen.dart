import 'package:file_selector/file_selector.dart';
import 'package:flutter/material.dart';

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import '../widgets/orblit_mark.dart';
import 'create_view.dart';
import 'examples_view.dart';
import 'project.dart';
import 'projects_view.dart';

/// How long ago, in the shortest form that is still true.
String ago(DateTime then) {
  final elapsed = DateTime.now().difference(then);
  if (elapsed.inMinutes < 1) return 'just now';
  if (elapsed.inMinutes < 60) return '${elapsed.inMinutes}m ago';
  if (elapsed.inHours < 24) return '${elapsed.inHours}h ago';
  if (elapsed.inDays < 30) return '${elapsed.inDays}d ago';
  return '${(elapsed.inDays / 30).floor()}mo ago';
}

/// What the launcher is currently showing.
enum _View { projects, create, examples }

/// The first thing the editor shows.
///
/// A launcher rather than an empty editor, because an editor with no project
/// open has nothing true to display: every panel would be an empty state, and
/// a screen full of empty states is worse than a screen that asks one question.
class LauncherScreen extends StatefulWidget {
  const LauncherScreen({super.key, required this.onOpen});

  /// Called once a project is chosen or created. The shell takes it from here.
  final ValueChanged<Project> onOpen;

  @override
  State<LauncherScreen> createState() => _LauncherScreenState();
}

class _LauncherScreenState extends State<LauncherScreen> {
  /// Whether an example has asked for the whole window.
  ///
  /// The rail goes with it. A full view of a scene with a navigation rail
  /// down the side of it is not a full view, and the way back is the button
  /// the example view puts where a project puts its own.
  bool _full = false;

  final ProjectStore _store = ProjectStore();
  _View _view = _View.projects;
  ProjectTemplate _template = ProjectTemplate.scene;
  List<Project> _recents = const [];
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final recents = await _store.recents();
    if (!mounted) return;
    setState(() {
      _recents = recents;
      _loading = false;
    });
  }

  Future<void> _openFolder() async {
    final directory = await getDirectoryPath(confirmButtonText: 'Open');
    if (directory == null) return;

    final project = _store.open(directory);
    if (project == null) {
      if (!mounted) return;
      _complain('No Orblit project there',
          'That folder has no $projectFileName in it.');
      return;
    }
    await _store.remember(project);
    widget.onOpen(project);
  }

  void _startFrom(ProjectTemplate template) => setState(() {
    _template = template;
    _view = _View.create;
  });

  void _complain(String title, String detail) {
    showDialog<void>(
      context: context,
      builder: (context) => _Complaint(title: title, detail: detail),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (!_full)
            _Rail(
              view: _view,
              onView: (view) => setState(() => _view = view),
            ),
          Expanded(
            child: Container(
              color: OrblitColors.ground,
              child: switch (_view) {
                _View.projects => ProjectsView(
                    loading: _loading,
                    projects: _recents,
                    onOpen: (project) async {
                      if (!project.exists) {
                        _complain('That folder has moved',
                            '${project.displayPath} no longer holds a project.');
                        return;
                      }
                      await _store.remember(project);
                      widget.onOpen(project);
                    },
                    onForget: (project) async {
                      await _store.forget(project);
                      await _load();
                    },
                    onCreate: () => _startFrom(ProjectTemplate.scene),
                    onTemplate: _startFrom,
                    onOpenFolder: _openFolder,
                  ),
                _View.create => CreateView(
                    store: _store,
                    template: _template,
                    onCancel: () => setState(() => _view = _View.projects),
                    onCreated: widget.onOpen,
                    onFailed: _complain,
                  ),
                // Kept alive behind the other two, so switching away and back
                // does not restart whatever was running — an example with a
                // day cycle in it is worth leaving where it was.
                _View.examples => ExamplesView(
                    onFull: (full) => setState(() => _full = full),
                  ),
              },
            ),
          ),
        ],
      ),
    );
  }
}

/// The left rail: identity, then the two places a launcher goes.
class _Rail extends StatelessWidget {
  const _Rail({required this.view, required this.onView});

  final _View view;
  final ValueChanged<_View> onView;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 232,
      color: OrblitColors.surface,
      padding: const EdgeInsets.symmetric(
        horizontal: Space.md,
        vertical: Space.xl,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        spacing: Space.xl,
        children: [
          const Padding(
            padding: EdgeInsets.symmetric(horizontal: Space.sm),
            child: Row(
              spacing: 10,
              children: [
                OrblitMark(size: 26),
                Text('Orblit', style: OrblitText.title),
              ],
            ),
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            spacing: 2,
            children: [
              _RailItem(
                label: 'Projects',
                icon: Icons.folder_outlined,
                // Making a project is part of this page, so it keeps the mark.
                selected: view != _View.examples,
                onTap: () => onView(_View.projects),
              ),
              _RailItem(
                label: 'Examples',
                icon: Icons.auto_stories_outlined,
                selected: view == _View.examples,
                onTap: () => onView(_View.examples),
              ),
            ],
          ),
          const Spacer(),
          // Constrained rather than left to its natural width: the rail is a
          // fixed size and the version string is not.
          const Padding(
            padding: EdgeInsets.symmetric(horizontal: Space.sm),
            child: Text(
              'Engine 0.1.0 · pre-alpha',
              overflow: TextOverflow.ellipsis,
              style: OrblitText.mono,
            ),
          ),
        ],
      ),
    );
  }
}

class _RailItem extends StatefulWidget {
  const _RailItem({
    required this.label,
    required this.icon,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final IconData icon;
  final bool selected;
  final VoidCallback onTap;

  @override
  State<_RailItem> createState() => _RailItemState();
}

class _RailItemState extends State<_RailItem> {
  bool _hovering = false;

  @override
  Widget build(BuildContext context) {
    final selected = widget.selected;

    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hovering = true),
      onExit: (_) => setState(() => _hovering = false),
      child: GestureDetector(
        onTap: widget.onTap,
        child: Container(
          height: 36,
          padding: const EdgeInsets.symmetric(horizontal: 10),
          decoration: BoxDecoration(
            color: selected
                ? OrblitColors.raised
                : (_hovering ? OrblitColors.hover : Colors.transparent),
            borderRadius: BorderRadius.circular(Radii.card),
          ),
          child: Row(
            spacing: 10,
            children: [
              Icon(
                widget.icon,
                size: 16,
                color: selected ? OrblitColors.ember : OrblitColors.inkMid,
              ),
              Text(
                widget.label,
                style: OrblitText.label.copyWith(
                  fontSize: 13,
                  color: selected ? OrblitColors.ink : OrblitColors.inkMid,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Complaint extends StatelessWidget {
  const _Complaint({required this.title, required this.detail});

  final String title;
  final String detail;

  @override
  Widget build(BuildContext context) {
    return Dialog(
      backgroundColor: Colors.transparent,
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 380),
        child: OrblitPanel(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(title, style: OrblitText.title),
              const SizedBox(height: Space.sm),
              Text(detail, style: OrblitText.body),
              const SizedBox(height: Space.lg),
              Align(
                alignment: Alignment.centerRight,
                child: OrblitButton(
                  label: 'Close',
                  onPressed: () => Navigator.of(context).pop(),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
