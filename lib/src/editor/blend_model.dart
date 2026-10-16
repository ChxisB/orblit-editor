import 'dart:math' as math;

import 'package:orblit_motion/orblit_motion.dart';

// What the blend graph editor knows about a blend, apart from drawing it:
// where each state is placed, which arrows join them, how a condition reads
// as words, and every change it makes as a function from one blend to the
// next. Nothing here reads a file or a widget.

/// Where a state is placed in the drawn graph.
typedef BlendCell = ({int column, int row});

/// Every change from one state to another, drawn as one arrow.
final class BlendEdge {
  const BlendEdge({
    required this.from,
    required this.to,
    required this.changes,
  });

  final String from;
  final String to;

  /// Indexes into [BlendDocument.writtenChanges], in order of precedence.
  final List<int> changes;
}

/// The state of [blend], as a file writes its states, that [name] belongs
/// to: the state itself, or the one whose nested graph holds a leaf named
/// `state/leaf`. Null for a name no state has.
String? stateOwning(BlendDocument blend, String name) {
  final names = {for (final state in blend.writtenStates) state.name};
  if (names.contains(name)) return name;
  final slash = name.indexOf('/');
  if (slash <= 0) return null;
  final owner = name.substring(0, slash);
  return names.contains(owner) ? owner : null;
}

/// One arrow for each pair of states a change joins.
///
/// A change from anywhere has no arrow, and neither does one that goes
/// nowhere a state is. [changesFromAnywhere] answers the first.
List<BlendEdge> blendEdges(BlendDocument blend) {
  final byPair = <(String, String), List<int>>{};
  for (final (index, change) in blend.writtenChanges.indexed) {
    final from = change.from;
    if (from == null) continue;
    final source = stateOwning(blend, from);
    final target = stateOwning(blend, change.to);
    if (source == null || target == null) continue;
    (byPair[(source, target)] ??= []).add(index);
  }
  return [
    for (final MapEntry(:key, :value) in byPair.entries)
      BlendEdge(from: key.$1, to: key.$2, changes: value),
  ];
}

/// Indexes into [BlendDocument.writtenChanges] of the changes that can be
/// taken from any state, by the state each one enters.
Map<String, List<int>> changesFromAnywhere(BlendDocument blend) {
  final out = <String, List<int>>{};
  for (final (index, change) in blend.writtenChanges.indexed) {
    if (change.from != null) continue;
    final target = stateOwning(blend, change.to);
    if (target != null) (out[target] ??= []).add(index);
  }
  return out;
}

/// Where each state goes: a column for how many changes it is from the
/// start, a row for where it came among them. A state nothing leads to is
/// put in a column after the last, so it is seen rather than lost.
///
/// Worked out and not stored. A file has no positions, so two people who
/// open one see the same graph and a merge cannot conflict over a drag.
Map<String, BlendCell> layoutBlend(BlendDocument blend) {
  final names = [for (final state in blend.writtenStates) state.name];
  final start = stateOwning(blend, blend.writtenStart) ?? names.first;
  final leadsTo = <String, List<String>>{};
  for (final edge in blendEdges(blend)) {
    (leadsTo[edge.from] ??= []).add(edge.to);
  }
  final column = <String, int>{start: 0};
  final order = [start];
  for (var i = 0; i < order.length; i++) {
    for (final to in leadsTo[order[i]] ?? const <String>[]) {
      if (column.containsKey(to)) continue;
      column[to] = column[order[i]]! + 1;
      order.add(to);
    }
  }
  final after = column.values.fold(0, math.max) + 1;
  for (final name in names) {
    if (column.containsKey(name)) continue;
    column[name] = after;
    order.add(name);
  }
  final rows = <int, int>{};
  return {
    for (final name in order)
      name: (
        column: column[name]!,
        row: rows.update(column[name]!, (n) => n + 1, ifAbsent: () => 0),
      ),
  };
}

/// What kind of test a condition is, which is what a person chooses.
enum ConditionKind {
  always('Always'),
  above('Above'),
  below('Below'),
  on('Is on'),
  through('Laps through'),
  all('All of'),
  any('Any of'),
  not('Not');

  const ConditionKind(this.label);

  final String label;

  /// Whether it is made of other conditions.
  bool get isComposite =>
      this == ConditionKind.all ||
      this == ConditionKind.any ||
      this == ConditionKind.not;

