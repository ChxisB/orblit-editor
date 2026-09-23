import 'dart:math' as math;

import 'package:flutter/material.dart' hide Easing;
import 'package:flutter/scheduler.dart';
import 'package:flutter/services.dart';
import 'package:orblit_motion/orblit_motion.dart'
    show ClipDocument, Easing, Hold, WhenDone;

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'clip_bench.dart';
import 'clip_edits.dart';
import 'scene.dart';
import 'timeline_curves.dart';
import 'timeline_sheet.dart';

/// Which of the two ways of looking at a clip the panel shows.
enum TimelineView { keys, curves }

/// How wide the list of what a clip moves is, beside the sheet.
const double _trackWidth = 200;

/// The clip being edited: what it moves on the left, its keys or one
/// channel's curve on the right, and the ruler the playhead is dragged along
/// over both.
///
/// Everything here reads and changes the bench, so closing the panel and
/// opening it again finds the playhead where it was left. Playing is
/// sampling: each frame asks the clip what it says at the new time, so
/// scrubbing backwards shows exactly what playing forwards did.
///
/// With the panel focused, Space plays and pauses, the arrows step a frame,
/// Home and End go to either end, Delete deletes the picked keys and Escape
/// picks none.
class TimelinePanel extends StatefulWidget {
  const TimelinePanel({
    super.key,
    required this.bench,
    this.selected,
    this.onProblem,
  });

  final ClipBench bench;

  /// What is selected in the scene, which is what the clip can be played on.
  final SceneObject? selected;

  /// Told what went wrong saving a clip.
  final ValueChanged<String>? onProblem;

  @override
  State<TimelinePanel> createState() => _TimelinePanelState();
}

