import 'package:orblit_motion/orblit_motion.dart';

/// How a bone of the target comes to be moved, as the table shows it.
enum PairHow {
  /// Names and the body decide. There may be nothing that pairs.
  auto,

  /// A bone of the source was chosen for it.
  hand,

  /// Left alone, whatever would pair it.
  none,
}

/// One bone of the target and what moves it.
final class PairRow {
  const PairRow({required this.bone, required this.driver, required this.how});

  final String bone;

  /// The bone of the source that moves [bone], or null for nothing.
  final String? driver;

  final PairHow how;
}

/// A bone map read against the two skeletons it is for.
final class PairTable {
  const PairTable({
    required this.rows,
    required this.map,
    required this.stale,
    required this.problems,
  });

  /// Every bone of the target, in the skeleton's order.
  final List<PairRow> rows;

  /// The map with every name a skeleton lacks taken out, which is what a
  /// retarget can be given. A map made for another model has such names.
  final BoneMap map;

  /// Bones of the target the map names that one of the skeletons lacks.
  final List<String> stale;

  /// What the pairing could not do, as retargeting words it.
  final List<String> problems;

  /// How many bones of the target something moves.
  int get driven => rows.where((row) => row.driver != null).length;
}

/// What [map] does between [from] and [to], bone by bone.
///
/// Pairs are not worked out again here. The map's own pairing is the one a
/// retarget uses, so what the table says is what a clip gets.
PairTable pairTable({
  required RestSkeleton from,
  required RestSkeleton to,
  required BoneMap map,
}) {
  final fitted = map.fitted(from: from, to: to);
  final pairing = fitted.pairing(from: from, to: to);
  return PairTable(
    rows: [
      for (final bone in to.names)
        PairRow(
          bone: bone,
          driver: pairing.drivers[bone],
          how: fitted.bones.containsKey(bone)
              ? PairHow.hand
              : fitted.unpaired.contains(bone)
              ? PairHow.none
              : PairHow.auto,
        ),
    ],
    map: fitted,
    stale: map.stale(from: from, to: to),
    problems: pairing.problems,
  );
}

/// What the retarget dialog is set to: the two models and the pairing.
final class RetargetDraft {
  const RetargetDraft({this.from, this.to, required this.map});

  /// Project paths of the model the clip was made for and the one it is
  /// made to move, or null before either is chosen.
  final String? from;
  final String? to;

  final BoneMap map;

  RetargetDraft copyWith({String? from, String? to, BoneMap? map}) =>
      RetargetDraft(
        from: from ?? this.from,
        to: to ?? this.to,
        map: map ?? this.map,
      );
}

/// The name for [clip] once it moves [model], such as `Walk (dwarf)`.
String retargetedName(String clip, String model) {
  final slash = model.lastIndexOf(RegExp(r'[/\\]'));
  final file = model.substring(slash + 1);
  final dot = file.lastIndexOf('.');
  final stem = dot > 0 ? file.substring(0, dot) : file;
  return '$clip ($stem)';
}