  /// Whether it tests one of the blend's inputs.
  bool get readsInput =>
      this == ConditionKind.above ||
      this == ConditionKind.below ||
      this == ConditionKind.on;
}

ConditionKind conditionKind(BlendCondition condition) {
  final json = condition.toJson();
  for (final (key, kind) in const [
    ('all', ConditionKind.all),
    ('any', ConditionKind.any),
    ('not', ConditionKind.not),
    ('through', ConditionKind.through),
    ('above', ConditionKind.above),
    ('below', ConditionKind.below),
  ]) {
    if (json.containsKey(key)) return kind;
  }
  return json.isEmpty ? ConditionKind.always : ConditionKind.on;
}

/// The input a test reads, or an empty name for a condition with none.
String conditionInput(BlendCondition condition) {
  final input = condition.toJson()['input'];
  return input is String ? input : '';
}

/// The number a test compares with or waits for, or zero.
double conditionNumber(BlendCondition condition) {
  final json = condition.toJson();
  final value = json['above'] ?? json['below'] ?? json['through'];
  return value is num ? value.toDouble() : 0;
}

/// What a composite is made of, outermost first.
List<BlendCondition> conditionParts(BlendCondition condition) {
  final json = condition.toJson();
  final raw = json['all'] ?? json['any'] ?? json['not'];
  final parts = raw is List ? raw : [raw];
  return [
    for (final part in parts)
      if (part is Map<String, Object?>)
        BlendCondition.fromJson(part, 'a condition', []) ??
            BlendCondition.always,
  ];
}

/// A condition of [kind], keeping what [old] has that still means something.
BlendCondition conditionOfKind(ConditionKind kind, BlendCondition old) {
  final input = conditionInput(old);
  final named = input.isEmpty ? 'input' : input;
  return switch (kind) {
    ConditionKind.always => BlendCondition.always,
    ConditionKind.above => BlendCondition.above(named, conditionNumber(old)),
    ConditionKind.below => BlendCondition.below(named, conditionNumber(old)),
    ConditionKind.on => BlendCondition.on(named),
    ConditionKind.through => BlendCondition.through(
      conditionKind(old) == ConditionKind.through ? conditionNumber(old) : 1,
    ),
    ConditionKind.all => BlendCondition.all([
      if (conditionKind(old) != ConditionKind.always) old,
    ]),
    ConditionKind.any => BlendCondition.any([
      if (conditionKind(old) != ConditionKind.always) old,
    ]),
    ConditionKind.not => BlendCondition.not(old),
  };
}

/// [condition] testing [input] instead, or the same condition when it has
/// no input or [input] is no name.
BlendCondition conditionReading(BlendCondition condition, String input) {
  if (input.isEmpty) return condition;
  final value = conditionNumber(condition);
  return switch (conditionKind(condition)) {
    ConditionKind.above => BlendCondition.above(input, value),
    ConditionKind.below => BlendCondition.below(input, value),
    ConditionKind.on => BlendCondition.on(input),
    _ => condition,
  };
}

/// [condition] with its number set to [value].
BlendCondition conditionNumbered(BlendCondition condition, double value) {
  if (!value.isFinite) return condition;
  final input = conditionInput(condition);
  return switch (conditionKind(condition)) {
    ConditionKind.above => BlendCondition.above(input, value),
    ConditionKind.below => BlendCondition.below(input, value),
    ConditionKind.through => BlendCondition.through(value),
    _ => condition,
  };
}

/// A composite with [parts] in place of what it was made of.
BlendCondition conditionMadeOf(
  BlendCondition condition,
  List<BlendCondition> parts,
) => switch (conditionKind(condition)) {
  ConditionKind.all => BlendCondition.all(parts),
  ConditionKind.any => BlendCondition.any(parts),
  ConditionKind.not => BlendCondition.not(
    parts.firstOrNull ?? BlendCondition.always,
  ),
  _ => condition,
};

/// A condition as a short phrase, such as `speed above 0.1`.
String describeCondition(BlendCondition condition) {
  final kind = conditionKind(condition);
  final input = conditionInput(condition);
  final number = numberText(conditionNumber(condition));
  return switch (kind) {
    ConditionKind.always => 'always',
    ConditionKind.above => '$input above $number',
    ConditionKind.below => '$input below $number',
    ConditionKind.on => '$input is on',
    ConditionKind.through =>
      conditionNumber(condition) == 1
          ? 'when it is done'
          : 'after $number laps',
    ConditionKind.all => _joined(condition, ' and '),
    ConditionKind.any => _joined(condition, ' or '),
    ConditionKind.not =>
      'not ${conditionParts(condition).map(describeCondition).join()}',
  };
}