class _TimelinePanelState extends State<TimelinePanel>
    with SingleTickerProviderStateMixin {
  late final Ticker _ticker = createTicker(_tick);
  final FocusNode _focus = FocusNode(debugLabel: 'timeline');
  final ScrollController _scroll = ScrollController();

  TimelineView _view = TimelineView.keys;

  /// Which way a bouncing clip is going.
  double _direction = 1;
  Duration? _lastTick;

  /// What ties typing a length into one step.
  Object? _typing;

  ClipDocument? _rowsClip;
  List<TimelineRow> _rows = const [];

  ClipBench get _bench => widget.bench;

  @override
  void initState() {
    super.initState();
    _bench.addListener(_changed);
    _follow();
  }

  @override
  void didUpdateWidget(TimelinePanel old) {
    super.didUpdateWidget(old);
    if (identical(old.bench, widget.bench)) return;
    old.bench.removeListener(_changed);
    widget.bench.addListener(_changed);
    _follow();
  }

  @override
  void dispose() {
    _bench.removeListener(_changed);
    _ticker.dispose();
    _focus.dispose();
    _scroll.dispose();
    super.dispose();
  }

  void _changed() {
    _follow();
    if (mounted) setState(() {});
  }

  /// Starts or stops the ticker to match the bench.
  void _follow() {
    if (_bench.playing && !_ticker.isActive) {
      _lastTick = null;
      _ticker.start();
    } else if (!_bench.playing && _ticker.isActive) {
      _ticker.stop();
    }
  }

  void _tick(Duration elapsed) {
    final clip = _bench.clip;
    final last = _lastTick;
    _lastTick = elapsed;
    if (clip == null || last == null || !_bench.playing) return;
    final length = clip.duration;
    var at = _bench.at + (elapsed - last).inMicroseconds / 1e6 * _direction;
    switch (clip.whenDone) {
      case WhenDone.loop:
        if (at >= length) at = length <= 0 ? 0 : at % length;
      case WhenDone.bounce:
        if (at >= length) {
          at = math.max(2 * length - at, 0);
          _direction = -1;
        } else if (at <= 0) {
          at = math.min(-at, length);
          _direction = 1;
        }
      case WhenDone.hold || WhenDone.release:
        if (at >= length) {
          _bench
            ..at = length
            ..playing = false;
          return;
        }
    }
    _bench.at = at;
  }

  void _play() {
    final clip = _bench.clip;
    if (clip == null) return;
    if (_bench.playing) {
      _bench.playing = false;
      return;
    }
    final once =
        clip.whenDone == WhenDone.hold || clip.whenDone == WhenDone.release;
    if (once && _bench.at >= clip.duration - sameMoment) _bench.at = 0;
    _direction = 1;
    _bench.playing = true;
  }

  void _step(int frames) {
    final clip = _bench.clip;
    if (clip == null || clip.rate <= 0) return;
    _bench
      ..playing = false
      ..at = snapped(_bench.frame + frames / clip.rate, clip.rate);
  }

  void _deleteKeys() {
    final picked = _bench.selection;
    if (picked.isEmpty) return;
    _bench.edit(
      picked.length == 1 ? 'Delete key' : 'Delete keys',
      (clip) => withoutKeys(clip, picked),
      keys: const {},
    );
  }

  KeyEventResult _key(FocusNode node, KeyEvent event) {
    // Only when the panel itself has focus: typing a length is typing.
    if (!_focus.hasPrimaryFocus || event is KeyUpEvent) {
      return KeyEventResult.ignored;
    }
    final repeat = event is KeyRepeatEvent;
    switch (event.logicalKey) {
      case LogicalKeyboardKey.space when !repeat:
        _play();
      case LogicalKeyboardKey.arrowLeft:
        _step(-1);
      case LogicalKeyboardKey.arrowRight:
        _step(1);
      case LogicalKeyboardKey.home when !repeat:
        _bench
          ..playing = false
          ..at = 0;
      case LogicalKeyboardKey.end when !repeat:
        _bench
          ..playing = false
          ..at = _bench.clip?.duration ?? 0;
      case LogicalKeyboardKey.delete || LogicalKeyboardKey.backspace
          when !repeat:
        _deleteKeys();
      case LogicalKeyboardKey.escape when !repeat:
        _bench.selection = const {};
      default:
        return KeyEventResult.ignored;
    }
    return KeyEventResult.handled;
  }

  void _focusPanel() {
    if (!_focus.hasPrimaryFocus) _focus.requestFocus();
  }

  List<TimelineRow> _rowsOf(ClipDocument clip) {
    if (!identical(clip, _rowsClip)) {
      _rowsClip = clip;
      _rows = rowsOf(clip);
    }
    return _rows;
  }

  @override
  Widget build(BuildContext context) {
    final clip = _bench.clip;
    return Focus(
      focusNode: _focus,
      onKeyEvent: _key,
      child: GestureDetector(
        behavior: HitTestBehavior.translucent,
        onTapDown: (_) => _focusPanel(),
        child: Container(
          color: OrblitColors.surface,
          child: clip == null
              ? const _Message(
                  'No clip open. Make one in the project with New › Clip, '
                  'or open a .oclip.',
                )
              : Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    _toolbar(clip),
                    _rulerRow(clip),
                    Expanded(
                      child: clip.channels.isEmpty
                          ? const _Message(
                              'Nothing keyed yet. Select something, then '
                              'press the diamond beside a value in the '
                              'inspector to key it here.',
                            )
                          : _body(clip),
                    ),
                  ],
                ),
        ),
      ),
    );
  }

  Widget _toolbar(ClipDocument clip) {
    final picked = _bench.selection.isNotEmpty;
    return Container(
      height: 38,
      padding: const EdgeInsets.symmetric(horizontal: Space.sm),
      decoration: const BoxDecoration(
        border: Border(bottom: BorderSide(color: OrblitColors.lineSoft)),
      ),
      child: Row(
        children: [
          Expanded(
            child: SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: [
                  _clipMenu(clip),
                  const SizedBox(width: Space.sm),
                  _Action(
                    icon: Icons.skip_previous,
                    tooltip: 'To the start (Home)',
                    onTap: () => _bench
                      ..playing = false
                      ..at = 0,
                  ),
                  _Action(
                    icon: _bench.playing ? Icons.pause : Icons.play_arrow,
                    tooltip: _bench.playing ? 'Pause (Space)' : 'Play (Space)',
                    onTap: _play,
                    on: _bench.playing,
                  ),
                  _Action(
                    icon: Icons.skip_next,
                    tooltip: 'To the end (End)',
                    onTap: () => _bench
                      ..playing = false
                      ..at = clip.duration,
                  ),
                  const SizedBox(width: Space.md),
                  Text('Length', style: OrblitText.caption),
                  const SizedBox(width: Space.xs),
                  SizedBox(
                    width: 64,
                    child: ValueField(
                      value: secondsLabel(clip.duration),
                      mono: true,
                      onChanged: _setLength,
                      onDone: () {
                        _typing = null;
                        _bench.history.seal();
                      },
                    ),
                  ),
                  const SizedBox(width: Space.xs),
                  Text('s', style: OrblitText.caption),
                  const SizedBox(width: Space.sm),
                  _rateMenu(clip),
                  const SizedBox(width: Space.xs),
                  _whenDoneMenu(clip),
                  const SizedBox(width: Space.sm),
                  _ownerMenu(),
                ],
              ),
            ),
          ),
          const SizedBox(width: Space.sm),
          _Chip(
            label: 'Keys',
            on: _view == TimelineView.keys,
            onTap: () => setState(() => _view = TimelineView.keys),
          ),
          _Chip(
            label: 'Curves',
            on: _view == TimelineView.curves,
            onTap: () => setState(() => _view = TimelineView.curves),
          ),
          const SizedBox(width: Space.sm),
          _holdMenu(enabled: picked),
          _Action(
            icon: Icons.auto_graph,
            tooltip: 'Work the picked keys\' slopes out again',
            onTap: picked
                ? () => _bench.edit(
                    'Auto slopes',
                    (clip) => withAutoSlopes(clip, _bench.selection),
                  )
                : null,
          ),
          _Action(
            icon: Icons.delete_outline,
            tooltip: 'Delete the picked keys (Delete)',
            onTap: picked ? _deleteKeys : null,
          ),
        ],
      ),
    );
  }

  void _setLength(String text) {
    final seconds = double.tryParse(text.trim());
    if (seconds == null || seconds <= 0 || seconds > 600) return;
    final gesture = _typing ??= Object();
    _bench.edit(
      'Clip length',
      (clip) => clip.copyWith(duration: seconds),
      gesture: gesture,
    );
  }

  Widget _clipMenu(ClipDocument clip) {
    final shown = _bench.shown!;
    final unsaved = _bench.isUnsaved(shown);
    return _Menu(
      label: '${clip.name}${unsaved ? ' •' : ''}',
      icon: Icons.animation,
      items: [
        for (final open in _bench.open)
          MenuItemButton(
            onPressed: () => _bench.show(open.path),
            leadingIcon: Icon(
              identical(open, shown) ? Icons.check : null,
              size: 14,
              color: OrblitColors.inkMid,
            ),
            child: Text(
              '${open.clip.name}${_bench.isUnsaved(open) ? ' •' : ''}',
              style: OrblitText.label,
            ),
          ),
        const Divider(height: 1),
        MenuItemButton(
          onPressed: unsaved
              ? () {
                  final problem = _bench.save(shown);
                  if (problem != null) widget.onProblem?.call(problem);
                }
              : null,
          leadingIcon: const Icon(
            Icons.save_outlined,
            size: 14,
            color: OrblitColors.inkMid,
          ),
          child: Text('Save ${clip.name}', style: OrblitText.label),
        ),
        MenuItemButton(
          onPressed: () => _bench.close(shown.path),
          leadingIcon: const Icon(
            Icons.close,
            size: 14,
            color: OrblitColors.inkMid,
          ),
          child: Text(
            unsaved ? 'Close without saving' : 'Close',
            style: OrblitText.label,
          ),
        ),
      ],
    );
  }

  Widget _rateMenu(ClipDocument clip) {
    final rate = clip.rate;
    return _Menu(
      label: '${secondsLabel(rate)} fps',
      tooltip: 'Frames a second, which is what keys and the playhead snap to',
      items: [
        for (final choice in const [24.0, 25.0, 30.0, 60.0])
          MenuItemButton(
            onPressed: choice == rate
                ? null
                : () => _bench.edit(
                    'Frame rate',
                    (clip) => clip.copyWith(rate: choice),
                  ),
            child: Text('${secondsLabel(choice)} fps', style: OrblitText.label),
          ),
      ],
    );
  }

  static String _whenDoneLabel(WhenDone done) => switch (done) {
    WhenDone.hold => 'Holds at the end',
    WhenDone.loop => 'Loops',
    WhenDone.bounce => 'Bounces',
    WhenDone.release => 'Lets go at the end',
  };

  Widget _whenDoneMenu(ClipDocument clip) {
    return _Menu(
      label: _whenDoneLabel(clip.whenDone),
      tooltip: 'What the clip does when it gets to the end',
      items: [
        for (final done in WhenDone.values)
          MenuItemButton(
            onPressed: done == clip.whenDone
                ? null
                : () => _bench.edit(
                    'When done',
                    (clip) => clip.copyWith(whenDone: done),
                  ),
            child: Text(_whenDoneLabel(done), style: OrblitText.label),
          ),
      ],
    );
  }

  Widget _ownerMenu() {
    final owner = _bench.objectOf('');
    final selected = widget.selected;
    return _Menu(
      label: owner == null ? 'Played on nothing' : 'On ${owner.name}',
      icon: Icons.person_pin_circle_outlined,
      tooltip: 'What the clip is played on while it is edited',
      warn: owner == null,
      items: [
        MenuItemButton(
          onPressed: selected == null || selected.id == owner?.id
              ? null
              : () => _bench.owner = selected.id,
          child: Text(
            selected == null
                ? 'Select something to play it on'
                : 'Play on ${selected.name}',
            style: OrblitText.label,
          ),
        ),
        MenuItemButton(
          onPressed: owner == null ? null : () => _bench.owner = null,
          child: Text('Play on nothing', style: OrblitText.label),
        ),
      ],
    );
  }

  static String _holdLabel(Hold hold) => switch (hold) {
    Hold.step => 'Step',
    Hold.linear => 'Linear',
    Hold.smooth => 'Smooth',
    Hold.shaped => 'Shaped',
    Hold.curve => 'Curve',
  };

  static String _easingLabel(Easing shape) => switch (shape) {
    Easing.linear => 'Even',
    Easing.in_ => 'Ease in',
    Easing.out => 'Ease out',
    Easing.inOut => 'Ease in and out',
    Easing.smooth => 'Smooth',
    Easing.back => 'Overshoot',
    Easing.bounce => 'Bounce',
  };

  Widget _holdMenu({required bool enabled}) {
    void hold(Hold hold, [Easing? shape]) => _bench.edit(
      'Hold ${_holdLabel(hold).toLowerCase()}',
      (clip) => withHold(clip, _bench.selection, hold, shape: shape),
    );
    return _Menu(
      label: 'Hold',
      tooltip: 'How the picked keys travel to the next',
      enabled: enabled,
      items: [
        for (final choice in const [
          Hold.step,
          Hold.linear,
          Hold.smooth,
          Hold.curve,
        ])
          MenuItemButton(
            onPressed: () => hold(choice),
            child: Text(_holdLabel(choice), style: OrblitText.label),
          ),
        SubmenuButton(
          menuStyle: _menuStyle,
          menuChildren: [
            for (final shape in Easing.values)
              MenuItemButton(
                onPressed: () => hold(Hold.shaped, shape),
                child: Text(_easingLabel(shape), style: OrblitText.label),
              ),
          ],
          child: Text(_holdLabel(Hold.shaped), style: OrblitText.label),
        ),
      ],
    );
  }

  Widget _rulerRow(ClipDocument clip) {
    final frame = clip.rate <= 0 ? 0 : (_bench.at * clip.rate).round();
    return Container(
      height: 24,
      decoration: const BoxDecoration(
        border: Border(bottom: BorderSide(color: OrblitColors.lineSoft)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            width: _trackWidth,
            padding: const EdgeInsets.symmetric(horizontal: Space.sm),
            alignment: Alignment.centerLeft,
            decoration: const BoxDecoration(
              border: Border(right: BorderSide(color: OrblitColors.lineSoft)),
            ),
            child: Text(
              '${_bench.at.toStringAsFixed(2)} s   frame $frame',
              style: OrblitText.mono.copyWith(fontSize: 11),
            ),
          ),
          Expanded(
            child: TimelineRuler(bench: _bench, onFocus: _focusPanel),
          ),
        ],
      ),
    );
  }

  Widget _body(ClipDocument clip) {
    final rows = _rowsOf(clip);
    final tracks = _TrackList(bench: _bench, rows: rows);
    if (_view == TimelineView.curves) {
      return Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _divided(SingleChildScrollView(controller: _scroll, child: tracks)),
          Expanded(
            child: CurveView(bench: _bench, onFocus: _focusPanel),
          ),
        ],
      );
    }
    return LayoutBuilder(
      builder: (context, constraints) {
        final height = math.max(
          rows.length * rowHeight,
          constraints.maxHeight,
        );
        return SingleChildScrollView(
          controller: _scroll,
          child: SizedBox(
            height: height,
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                _divided(tracks),
                Expanded(
                  child: DopeSheet(
                    bench: _bench,
                    rows: rows,
                    height: height,
                    onFocus: _focusPanel,
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  static Widget _divided(Widget child) => Container(
    width: _trackWidth,
    decoration: const BoxDecoration(
      border: Border(right: BorderSide(color: OrblitColors.lineSoft)),
    ),
    child: child,
  );
}

/// What the clip moves, one row for each thing and one for each channel of
/// it, in step with the rows of the sheet.
class _TrackList extends StatelessWidget {
  const _TrackList({required this.bench, required this.rows});

  final ClipBench bench;
  final List<TimelineRow> rows;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        for (final row in rows)
          if (row.channel case final channel?)
            _ChannelRow(
              bench: bench,
              address: addressOf(channel),
              keys: channel.keys.length,
            )
          else
            _heading(row.target),
      ],
    );
  }

  Widget _heading(String target) {
    final object = bench.objectOf(target);
    final String name;
    var missing = false;
    if (object != null) {
      name = object.name;
    } else if (target.isEmpty) {
      name = 'Whatever plays it';
    } else {
      name = target;
      missing = true;
    }
    final text = Container(
      height: rowHeight,
      color: OrblitColors.raised,
      padding: const EdgeInsets.symmetric(horizontal: Space.sm),
      alignment: Alignment.centerLeft,
      child: Row(
        children: [
          Icon(
            missing ? Icons.link_off : Icons.view_in_ar_outlined,
            size: 12,
            color: missing ? OrblitColors.warn : OrblitColors.inkDim,
          ),
          const SizedBox(width: Space.xs),
          Expanded(
            child: Text(
              name,
              overflow: TextOverflow.ellipsis,
              style: OrblitText.label.copyWith(
                color: missing ? OrblitColors.warn : OrblitColors.ink,
              ),
            ),
          ),
        ],
      ),
    );
    if (!missing) return text;
    return Tooltip(
      message: 'Nothing is called this where the clip is played.',
      child: text,
    );
  }
}

class _ChannelRow extends StatefulWidget {
  const _ChannelRow({
    required this.bench,
    required this.address,
    required this.keys,
  });

  final ClipBench bench;
  final ChannelAddress address;
  final int keys;

  @override
  State<_ChannelRow> createState() => _ChannelRowState();
}

class _ChannelRowState extends State<_ChannelRow> {
  bool _hovered = false;

  String get _label {
    final property = widget.address.property;
    final dot = property.indexOf('.');
    final field = dot < 0 ? property : property.substring(dot + 1);
    final bone = widget.address.bone;
    return bone == null ? field : '$bone · $field';
  }

  @override
  Widget build(BuildContext context) {
    final bench = widget.bench;
    final chosen = bench.curve == widget.address;
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hovered = true),
      onExit: (_) => setState(() => _hovered = false),
      child: GestureDetector(
        onTap: () => bench.curve = widget.address,
        child: Container(
          height: rowHeight,
          padding: const EdgeInsets.only(left: Space.xl, right: Space.xs),
          decoration: BoxDecoration(
            color: chosen
                ? OrblitColors.emberWash
                : _hovered
                ? OrblitColors.hover
                : null,
            border: const Border(
              bottom: BorderSide(color: OrblitColors.lineSoft),
            ),
          ),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  _label,
                  overflow: TextOverflow.ellipsis,
                  style: OrblitText.label.copyWith(
                    color: chosen ? OrblitColors.ink : OrblitColors.inkMid,
                  ),
                ),
              ),
              if (_hovered)
                _Action(
                  icon: Icons.close,
                  tooltip: 'Remove this channel and its keys',
                  onTap: () => bench.edit(
                    'Remove $_label',
                    (clip) => withoutKeys(clip, {
                      for (var index = 0; index < widget.keys; index++)
                        (channel: widget.address, index: index),
                    }),
                    keys: const {},
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Message extends StatelessWidget {
  const _Message(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(Space.lg),
        child: Text(
          text,
          style: OrblitText.caption,
          textAlign: TextAlign.center,
        ),
      ),
    );
  }
}

class _Action extends StatelessWidget {
  const _Action({
    required this.icon,
    required this.tooltip,
    required this.onTap,
    this.on = false,
  });

  final IconData icon;
  final String tooltip;

  /// Null greys it out.
  final VoidCallback? onTap;

  final bool on;

  @override
  Widget build(BuildContext context) {
    final colour = onTap == null
        ? OrblitColors.line
        : on
        ? OrblitColors.ember
        : OrblitColors.inkMid;
    return Tooltip(
      message: tooltip,
      child: MouseRegion(
        cursor: onTap == null ? MouseCursor.defer : SystemMouseCursors.click,
        child: GestureDetector(
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.all(Space.xs),
            child: Icon(icon, size: 16, color: colour),
          ),
        ),
      ),
    );
  }
}

class _Chip extends StatelessWidget {
  const _Chip({required this.label, required this.on, required this.onTap});

  final String label;
  final bool on;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      child: GestureDetector(
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(
            horizontal: Space.sm,
            vertical: 3,
          ),
          decoration: BoxDecoration(
            color: on ? OrblitColors.raised : Colors.transparent,
            borderRadius: BorderRadius.circular(Radii.control),
            border: Border.all(
              color: on ? OrblitColors.line : Colors.transparent,
            ),
          ),
          child: Text(
            label,
            style: OrblitText.caption.copyWith(
              fontSize: 11,
              color: on ? OrblitColors.ink : OrblitColors.inkDim,
            ),
          ),
        ),
      ),
    );
  }
}

