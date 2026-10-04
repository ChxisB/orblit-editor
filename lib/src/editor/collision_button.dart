import 'package:flutter/material.dart';
import 'package:orblit_asset/orblit_asset.dart';

import '../widgets/controls.dart';

/// Reads geometry on a click, then passes the completed collider to history.
final class CollisionButton extends StatefulWidget {
  const CollisionButton({super.key, required this.read, required this.put});

  final Future<CollisionMesh> Function() read;
  final ValueChanged<CollisionMesh> put;

  @override
  State<CollisionButton> createState() => _CollisionButtonState();
}

final class _CollisionButtonState extends State<CollisionButton> {
  bool _busy = false;

  Future<void> _collide() async {
    setState(() => _busy = true);
    try {
      final geometry = await widget.read();
      if (!mounted) return;
      widget.put(geometry);
    } on Exception catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.maybeOf(context)?.showSnackBar(
        SnackBar(content: Text('Could not make this collider: $error')),
      );
    } on ArgumentError catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.maybeOf(context)?.showSnackBar(
        SnackBar(
          content: Text('Could not make this collider: ${error.message}'),
        ),
      );
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => OrblitButton(
    label: _busy ? 'Reading model…' : 'Collide with this model',
    tooltip: 'Use the model triangles as a fixed collision surface.',
    icon: Icons.grid_on_outlined,
    tone: ButtonTone.quiet,
    expand: true,
    onPressed: _busy ? null : _collide,
  );
}
