import 'package:flutter/widgets.dart';

import 'gizmo.dart';

/// Which part of a pointer's movement a [ViewportGesture] is.
enum ViewportPhase {
  /// Moving over the view with nothing held down.
  hover,

  /// The pointer is no longer over whatever a stage last showed as under it:
  /// it left the view, or a stage ahead of this one took the hover.
  leave,

  /// A click.
  tap,

  /// A drag beginning. Whichever stage takes it is handed the rest of the
  /// drag, and no other stage sees any of it.
  dragStart,

  dragUpdate,

  dragEnd,

  /// A drag that stopped without finishing.
  dragCancel,
}

/// One thing the pointer did in a scene view.
@immutable
class ViewportGesture {
  const ViewportGesture(
    this.phase,
    this.at, {
    this.add = false,
    this.projection,
  });

  final ViewportPhase phase;

  /// Where, in the view's own pixels.
  final Offset at;

  /// Whether a modifier that adds to a selection, rather than replacing it,
  /// was held.
  final bool add;

  /// How the view's pixels map into the world, or null before the view has
  /// been laid out. A brush turns [at] into a ray through this.
  final ViewportProjection? projection;

  ViewportGesture _as(ViewportPhase phase) =>
      ViewportGesture(phase, at, add: add, projection: projection);
}

/// Something that can take a gesture in a scene view, and says whether it
/// did.
typedef ViewportInput = bool Function(ViewportGesture gesture);

/// One place in a [ViewportInputOrder]: who it is, and what it does.
typedef ViewportStage = ({String name, ViewportInput input});

/// Who gets a gesture in a scene view, and in what order.
///
/// The stages are asked in turn and the first that takes a gesture is the
/// only one that sees it. The order is the whole point: a brush that cannot
/// take a drag before the handles do is a brush that moves objects when
/// somebody meant to sculpt, so the order is written down here as a list and
/// tested, rather than being whatever order a run of `if`s happened to be in.
///
/// The stages are read again for each gesture, because what is in the order
/// depends on what is selected. A drag is the exception: the stage that took
/// its start keeps it to the end, even if the list has changed under it.
class ViewportInputOrder {
  ViewportInputOrder(this.stages);

  /// The stages, first to last.
  final Iterable<ViewportStage> Function() stages;

  ViewportStage? _holder;

  Offset _last = Offset.zero;

  /// The stage that has the drag in progress, if one does.
  String? get holding => _holder?.name;

  /// Hands [gesture] down the order, and says which stage took it.
  ///
  /// Null when nothing did. A hover taken by one stage is a
  /// [ViewportPhase.leave] for every stage behind it, so a highlight further
  /// down the order goes out rather than staying lit under the one that won.
  String? handle(ViewportGesture gesture) {
    switch (gesture.phase) {
      case ViewportPhase.leave:
        leave(gesture.at);
        return null;
      case ViewportPhase.dragUpdate:
        _last = gesture.at;
        _holder?.input(gesture);
        return _holder?.name;
      case ViewportPhase.dragEnd || ViewportPhase.dragCancel:
        final holder = _holder;
        _holder = null;
        holder?.input(gesture);
        return holder?.name;
      case ViewportPhase.dragStart:
        // A start with a drag still held means the end went missing. The
        // stage holding it is told it is over, rather than being left
        // mid-drag for ever.
        if (_holder != null) cancel();
        _last = gesture.at;
        final taker = _first(gesture, stages());
        _holder = taker;
        return taker?.name;
      case ViewportPhase.hover:
        final all = stages().toList();
        final taker = _first(gesture, all);
        if (taker != null) {
          for (final stage in all.skip(all.indexOf(taker) + 1)) {
            stage.input(gesture._as(ViewportPhase.leave));
          }
        }
        return taker?.name;
      case ViewportPhase.tap:
        return _first(gesture, stages())?.name;
    }
  }

  /// Tells every stage the pointer has gone.
  void leave([Offset at = Offset.zero]) {
    for (final stage in stages()) {
      stage.input(ViewportGesture(ViewportPhase.leave, at));
    }
  }

  /// Ends the drag in progress without finishing it, where it last was.
  void cancel() => handle(ViewportGesture(ViewportPhase.dragCancel, _last));

  static ViewportStage? _first(
    ViewportGesture gesture,
    Iterable<ViewportStage> stages,
  ) {
    for (final stage in stages) {
      if (stage.input(gesture)) return stage;
    }
    return null;
  }
}
