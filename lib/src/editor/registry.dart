/// Something the editor looks up by name.
abstract interface class Registered {
  /// Unique among its own sort: two panels cannot share a name, but a panel
  /// and a mode can.
  String get name;
}

/// The things of one sort the editor can show, by name and in order.
///
/// A list rather than a map underneath, because the order is part of what
/// is registered: it is the order the View menu lists panels in, and the
/// order the inspector stacks its sections.
///
/// This is the seam that keeps the editor core from having to know every
/// panel, section and gizmo there is. Something new registers itself here,
/// and the shell shows it without being told its name.
class Registry<T extends Registered> {
  final List<T> _entries = [];

  /// Adds [entry], after the others or just ahead of the one called [before].
  ///
  /// A name registered twice is a mistake in the code, not something a user
  /// did, so it throws rather than quietly keeping one of them.
  void register(T entry, {String? before}) {
    if (this[entry.name] != null) {
      throw StateError('Two things registered as "${entry.name}".');
    }
    final at = before == null
        ? -1
        : _entries.indexWhere((one) => one.name == before);
    if (at < 0) {
      _entries.add(entry);
    } else {
      _entries.insert(at, entry);
    }
  }

  /// The one called [name], or null when nothing is.
  T? operator [](String name) {
    for (final entry in _entries) {
      if (entry.name == name) return entry;
    }
    return null;
  }

  /// Everything registered, in order.
  List<T> get all => List.unmodifiable(_entries);
}