const _menuStyle = MenuStyle(
  backgroundColor: WidgetStatePropertyAll(OrblitColors.raised),
  surfaceTintColor: WidgetStatePropertyAll(Colors.transparent),
  shape: WidgetStatePropertyAll(
    RoundedRectangleBorder(
      borderRadius: BorderRadius.all(Radius.circular(Radii.panel)),
      side: BorderSide(color: OrblitColors.line),
    ),
  ),
);

/// A label that opens a menu, the way every choice on the toolbar is made.
class _Menu extends StatelessWidget {
  const _Menu({
    required this.label,
    required this.items,
    this.icon,
    this.tooltip,
    this.enabled = true,
    this.warn = false,
  });

  final String label;
  final List<Widget> items;
  final IconData? icon;
  final String? tooltip;
  final bool enabled;
  final bool warn;

  @override
  Widget build(BuildContext context) {
    final colour = !enabled
        ? OrblitColors.line
        : warn
        ? OrblitColors.warn
        : OrblitColors.inkMid;
    final menu = MenuAnchor(
      style: _menuStyle,
      menuChildren: items,
      builder: (context, controller, _) => MouseRegion(
        cursor: enabled ? SystemMouseCursors.click : MouseCursor.defer,
        child: GestureDetector(
          onTap: !enabled
              ? null
              : () => controller.isOpen
                    ? controller.close()
                    : controller.open(),
          child: Container(
            padding: const EdgeInsets.symmetric(
              horizontal: Space.sm,
              vertical: 3,
            ),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(Radii.control),
              border: Border.all(color: OrblitColors.lineSoft),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (icon != null) ...[
                  Icon(icon, size: 13, color: colour),
                  const SizedBox(width: 5),
                ],
                Text(
                  label,
                  style: OrblitText.caption.copyWith(
                    fontSize: 11,
                    color: enabled ? OrblitColors.ink : OrblitColors.inkDim,
                  ),
                ),
                const SizedBox(width: 2),
                Icon(Icons.arrow_drop_down, size: 14, color: colour),
              ],
            ),
          ),
        ),
      ),
    );
    return tooltip == null ? menu : Tooltip(message: tooltip, child: menu);
  }
}