String _joined(BlendCondition condition, String word) {
  final parts = conditionParts(condition).map(describeCondition).toList();
  return parts.isEmpty ? 'always' : parts.join(word);
}

/// A number with no trailing zeros, to three places.
String numberText(double value) {
  final text = value.toStringAsFixed(3);
  return text.replaceFirst(RegExp(r'\.?0+$'), '');
}

/// What a state plays as a phrase.
String describeSource(BlendSource source) => switch (source) {
  BlendClip(:final clip) => clip,
  BlendLine(:final input, :final points) =>
    '${points.length} points along $input',
  BlendPlane(:final x, :final y, :final points) =>
    '${points.length} points across $x and $y',
  BlendGraph(:final graph) => 'a graph of ${graph.states.length} states',
};

/// The line [source] is, when every point of it plays one clip. A line
/// with a mix inside it is left to the file.
BlendLine? lineOfClips(BlendSource source) {
  if (source is! BlendLine) return null;
  return source.points.every((point) => point.plays is BlendClip)
      ? source
      : null;
}

/// Whether the editor changes what [source] plays. A plane, a graph and a
/// line with a mix inside it are read and not changed.
bool canEditSource(BlendSource source) =>
    source is BlendClip || lineOfClips(source) != null;

/// The first clip [source] names, or `clip` for one that names none.
String firstClipOf(BlendSource source) => source.clips.firstOrNull ?? 'clip';

/// A line along [input] that plays [clip] at nought and at one, which is
/// somewhere to start from when a state that played one clip is to mix.
BlendLine lineFrom(String clip, String input) => BlendLine(input, [
  LinePoint(0, BlendClip(clip)),
  LinePoint(1, BlendClip(clip)),
]);

/// [line] along [input] instead.
BlendLine lineAlong(BlendLine line, String input) =>
    BlendLine(input, line.points);

/// [line] with point [index] moved to [at] and playing [clip], each when
/// given. The points are put in order again, so the point may not stay at
/// [index].
BlendLine linePointChanged(
  BlendLine line,
  int index, {
  double? at,
  String? clip,
}) => BlendLine(line.input, [
  for (final (i, point) in line.points.indexed)
    if (i != index)
      point
    else
      LinePoint(
        at != null && at.isFinite ? at : point.at,
        clip == null || clip.isEmpty ? point.plays : BlendClip(clip),
      ),
]);

/// [line] with a point after the last, one further along and playing what
/// the last does.
BlendLine linePointAdded(BlendLine line) => BlendLine(line.input, [
  ...line.points,
  LinePoint(line.points.last.at + 1, line.points.last.plays),
]);

/// [line] without point [index], or the same line when it has one point.
BlendLine linePointRemoved(BlendLine line, int index) {
  if (line.points.length < 2) return line;
  return BlendLine(line.input, [
    for (final (i, point) in line.points.indexed)
      if (i != index) point,
  ]);
}

/// The ways a blend is changed, each answering another blend.
///
/// Every one answers the same blend, by identity, when it would change
/// nothing or the result is not a blend: a name taken, the only state
/// removed. A caller tests for that rather than for an exception.
extension BlendEditing on BlendDocument {
  /// The name a new state would be given: [base], or [base] with a number
  /// when that is taken.
  String uniqueStateName(String base) {
    final taken = {
      for (final state in writtenStates) state.name,
      ...entries.keys,
      ...states.map((state) => state.name),
    };
    if (!taken.contains(base)) return base;
    for (var n = 2; ; n++) {
      if (!taken.contains('$base $n')) return '$base $n';
    }
  }

  /// This blend with a state playing [clip], named after it.
  BlendDocument withStateAdded(String clip) {
    // A clip with no name writes as a state that plays nothing, which a
    // file then reads back without.
    final named = clip.isEmpty ? 'clip' : clip;
    final name = uniqueStateName(named);
    return _tried(
      () => copyWith(
        states: [
          ...writtenStates,
          BlendState(name, plays: BlendClip(named)),
        ],
      ),
    );
  }

