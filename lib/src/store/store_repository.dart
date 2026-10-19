import 'dart:async';

import 'package:flutter/material.dart';

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'store_session.dart';
import 'store_settings.dart';

/// The store being read, the stores a person can switch to, and buttons to
/// add one and to forget one that was added.
class RepositoryPicker extends StatelessWidget {
  const RepositoryPicker({super.key, required this.session});

  final StoreSession session;

  Future<void> _add(BuildContext context) async {
    final text = await showDialog<String>(
      context: context,
      builder: (context) => AddStorePrompt(settings: session.settings),
    );
    if (text != null) await session.addRepository(text);
  }

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        SizedBox(
          width: 240,
          child: DropdownButton<Uri>(
            value: session.repository,
            isExpanded: true,
            dropdownColor: OrblitColors.raised,
            underline: const SizedBox.shrink(),
            style: OrblitText.label,
            items: [
              for (final address in session.settings.repositories)
                DropdownMenuItem(
                  value: address,
                  child: Text(
                    '${address.host}${address.path}',
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
            ],
            onChanged: (address) {
              if (address != null) unawaited(session.useRepository(address));
            },
          ),
        ),
        IconButton(
          tooltip: 'Add a store',
          icon: const Icon(Icons.add, size: 16, color: OrblitColors.inkMid),
          onPressed: () => unawaited(_add(context)),
        ),
        if (session.repository != defaultStoreAddress)
          IconButton(
            tooltip: 'Forget this store',
            icon: const Icon(
              Icons.remove,
              size: 16,
              color: OrblitColors.inkMid,
            ),
            onPressed: () =>
                unawaited(session.removeRepository(session.repository)),
          ),
      ],
    );
  }
}

/// Asks for the address of a store and says at once why one cannot be used.
/// Pops the text when the person adds it.
class AddStorePrompt extends StatefulWidget {
  const AddStorePrompt({super.key, required this.settings});

  final StoreSettings settings;

  @override
  State<AddStorePrompt> createState() => _AddStorePromptState();
}

class _AddStorePromptState extends State<AddStorePrompt> {
  final TextEditingController _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  String? get _problem => _controller.text.trim().isEmpty
      ? null
      : widget.settings.repositoryProblem(_controller.text);

  bool get _ready => _controller.text.trim().isNotEmpty && _problem == null;

  void _accept() {
    if (_ready) Navigator.of(context).pop(_controller.text.trim());
  }

  @override
  Widget build(BuildContext context) {
    final problem = _problem;
    return AlertDialog(
      backgroundColor: OrblitColors.surface,
      title: const Text('Add a store', style: OrblitText.title),
      content: SizedBox(
        width: 420,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            OrblitField(
              controller: _controller,
              hint: 'https://example.com/store/',
              mono: true,
              autofocus: true,
              onChanged: (_) => setState(() {}),
              onSubmitted: (_) => _accept(),
            ),
            const SizedBox(height: Space.sm),
            Text(
              problem ?? 'The address of a folder that holds index.json.',
              style: problem == null
                  ? OrblitText.caption
                  : OrblitText.caption.copyWith(color: OrblitColors.bad),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
        TextButton(
          onPressed: _ready ? _accept : null,
          child: const Text('Add'),
        ),
      ],
    );
  }
}
