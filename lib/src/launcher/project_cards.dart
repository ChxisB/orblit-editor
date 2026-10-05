import 'package:flutter/material.dart';

import '../theme/orblit_theme.dart';
import '../widgets/icon_tile.dart';
import 'launcher_screen.dart' show ago;
import 'project.dart';

/// The picture that stands in for a scene on a card: sky over floor, with a
/// glow where the light is. It is decoration, so its colours are not theme
/// tokens.
final class SceneArt {
  const SceneArt(this.sky, this.floor, this.glow, this.glowAt);

  final Color sky;
  final Color floor;
  final Color glow;

  /// Where the glow sits, as an alignment within the picture.
  final Alignment glowAt;

  /// A scene for a project that has none to show yet. The same name always
  /// draws the same picture, so a card is recognised by its look.
  static SceneArt forName(String name) {
    var hash = 0;
    for (final unit in name.codeUnits) {
      hash = (hash * 31 + unit) & 0x7fffffff;
    }
    return _projects[hash % _projects.length];
  }

  static SceneArt forTemplate(ProjectTemplate template) => switch (template) {
    ProjectTemplate.empty => _empty,
    ProjectTemplate.scene => _warm,
    ProjectTemplate.thirdPerson => _green,
  };

  static const _empty = SceneArt(
    Color(0xFF171C25),
    Color(0xFF12161D),
    Color(0x0FFFFFFF),
    Alignment(0.4, -0.2),
  );
  static const _warm = SceneArt(
    Color(0xFF1B2130),
    Color(0xFF141922),
    Color(0x73E5893F),
    Alignment(0.44, -0.16),
  );
  static const _green = SceneArt(
    Color(0xFF17241F),
    Color(0xFF111A16),
    Color(0x4D7FD083),
    Alignment(0.24, -0.12),
  );
  static const _projects = [
    _warm,
    SceneArt(
      Color(0xFF241C2B),
      Color(0xFF171320),
      Color(0x66B58AF0),
      Alignment(-0.4, -0.2),
    ),
    _green,
    SceneArt(
      Color(0xFF2A2119),
      Color(0xFF1B1611),
      Color(0x66E5893F),
      Alignment(0.36, -0.12),
    ),
    SceneArt(
      Color(0xFF16212D),
      Color(0xFF111820),
      Color(0x6162A0E8),
      Alignment(-0.32, -0.16),
    ),
    _empty,
  ];
}

class _SceneThumbnail extends StatelessWidget {
  const _SceneThumbnail({required this.art, required this.height, this.child});

  final SceneArt art;
  final double height;
  final Widget? child;

  @override
  Widget build(BuildContext context) => ClipRRect(
    borderRadius: BorderRadius.circular(Radii.card),
    child: SizedBox(
      height: height,
      child: DecoratedBox(
        decoration: BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [art.sky, art.sky, art.floor, art.floor],
            stops: const [0, 0.56, 0.56, 1],
          ),
        ),
        child: DecoratedBox(
          decoration: BoxDecoration(
            gradient: RadialGradient(
              center: art.glowAt,
              radius: 0.8,
              colors: [art.glow, art.glow.withValues(alpha: 0)],
            ),
          ),
          child: child,
        ),
      ),
    ),
  );
}

/// Cards side by side, as many to a row as the width holds at a readable size.
///
/// Every grid on the launcher uses the same column width, so the template
/// cards sit over the project cards in straight lines.
final class CardGrid extends StatelessWidget {
  const CardGrid({super.key, required this.children});

  final List<Widget> children;

  static const _gap = Space.lg;
  static const _wanted = 280.0;
  static const _mostColumns = 3;

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, constraints) {
      final columns = ((constraints.maxWidth + _gap) / (_wanted + _gap))
          .floor()
          .clamp(1, _mostColumns);
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        spacing: _gap,
        children: [
          for (var start = 0; start < children.length; start += columns)
            // Stretched to the tallest card in the row, so a card with a
            // longer description does not leave its neighbours short.
            IntrinsicHeight(
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                spacing: _gap,
                children: [
                  for (var i = start; i < start + columns; i++)
                    Expanded(
                      child: i < children.length
                          ? children[i]
                          : const SizedBox.shrink(),
                    ),
                ],
              ),
            ),
        ],
      );
    },
  );
}

/// What a card sits in: a raised panel that lights on hover.
///
/// [builder] is told whether the pointer is over the card, for the parts that
/// only appear then.
class _CardShell extends StatefulWidget {
  const _CardShell({
    required this.label,
    required this.onTap,
    required this.builder,
    this.enabled = true,
  });

  final String label;
  final VoidCallback onTap;
  final Widget Function(bool hovering) builder;

  /// False for a project whose folder has gone, which cannot be opened.
  final bool enabled;

  @override
  State<_CardShell> createState() => _CardShellState();
}

class _CardShellState extends State<_CardShell> {
  bool _hovering = false;

