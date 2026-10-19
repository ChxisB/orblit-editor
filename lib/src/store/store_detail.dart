import 'dart:async';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:orblit_asset/orblit_asset.dart';

import '../editor/assets.dart' show sizeLabel;
import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'store_session.dart';

/// One kit of the store: what it is, who made it, what it needs, what is in
/// it, and a button to install the release shown.
class StoreDetail extends StatelessWidget {
  const StoreDetail({
    super.key,
    required this.session,
    required this.onInstall,
  });

  final StoreSession session;

  /// Called with the kit and the release the person is looking at.
  final void Function(KitRow row, KitRelease release) onInstall;

  @override
  Widget build(BuildContext context) {
    final row = session.selected!; // The dialog shows this view only for one.
    final chosen = session.chosen;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            OrblitButton(
              label: 'All kits',
              icon: Icons.arrow_back,
              tone: ButtonTone.flat,
              onPressed: () => unawaited(session.select(null)),
            ),
            const Spacer(),
            OrblitButton(
              label: 'Install…',
              icon: Icons.download_outlined,
              tone: ButtonTone.primary,
              onPressed: chosen == null
                  ? null
                  : () => onInstall(row, chosen.release),
            ),
          ],
        ),
        const SizedBox(height: Space.sm),
        Expanded(
          child: _Body(session: session, row: row),
        ),
      ],
    );
  }
}

class _Body extends StatelessWidget {
  const _Body({required this.session, required this.row});

  final StoreSession session;
  final KitRow row;

  @override
  Widget build(BuildContext context) {
    final manifest = row.manifest;
    final releases = session.releases;
    final chosen = session.chosen;
    final files = session.files;
    final problem = session.problem;
    return CustomScrollView(
      slivers: [
        SliverToBoxAdapter(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(manifest.name, style: OrblitText.title),
              Text(
                '${manifest.publisher} · ${manifest.version}',
                style: OrblitText.caption,
              ),
              const SizedBox(height: Space.sm),
              Text(manifest.summary, style: OrblitText.body),
              const SizedBox(height: Space.md),
              if (problem != null)
                Text(
                  problem,
                  style: OrblitText.caption.copyWith(color: OrblitColors.bad),
                )
              else if (releases == null)
                const _Waiting()
              else ...[
                if (releases.previews.isNotEmpty)
                  _Previews(session: session, names: releases.previews),
                _Facts(session: session, row: row),
                if (chosen != null)
                  _ReleaseChoice(session: session, chosen: chosen),
                _FilesHeader(files: files),
              ],
            ],
          ),
        ),
        if (files != null)
          SliverList.builder(
            itemCount: files.length,
            itemBuilder: (context, index) => _FileRow(file: files[index]),
          ),
      ],
    );
  }
}

class _Waiting extends StatelessWidget {
  const _Waiting();

  @override
  Widget build(BuildContext context) {
    return const Padding(
      padding: EdgeInsets.all(Space.lg),
      child: Center(
        child: SizedBox(
          width: 20,
          height: 20,
          child: CircularProgressIndicator(strokeWidth: 2),
        ),
      ),
    );
  }
}

class _Previews extends StatelessWidget {
  const _Previews({required this.session, required this.names});

  final StoreSession session;
  final List<String> names;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: Space.md),
      child: SizedBox(
        height: 120,
        child: ListView.separated(
          scrollDirection: Axis.horizontal,
          itemCount: names.length,
          separatorBuilder: (context, index) => const SizedBox(width: Space.sm),
          itemBuilder: (context, index) => ClipRRect(
            borderRadius: BorderRadius.circular(Radii.control),
            child: AspectRatio(
              aspectRatio: 16 / 9,
              child: _Picture(image: session.preview(names[index])),
            ),
          ),
        ),
      ),
    );
  }
}

class _Picture extends StatelessWidget {
  const _Picture({required this.image});

  final Future<Uint8List?> image;

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<Uint8List?>(
      future: image,
      builder: (context, snapshot) {
        final bytes = snapshot.data;
        return bytes == null
            ? const ColoredBox(color: OrblitColors.lineSoft)
            : Image.memory(
                bytes,
                fit: BoxFit.cover,
                errorBuilder: (context, error, stack) =>
                    const ColoredBox(color: OrblitColors.lineSoft),
              );
      },
    );
  }
}

/// The licence, the engine range and the tags.
class _Facts extends StatelessWidget {
  const _Facts({required this.session, required this.row});

  final StoreSession session;
  final KitRow row;

  @override
  Widget build(BuildContext context) {
    final manifest = row.manifest;
    final range = '${manifest.engine.from} to ${manifest.engine.to}';
    return Padding(
      padding: const EdgeInsets.only(bottom: Space.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SelectableText(
            'Licence: ${manifest.licence.spdx}, ${manifest.licence.url}',
            style: OrblitText.caption,
          ),
          Text('For engine $range', style: OrblitText.caption),
          if (!session.fits(row))
            Text(
              'This project uses engine ${session.engine}, which is outside '
              'that range. The kit may not work.',
              style: OrblitText.caption.copyWith(color: OrblitColors.warn),
            ),
          if (manifest.tags.isNotEmpty)
            Text(
              'Tags: ${manifest.tags.join(', ')}',
              style: OrblitText.caption,
            ),
        ],
      ),
    );
  }
}

class _ReleaseChoice extends StatelessWidget {
  const _ReleaseChoice({required this.session, required this.chosen});

  final StoreSession session;
  final PublishedRelease chosen;

  @override
  Widget build(BuildContext context) {
    final release = chosen.release;
    return Padding(
      padding: const EdgeInsets.only(bottom: Space.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const SectionLabel('Release'),
          DropdownButton<PublishedRelease>(
            value: chosen,
            dropdownColor: OrblitColors.raised,
            underline: const SizedBox.shrink(),
            style: OrblitText.label,
            items: [
              for (final item in session.releases!.releases)
                DropdownMenuItem(
                  value: item,
                  child: Text(
                    '${item.release.version} · ${item.release.released}',
                  ),
                ),
            ],
            onChanged: (item) {
              if (item != null) unawaited(session.chooseRelease(item));
            },
          ),
          Text(
            '${sizeLabel(release.bytes)}. ${release.notes}',
            style: OrblitText.caption,
          ),
          if (chosen.containsScripts)
            Text(
              'Contains scripts. You see them before anything is installed.',
              style: OrblitText.caption.copyWith(color: OrblitColors.warn),
            ),
        ],
      ),
    );
  }
}

class _FilesHeader extends StatelessWidget {
  const _FilesHeader({required this.files});

  final List<MapEntry<AssetId, int>>? files;

  @override
  Widget build(BuildContext context) {
    final list = files;
    return Padding(
      padding: const EdgeInsets.only(bottom: Space.xs),
      child: list == null
          ? const Text(
              'The file list is not available from this store. You will see '
              'the files before anything is installed.',
              style: OrblitText.caption,
            )
          : SectionLabel('Files (${list.length})'),
    );
  }
}

class _FileRow extends StatelessWidget {
  const _FileRow({required this.file});

  final MapEntry<AssetId, int> file;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: Text(
            '${file.key}',
            style: OrblitText.mono,
            overflow: TextOverflow.ellipsis,
          ),
        ),
        Text(sizeLabel(file.value), style: OrblitText.caption),
      ],
    );
  }
}
