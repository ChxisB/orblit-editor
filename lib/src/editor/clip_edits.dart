import 'dart:math' as math;

import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_scene/orblit_scene.dart' show TransformComponent;
import 'package:vector_math/vector_math_64.dart';

// Every change the timeline makes to a clip, each a function from one clip to
// the next. Nothing here knows about widgets or the undo stack: a command
// holds the clip from before and the clip from after, and these are how the
// second one is made.

/// What a channel moves, which is what a clip has one channel for.
typedef ChannelAddress = ({String target, String? bone, String property});

/// One key: the channel it is in, and where in that channel's keys.
///
/// By place rather than by time, because two keys at one moment are a cut
/// and a time cannot tell them apart. Every edit that moves keys about says
/// where they went, so a selection follows its keys.
typedef KeyRef = ({ChannelAddress channel, int index});

/// Keys after a move, and the clip they were moved in.
typedef KeysMoved = ({ClipDocument clip, Set<KeyRef> keys});

/// How a property looks from a clip, at the moment the playhead is on.
enum KeyMark {
  /// It cannot be keyed here: no clip is open, or it belongs to something
  /// the clip cannot name.
  none,

  /// It could be, and has no channel yet.
  unkeyed,

  /// The clip moves it, with no key on this frame.
  animated,

  /// There is a key on this frame.
  keyed,
}

/// Which side of a key a curve handle is on.
enum KeySide { arriving, leaving }

ChannelAddress addressOf(ClipChannel<Object> channel) =>
    (target: channel.target, bone: channel.bone, property: channel.property);

ClipChannel<Object>? channelAt(ClipDocument clip, ChannelAddress address) =>
    clip.channelFor(address.target, address.property, bone: address.bone);

/// The key [ref] names, or null when the clip no longer has it.
Key<Object>? keyOf(ClipDocument clip, KeyRef ref) {
  final keys = channelAt(clip, ref.channel)?.keys;
  if (keys == null || ref.index < 0 || ref.index >= keys.length) return null;
  return keys[ref.index];
}

/// How close two moments have to be to be one frame.
///
/// Far finer than any rate anybody animates at, and far coarser than what a
/// double loses on its way through a file and back.
const double sameMoment = 1e-5;

/// [at] on the nearest of [rate]'s frames.
double snapped(double at, double rate) {
  if (rate <= 0 || !at.isFinite) return at;
  return (at * rate).round() / rate;
}

/// The key of [channel] on the frame at [at], or -1.
///
/// The later of a cut, since that is the one the channel says from then on.
int keyAt(ClipChannel<Object> channel, double at) {
  for (var i = channel.keys.length - 1; i >= 0; i--) {
    if ((channel.keys[i].at - at).abs() < sameMoment) return i;
  }
  return -1;
}

KeyMark markOf(ClipDocument clip, ChannelAddress address, double at) {
  final channel = channelAt(clip, address);
  if (channel == null) return KeyMark.unkeyed;
  return keyAt(channel, at) >= 0 ? KeyMark.keyed : KeyMark.animated;
}

/// The kind a field is keyed as, from how the scene writes it, or null when
/// it is not something a key can hold.
///
/// Four numbers are not keyed as a turn: in a component they are as likely
/// to be a colour with its alpha, and a colour mixed as a rotation is a
/// colour nobody chose. A turn is keyed where a channel already says it
/// carries one.
ChannelKind<Object>? kindOfField(Object? raw) => switch (raw) {
  num() => ChannelKind.number,
  bool() => ChannelKind.flag,
  List(length: 3) when ChannelKind.vector.read(raw) != null =>
    ChannelKind.vector,
  _ => null,
};

/// [raw], a field as the scene writes it, as a key of [kind], or null when
/// it cannot be one.
///
/// A transform's rotation is written in degrees, which is what a vector
/// channel on it carries; a channel carrying a turn, as an imported one
/// does, is keyed with the turn those degrees make.
Object? keyValueOf(ChannelKind<Object> kind, String property, Object? raw) {
  if (kind == ChannelKind.rotation && property == 'transform.rotation') {
    final degrees = ChannelKind.vector.read(raw);
    if (degrees == null) return null;
    return Quaternion.fromRotation(TransformComponent.rotationOf(degrees));
  }
  return kind.read(raw);
}