  @override
  Widget build(BuildContext context) => Semantics(
    button: true,
    label: widget.label,
    child: MouseRegion(
      cursor: widget.enabled
          ? SystemMouseCursors.click
          : SystemMouseCursors.basic,
      onEnter: (_) => setState(() => _hovering = true),
      onExit: (_) => setState(() => _hovering = false),
      child: GestureDetector(
        onTap: widget.onTap,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 90),
          padding: const EdgeInsets.fromLTRB(8, 8, 8, 14),
          decoration: BoxDecoration(
            color: _hovering ? OrblitColors.raised : OrblitColors.surface,
            borderRadius: BorderRadius.circular(Radii.window),
            border: Border.all(color: OrblitColors.lineSoft),
          ),
          child: widget.builder(_hovering),
        ),
      ),
    ),
  );
}

/// A way to start a project, with a picture of what it starts as.
final class TemplateCard extends StatelessWidget {
  const TemplateCard({super.key, required this.template, required this.onTap});

  final ProjectTemplate template;
  final VoidCallback onTap;

  String get _tag => switch (template) {
    ProjectTemplate.empty => 'Blank',
    ProjectTemplate.scene || ProjectTemplate.thirdPerson => '3D',
  };

  @override
  Widget build(BuildContext context) => _CardShell(
    label: 'Start a project from ${template.label}',
    onTap: onTap,
    builder: (_) => Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      spacing: Space.md,
      children: [
        _SceneThumbnail(
          art: SceneArt.forTemplate(template),
          height: 96,
          child: Align(
            alignment: Alignment.topLeft,
            child: Padding(
              padding: const EdgeInsets.all(Space.sm),
              child: _Badge(_tag),
            ),
          ),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 6),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            spacing: 2,
            children: [
              Text(template.label, style: OrblitText.panelTitle),
              Text(template.description, style: OrblitText.label),
            ],
          ),
        ),
      ],
    ),
  );
}

/// A project the editor has opened, with the way to take it off the list.
final class ProjectCard extends StatelessWidget {
  const ProjectCard({
    super.key,
    required this.project,
    required this.onOpen,
    required this.onForget,
  });

  final Project project;
  final VoidCallback onOpen;
  final VoidCallback onForget;

  @override
  Widget build(BuildContext context) {
    // A project whose folder has gone is shown rather than hidden, so someone
    // who moved a directory sees why it will not open instead of wondering
    // where their work went.
    final missing = !project.exists;

    return _CardShell(
      label: 'Open ${project.name}',
      onTap: onOpen,
      enabled: !missing,
      builder: (hovering) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        spacing: Space.md,
        children: [
          _SceneThumbnail(
            art: missing ? _gone : SceneArt.forName(project.name),
            height: 124,
            child: Stack(
              children: [
                if (missing)
                  const Center(
                    child: Icon(
                      Icons.link_off,
                      size: 22,
                      color: OrblitColors.inkDim,
                    ),
                  ),
                if (hovering)
                  Positioned(
                    top: Space.sm,
                    right: Space.sm,
                    child: IconTile(
                      tooltip: 'Remove from this list',
                      size: 26,
                      iconSize: 14,
                      radius: Radii.control,
                      onTap: onForget,
                      child: const Icon(Icons.close),
                    ),
                  ),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 6),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              spacing: 2,
              children: [
                Row(
                  spacing: Space.sm,
                  children: [
                    Flexible(
                      child: Text(
                        project.name,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: OrblitText.panelTitle.copyWith(
                          fontSize: 13,
                          color: missing
                              ? OrblitColors.inkDim
                              : OrblitColors.ink,
                        ),
                      ),
                    ),
                    if (missing) const _Tag('moved', tone: OrblitColors.warn),
                  ],
                ),
                Text(
                  project.displayPath,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: OrblitText.mono,
                ),
                Text(
                  'Opened ${ago(project.lastOpened)}',
                  style: OrblitText.label,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  static const _gone = SceneArt(
    OrblitColors.ground,
    OrblitColors.ground,
    Color(0x00000000),
    Alignment.center,
  );
}

/// A small label laid over a picture.
class _Badge extends StatelessWidget {
  const _Badge(this.label);

  final String label;

  @override
  Widget build(BuildContext context) => DecoratedBox(
    decoration: BoxDecoration(
      // Mostly opaque, so the word reads over any sky.
      color: OrblitColors.ground.withValues(alpha: 0.72),
      borderRadius: BorderRadius.circular(5),
    ),
    child: Padding(
      padding: const EdgeInsets.symmetric(horizontal: Space.sm, vertical: 2),
      child: Text(label, style: OrblitText.mono.copyWith(fontSize: 11)),
    ),
  );
}

class _Tag extends StatelessWidget {
  const _Tag(this.label, {required this.tone});

  final String label;
  final Color tone;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 1),
    decoration: BoxDecoration(
      color: tone.withValues(alpha: 0.12),
      borderRadius: BorderRadius.circular(Radii.control / 2),
      border: Border.all(color: tone.withValues(alpha: 0.4)),
    ),
    child: Text(
      label,
      style: OrblitText.caption.copyWith(fontSize: 10, color: tone),
    ),
  );
}
