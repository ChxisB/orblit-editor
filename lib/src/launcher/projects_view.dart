import 'package:flutter/material.dart';

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import '../widgets/filter_field.dart';
import 'project.dart';
import 'project_cards.dart';

/// The launcher's home: ways to start a project, then the ones already opened.
class ProjectsView extends StatefulWidget {
  const ProjectsView({
    super.key,
    required this.loading,
    required this.projects,
    required this.onOpen,
    required this.onForget,
    required this.onCreate,
    required this.onTemplate,
    required this.onOpenFolder,
  });

  final bool loading;
  final List<Project> projects;
  final ValueChanged<Project> onOpen;
  final ValueChanged<Project> onForget;
  final VoidCallback onCreate;
  final ValueChanged<ProjectTemplate> onTemplate;
  final VoidCallback onOpenFolder;

  @override
  State<ProjectsView> createState() => _ProjectsViewState();
}

class _ProjectsViewState extends State<ProjectsView> {
  final TextEditingController _search = TextEditingController();
  String _query = '';

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  List<Project> get _matching => [
    for (final project in widget.projects)
      if (project.name.toLowerCase().contains(_query)) project,
  ];

  @override
  Widget build(BuildContext context) => SingleChildScrollView(
    padding: const EdgeInsets.all(Space.xxl),
    child: Align(
      alignment: Alignment.topCenter,
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 1120),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          spacing: Space.xxl,
          children: [
            _Header(
              search: _search,
              onSearch: (text) =>
                  setState(() => _query = text.trim().toLowerCase()),
              onOpenFolder: widget.onOpenFolder,
              onCreate: widget.onCreate,
            ),
            _Section(
              title: 'Start from a template',
              child: CardGrid(
                children: [
                  for (final template in ProjectTemplate.values)
                    TemplateCard(
                      template: template,
                      onTap: () => widget.onTemplate(template),
                    ),
                ],
              ),
            ),
            _Section(
              title: 'Recent',
              child: _Recent(
                loading: widget.loading,
                anyOpened: widget.projects.isNotEmpty,
                shown: _matching,
                onOpen: widget.onOpen,
                onForget: widget.onForget,
              ),
            ),
          ],
        ),
      ),
    ),
  );
}

class _Header extends StatelessWidget {
  const _Header({
    required this.search,
    required this.onSearch,
    required this.onOpenFolder,
    required this.onCreate,
  });

  final TextEditingController search;
  final ValueChanged<String> onSearch;
  final VoidCallback onOpenFolder;
  final VoidCallback onCreate;

  @override
  Widget build(BuildContext context) => Wrap(
    alignment: WrapAlignment.spaceBetween,
    crossAxisAlignment: WrapCrossAlignment.end,
    spacing: Space.lg,
    runSpacing: Space.lg,
    children: [
      const Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        spacing: Space.xs,
        children: [
          Text('Projects', style: OrblitText.display),
          Text(
            'Pick up where you left off, or start something new.',
            style: OrblitText.body,
          ),
        ],
      ),
      Row(
        mainAxisSize: MainAxisSize.min,
        spacing: Space.sm,
        children: [
          SizedBox(
            width: 220,
            child: FilterField(
              controller: search,
              hint: 'Search projects',
              onChanged: onSearch,
              height: 36,
              fill: OrblitColors.surface,
              radius: Radii.card,
            ),
          ),
          OrblitButton(
            label: 'Open a folder',
            icon: Icons.folder_open_outlined,
            onPressed: onOpenFolder,
          ),
          OrblitButton(
            label: 'New project',
            icon: Icons.add,
            tone: ButtonTone.primary,
            onPressed: onCreate,
          ),
        ],
      ),
    ],
  );
}

class _Section extends StatelessWidget {
  const _Section({required this.title, required this.child});

  final String title;
  final Widget child;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    spacing: Space.md,
    children: [
      Text(title, style: OrblitText.title),
      child,
    ],
  );
}

/// The projects the editor has opened, or the reason there are none to show.
class _Recent extends StatelessWidget {
  const _Recent({
    required this.loading,
    required this.anyOpened,
    required this.shown,
    required this.onOpen,
    required this.onForget,
  });

  final bool loading;

  /// Whether the editor has opened anything at all, as opposed to whether
  /// the search left anything standing.
  final bool anyOpened;
  final List<Project> shown;
  final ValueChanged<Project> onOpen;
  final ValueChanged<Project> onForget;

  @override
  Widget build(BuildContext context) {
    if (loading) return const SizedBox.shrink();
    if (shown.isNotEmpty) {
      return CardGrid(
        children: [
          for (final project in shown)
            ProjectCard(
              project: project,
              onOpen: () => onOpen(project),
              onForget: () => onForget(project),
            ),
        ],
      );
    }
    return anyOpened
        ? const _Note(
            title: 'Nothing matches',
            detail: 'No project has that in its name.',
          )
        : const _Note(
            title: 'No projects yet',
            detail:
                'Make one from a template, or open a folder that already '
                'has an $projectFileName in it.',
          );
  }
}

class _Note extends StatelessWidget {
  const _Note({required this.title, required this.detail});

  final String title;
  final String detail;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.all(Space.xl),
    decoration: BoxDecoration(
      color: OrblitColors.surface,
      borderRadius: BorderRadius.circular(Radii.window),
      border: Border.all(color: OrblitColors.lineSoft),
    ),
    child: Column(
      spacing: Space.sm,
      children: [
        Text(title, style: OrblitText.title),
        Text(detail, textAlign: TextAlign.center, style: OrblitText.body),
      ],
    ),
  );
}