/// [clip] with [value] keyed on [address] at [at].
///
/// A key already on that frame takes the value and keeps how it travels. A
/// new key travels the way the key before it does, so keying the middle of
/// an eased span leaves both halves eased. A channel that does not exist yet
/// is made, of [kind]; one that does keeps its own, and [value] has to be
/// one of those.
ClipDocument keyed(
  ClipDocument clip,
  ChannelAddress address,
  ChannelKind<Object> kind,
  double at,
  Object value,
) {
  final channel = channelAt(clip, address);
  if (channel == null) {
    final made = ClipChannel.ofKind(
      target: address.target,
      bone: address.bone,
      property: address.property,
      kind: kind,
      keys: [Key<Object>(at, value)],
    );
    return clip.copyWith(channels: [...clip.channels, made]);
  }

  final keys = <Key<Object>>[...channel.keys];
  final index = keyAt(channel, at);
  if (index >= 0) {
    keys[index] = _changed(keys[index], value: value);
  } else {
    final before = keys.lastWhere(
      (key) => key.at < at,
      orElse: () => keys.first,
    );
    keys.add(Key<Object>(at, value, hold: before.hold, shape: before.shape));
  }
  return _swapped(clip, channel, channel.rekeyed(keys));
}

/// [clip] without the keys [refs] name. A channel left with none goes too:
/// a channel has to have a key, and one with nothing to say should not be
/// in the file.
ClipDocument withoutKeys(ClipDocument clip, Set<KeyRef> refs) {
  final channels = <ClipChannel<Object>>[];
  for (final channel in clip.channels) {
    final going = _indicesIn(channel, refs);
    if (going.isEmpty) {
      channels.add(channel);
      continue;
    }
    final keys = [
      for (final (index, key) in channel.keys.indexed)
        if (!going.contains(index)) key,
    ];
    if (keys.isNotEmpty) channels.add(channel.rekeyed(keys));
  }
  return clip.copyWith(channels: channels);
}

/// [clip] with the keys [refs] name moved [by] seconds, and where they went.
///
/// Worked out from the clip as the drag began, every time the pointer moves,
/// so a key dragged over another and back again leaves the other where it
/// was. The move is in whole frames and stops at either end of the clip. A
/// key landing on a frame another key of its channel is on replaces it,
/// which is what dropping one key on another means.
KeysMoved movedKeys(ClipDocument clip, Set<KeyRef> refs, double by) {
  var first = double.infinity;
  var last = double.negativeInfinity;
  final present = <KeyRef>{};
  for (final ref in refs) {
    final key = keyOf(clip, ref);
    if (key == null) continue;
    present.add(ref);
    first = math.min(first, key.at);
    last = math.max(last, key.at);
  }
  if (present.isEmpty) return (clip: clip, keys: const {});

  var delta = snapped(by, clip.rate);
  delta = math.min(delta, math.max(clip.duration - last, 0));
  delta = math.max(delta, -first);
  if (delta.abs() < sameMoment) return (clip: clip, keys: present);

  final moved = <KeyRef>{};
  final channels = <ClipChannel<Object>>[];
  for (final channel in clip.channels) {
    final going = _indicesIn(channel, present).toList()..sort();
    if (going.isEmpty) {
      channels.add(channel);
      continue;
    }
    final arriving = [
      for (final index in going)
        _changed(channel.keys[index], at: channel.keys[index].at + delta),
    ];
    bool landedOn(Key<Object> key) =>
        arriving.any((one) => (one.at - key.at).abs() < sameMoment);
    final entries = [
      for (final (index, key) in channel.keys.indexed)
        if (!going.contains(index) && !landedOn(key)) (key: key, moved: false),
      for (final key in arriving) (key: key, moved: true),
    ];
    // By time, and by place among equal times, so a cut stays the way
    // round it was. The channel sorts the same way, which is what makes the
    // places worked out here the places it ends up with.
    final order = [for (var i = 0; i < entries.length; i++) i]
      ..sort((a, b) {
        final byTime = entries[a].key.at.compareTo(entries[b].key.at);
        return byTime != 0 ? byTime : a.compareTo(b);
      });
    final address = addressOf(channel);
    for (final (place, entry) in order.indexed) {
      if (entries[entry].moved) moved.add((channel: address, index: place));
    }
    channels.add(channel.rekeyed([for (final i in order) entries[i].key]));
  }
  return (clip: clip.copyWith(channels: channels), keys: moved);
}

/// [clip] with the key [ref] names holding [value] instead.
ClipDocument withKeyValue(ClipDocument clip, KeyRef ref, Object value) {
  final channel = channelAt(clip, ref.channel);
  if (channel == null || keyOf(clip, ref) == null) return clip;
  final keys = <Key<Object>>[...channel.keys];
  keys[ref.index] = _changed(keys[ref.index], value: value);
  return _swapped(clip, channel, channel.rekeyed(keys));
}

