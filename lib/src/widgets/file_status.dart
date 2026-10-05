import 'package:flutter/widgets.dart';

import '../theme/orblit_theme.dart';

/// Which file a workspace is working on, and whether what is in front of
/// somebody is what is on disk.
///
/// A dot and a word, because a dot alone is a colour somebody has to know the
/// meaning of.
final class FileStatus extends StatelessWidget {
  const FileStatus({super.key, required this.file, required this.unsaved});

  final String file;
  final bool unsaved;

  @override
  Widget build(BuildContext context) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      Flexible(
        child: Text(
          file,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: OrblitText.mono.copyWith(
            fontSize: 11.5,
            color: OrblitColors.inkDim,
          ),
        ),
      ),
      const SizedBox(width: Space.md),
      Container(
        width: 6,
        height: 6,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          color: unsaved ? OrblitColors.ember : OrblitColors.good,
        ),
      ),
      const SizedBox(width: 6),
      Text(
        unsaved ? 'Unsaved' : 'Saved',
        maxLines: 1,
        style: OrblitText.caption.copyWith(fontSize: 11.5),
      ),
    ],
  );
}
