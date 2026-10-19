import 'dart:async';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:orblit_asset/orblit_asset.dart';

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import '../widgets/orblit_switch.dart';
import 'store_query.dart';
import 'store_session.dart';

/// The kits of one store: words and filters above, a grid of cards below.
class StoreBrowse extends StatefulWidget {
  const StoreBrowse({super.key, required this.session});

  final StoreSession session;

  @override
  State<StoreBrowse> createState() => _StoreBrowseState();
}

class _StoreBrowseState extends State<StoreBrowse> {
  // Starts from the query, so coming back from a kit keeps the words typed.
  late final TextEditingController _search = TextEditingController(
    text: widget.session.query.text,
  );

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final session = widget.session;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        OrblitField(
          controller: _search,
          hint: 'Search kits',
          onChanged: (text) =>
              session.setQuery(session.query.copyWith(text: text)),
        ),
        const SizedBox(height: Space.sm),
        _Filters(session: session),
        if (!session.engineKnown)
          const Padding(
            padding: EdgeInsets.only(top: Space.xs),
            child: Text(
              'This project does not say which engine it uses, so kits for '
              'any version are listed.',
              style: OrblitText.caption,
            ),
          ),
        const SizedBox(height: Space.sm),
        Expanded(child: _Results(session: session)),
      ],
    );
  }
}

class _Filters extends StatelessWidget {
  const _Filters({required this.session});

  final StoreSession session;

  @override
  Widget build(BuildContext context) {
    final query = session.query;
    return Wrap(
      spacing: Space.md,
      runSpacing: Space.xs,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        _Pick<StoreSort>(
          label: 'Sort',
          value: query.sort,
          options: const {StoreSort.updated: 'Newest', StoreSort.name: 'Name'},
          onChanged: (sort) => session.setQuery(query.copyWith(sort: sort)),
        ),
        _Pick<String>(
          label: 'Licence',
          value: query.licence,
          options: {
            '': 'Any',
            for (final x in storeLicences(session.rows)) x: x,
          },
          onChanged: (x) => session.setQuery(query.copyWith(licence: x)),
        ),
        _Pick<String>(
          label: 'Tag',
          value: query.tag,
          options: {'': 'Any', for (final x in storeTags(session.rows)) x: x},
          onChanged: (x) => session.setQuery(query.copyWith(tag: x)),
        ),
        _Toggle(
          label: 'Hide kits with scripts',
          value: query.hideScripts,
          onChanged: (x) => session.setQuery(query.copyWith(hideScripts: x)),
        ),
        _Toggle(
          label: 'Show kits for other engines',
          value: session.settings.showIncompatible,
          onChanged: session.setShowIncompatible,
        ),
      ],
    );
  }
}

class _Pick<T> extends StatelessWidget {
  const _Pick({
    required this.label,
    required this.value,
    required this.options,
    required this.onChanged,
  });

  final String label;
  final T value;
  final Map<T, String> options;
  final ValueChanged<T> onChanged;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text('$label  ', style: OrblitText.caption),
        DropdownButton<T>(
          // A licence or tag that left the list when the store changed.
          value: options.containsKey(value) ? value : options.keys.first,
          dropdownColor: OrblitColors.raised,
          underline: const SizedBox.shrink(),
          style: OrblitText.label,
          items: [
            for (final MapEntry(:key, :value) in options.entries)
              DropdownMenuItem(value: key, child: Text(value)),
          ],
          onChanged: (picked) {
            if (picked != null) onChanged(picked);
          },
        ),
      ],
    );
  }
}

class _Toggle extends StatelessWidget {
  const _Toggle({
    required this.label,
    required this.value,
    required this.onChanged,
  });

  final String label;
  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        OrblitSwitch(label: label, value: value, onChanged: onChanged),
        const SizedBox(width: Space.sm),
        Text(label, style: OrblitText.caption),
      ],
    );
  }
}

class _Results extends StatelessWidget {
  const _Results({required this.session});

  final StoreSession session;

  @override
  Widget build(BuildContext context) {
    if (session.loading) {
      return const Center(
        child: SizedBox(
          width: 20,
          height: 20,
          child: CircularProgressIndicator(strokeWidth: 2),
        ),
      );
    }
    final problem = session.problem;
    if (problem != null) {
      return Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(problem, style: OrblitText.caption, textAlign: TextAlign.center),
          const SizedBox(height: Space.md),
          OrblitButton(
            label: 'Try again',
            onPressed: () => unawaited(session.goOnline()),
          ),
        ],
      );
    }
    final shown = session.shown;
    if (shown.isEmpty) {
      return PanelMessage(
        session.rows.isEmpty
            ? 'This store lists no kits.'
            : session.settings.showIncompatible
            ? 'No kit matches.'
            : 'No kit matches. Kits for other engine versions are hidden.',
      );
    }
    return GridView.builder(
      gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
        maxCrossAxisExtent: 200,
        mainAxisExtent: 190,
        crossAxisSpacing: Space.md,
        mainAxisSpacing: Space.md,
      ),
      itemCount: shown.length,
      itemBuilder: (context, index) => _KitCard(
        session: session,
        row: shown[index],
        fits: session.fits(shown[index]),
      ),
    );
  }
}

class _KitCard extends StatelessWidget {
  const _KitCard({
    required this.session,
    required this.row,
    required this.fits,
  });

  final StoreSession session;
  final KitRow row;
  final bool fits;

  @override
  Widget build(BuildContext context) {
    final manifest = row.manifest;
    return Material(
      color: OrblitColors.raised,
      borderRadius: BorderRadius.circular(Radii.card),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => unawaited(session.select(row)),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Expanded(child: _Thumbnail(image: session.thumbnail(row))),
            Padding(
              padding: const EdgeInsets.all(Space.sm),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    manifest.name,
                    style: OrblitText.label,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  Text(
                    '${manifest.publisher} · ${manifest.version}',
                    style: OrblitText.caption,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  Text(
                    [
                      if (manifest.containsScripts) 'Scripts',
                      if (!fits) 'Other engine',
                    ].join(' · '),
                    style: OrblitText.caption.copyWith(
                      color: OrblitColors.warn,
                    ),
                    maxLines: 1,
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// A card's picture. The session hands out one future for a row, so a rebuild
/// of the grid does not start another read.
class _Thumbnail extends StatelessWidget {
  const _Thumbnail({required this.image});

  final Future<Uint8List?> image;

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<Uint8List?>(
      future: image,
      builder: (context, snapshot) {
        final bytes = snapshot.data;
        if (bytes == null) {
          return const ColoredBox(
            color: OrblitColors.lineSoft,
            child: Center(
              child: Icon(
                Icons.inventory_2_outlined,
                color: OrblitColors.inkDim,
              ),
            ),
          );
        }
        return Image.memory(
          bytes,
          fit: BoxFit.cover,
          // A file the store sent that is not a picture draws as the empty
          // card instead of an error.
          errorBuilder: (context, error, stack) =>
              const ColoredBox(color: OrblitColors.lineSoft),
        );
      },
    );
  }
}