/// [clip] with every key [refs] names travelling to the next by [hold].
///
/// A shaped hold with no [shape] eases in and out, which is what smooth
/// does, so choosing it changes nothing until a shape is chosen too.
ClipDocument withHold(
  ClipDocument clip,
  Set<KeyRef> refs,
  Hold hold, {
  Easing? shape,
}) {
  final channels = <ClipChannel<Object>>[];
  for (final channel in clip.channels) {
    final changing = _indicesIn(channel, refs);
    if (changing.isEmpty) {
      channels.add(channel);
      continue;
    }
    channels.add(
      channel.rekeyed([
        for (final (index, key) in channel.keys.indexed)
          if (!changing.contains(index))
            key
          else
            _changed(
              key,
              hold: hold,
              shape: hold == Hold.shaped ? shape ?? Easing.inOut : key.shape,
            ),
      ]),
    );
  }
  return clip.copyWith(channels: channels);
}

/// How many numbers a key of [kind] is drawn as on a curve: one for a
/// number, three for a vector, and none for a turn or a flag.
///
/// A turn's four numbers are not four things anybody can reason about one
/// at a time, and a flag has no slope; both are edited as keys, not curves.
int curvesOf(ChannelKind<Object> kind) => switch (kind) {
  ChannelKind.number => 1,
  ChannelKind.vector => 3,
  _ => 0,
};

/// One of the numbers [value] is drawn as.
double partOf(Object value, int part) => switch (value) {
  final double number => number,
  final Vector3 vector => vector[part],
  _ => 0,
};

/// [value] with one of its numbers changed.
Object withPart(Object value, int part, double to) => switch (value) {
  double() => to,
  final Vector3 vector => vector.clone()..[part] = to,
  _ => value,
};

/// [clip] with a slope of the key [ref] names set, from dragging a handle.
///
/// Aligned unless [broken]: both handles turn together, so the curve passes
/// through the key smoothly, and breaking them is what makes a corner. The
/// span the handle belongs to becomes a curve, since a handle on a span that
/// is not one would move nothing anybody could see.
ClipDocument withSlope(
  ClipDocument clip,
  KeyRef ref, {
  required KeySide side,
  required int part,
  required double slope,
  bool broken = false,
}) {
  final channel = channelAt(clip, ref.channel);
  final key = keyOf(clip, ref);
  if (channel == null || key == null || curvesOf(channel.kind) == 0) {
    return clip;
  }
  final index = ref.index;
  final slopeIn = key.slopeIn ?? channel.curve.slopeAt(index);
  final slopeOut = key.slopeOut ?? channel.curve.slopeAt(index);
  final dragged = withPart(
    side == KeySide.arriving ? slopeIn : slopeOut,
    part,
    slope,
  );
  final arriving = side == KeySide.arriving || !broken;
  final leaving = side == KeySide.leaving || !broken;

  final keys = <Key<Object>>[...channel.keys];
  keys[index] = Key<Object>(
    key.at,
    key.value,
    hold: leaving ? Hold.curve : key.hold,
    shape: key.shape,
    slopeIn: arriving ? dragged : key.slopeIn,
    slopeOut: leaving ? dragged : key.slopeOut,
  );
  if (arriving && index > 0) {
    keys[index - 1] = _changed(keys[index - 1], hold: Hold.curve);
  }
  return _swapped(clip, channel, channel.rekeyed(keys));
}

/// [clip] with the slopes of every key [refs] names worked out again from
/// their neighbours, the way a key nobody has dragged a handle of is.
ClipDocument withAutoSlopes(ClipDocument clip, Set<KeyRef> refs) {
  final channels = <ClipChannel<Object>>[];
  for (final channel in clip.channels) {
    final resetting = _indicesIn(channel, refs);
    if (resetting.isEmpty) {
      channels.add(channel);
      continue;
    }
    channels.add(
      channel.rekeyed([
        for (final (index, key) in channel.keys.indexed)
          if (!resetting.contains(index))
            key
          else
            Key<Object>(key.at, key.value, hold: key.hold, shape: key.shape),
      ]),
    );
  }
  return clip.copyWith(channels: channels);
}

/// The places in [channel] of the keys [refs] names there.
Set<int> _indicesIn(ClipChannel<Object> channel, Set<KeyRef> refs) {
  final address = addressOf(channel);
  return {
    for (final ref in refs)
      if (ref.channel == address &&
          ref.index >= 0 &&
          ref.index < channel.keys.length)
        ref.index,
  };
}

Key<Object> _changed(
  Key<Object> key, {
  double? at,
  Object? value,
  Hold? hold,
  Easing? shape,
}) => Key<Object>(
  at ?? key.at,
  value ?? key.value,
  hold: hold ?? key.hold,
  shape: shape ?? key.shape,
  slopeIn: key.slopeIn,
  slopeOut: key.slopeOut,
);

/// [clip] with [now] where [was] is.
ClipDocument _swapped(
  ClipDocument clip,
  ClipChannel<Object> was,
  ClipChannel<Object> now,
) => clip.copyWith(
  channels: [
    for (final channel in clip.channels)
      identical(channel, was) ? now : channel,
  ],
);