  /// This blend with state [name] called [to], and every change and the
  /// start that named it naming it by the new name.
  BlendDocument withStateRenamed(String name, String to) {
    if (to.isEmpty || to == name || _stateIndex(name) < 0) return this;
    String rename(String old) => old == name
        ? to
        : old.startsWith('$name/')
        ? '$to${old.substring(name.length)}'
        : old;
    return _tried(
      () => copyWith(
        states: [
          for (final state in writtenStates)
            state.name == name ? state.copyWith(name: to) : state,
        ],
        changes: [
          for (final change in writtenChanges)
            change.copyWith(
              from: change.from == null ? null : rename(change.from!),
              to: rename(change.to),
            ),
        ],
        start: rename(writtenStart),
      ),
    );
  }

  /// This blend without state [name], or the changes into or out of it. The
  /// start moves to the first state left when it was the one removed.
  BlendDocument withStateRemoved(String name) {
    if (writtenStates.length < 2 || _stateIndex(name) < 0) return this;
    bool names(String? state) =>
        state != null && (state == name || state.startsWith('$name/'));
    final left = [
      for (final state in writtenStates)
        if (state.name != name) state,
    ];
    return _tried(
      () => copyWith(
        states: left,
        changes: [
          for (final change in writtenChanges)
            if (!names(change.from) && !names(change.to)) change,
        ],
        start: names(writtenStart) ? left.first.name : writtenStart,
      ),
    );
  }

  /// This blend with state [name] made by [change] of what it was.
  BlendDocument withState(String name, BlendState Function(BlendState) change) {
    final index = _stateIndex(name);
    if (index < 0) return this;
    return _tried(
      () => copyWith(
        states: [
          for (final (i, state) in writtenStates.indexed)
            i == index ? change(state) : state,
        ],
      ),
    );
  }

  /// This blend starting in state [name].
  BlendDocument withStart(String name) =>
      _stateIndex(name) < 0 || name == writtenStart
      ? this
      : _tried(() => copyWith(start: name));

  /// This blend with a change from [from] (anywhere for null) to [to],
  /// last in precedence.
  BlendDocument withChangeAdded(String? from, String to) => _tried(
    () => copyWith(
      changes: [
        ...writtenChanges,
        BlendChange(from: from, to: to),
      ],
    ),
  );

  /// This blend with the change at [index] made by [change] of what it was.
  BlendDocument withChange(
    int index,
    BlendChange Function(BlendChange) change,
  ) {
    if (index < 0 || index >= writtenChanges.length) return this;
    return _tried(
      () => copyWith(
        changes: [
          for (final (i, one) in writtenChanges.indexed)
            i == index ? change(one) : one,
        ],
      ),
    );
  }

  /// This blend without the change at [index].
  BlendDocument withChangeRemoved(int index) {
    if (index < 0 || index >= writtenChanges.length) return this;
    return copyWith(
      changes: [
        for (final (i, one) in writtenChanges.indexed)
          if (i != index) one,
      ],
    );
  }

  /// This blend with the change at [index] moved [by] places in precedence,
  /// stopping at either end.
  BlendDocument withChangeMoved(int index, int by) {
    final to = (index + by).clamp(0, writtenChanges.length - 1);
    if (index < 0 || index >= writtenChanges.length || to == index) {
      return this;
    }
    final changes = [...writtenChanges];
    changes.insert(to, changes.removeAt(index));
    return copyWith(changes: changes);
  }

  /// This blend called [name].
  BlendDocument withName(String name) =>
      name.isEmpty || name == this.name ? this : copyWith(name: name);

  /// This blend with input [name] at nought, or the same blend when it
  /// already has an input by that name.
  BlendDocument withInputAdded(String name) =>
      name.isEmpty || inputs.containsKey(name) ? this : withInput(name, 0);

  /// This blend with input [name] at [value] until something sets it.
  BlendDocument withInput(String name, double value) {
    if (name.isEmpty || !value.isFinite || inputs[name] == value) return this;
    return copyWith(inputs: {...inputs, name: value});
  }

  /// This blend without input [name].
  BlendDocument withInputRemoved(String name) {
    if (!inputs.containsKey(name)) return this;
    return copyWith(inputs: {...inputs}..remove(name));
  }

  int _stateIndex(String name) =>
      writtenStates.indexWhere((state) => state.name == name);

  BlendDocument _tried(BlendDocument Function() make) {
    try {
      return make();
    } on ArgumentError {
      return this;
    }
  }
}
