part of 'ui_editor.dart';

// The panel of properties for whichever element is selected. Which
// properties those are depends on the kind, so most of this file is the
// switch that decides.

/// The palette, the element's properties and the canvas's.
class _Side extends StatelessWidget {
  const _Side({
    required this.document,
    required this.element,
    required this.device,
    required this.landscape,
    required this.preview,
    required this.onCanvas,
    required this.onElement,
    required this.onAdd,
    required this.onSplit,
    required this.onDevice,
    required this.onLandscape,
  });

  final UiDocument document;
  final UiNode? element;

  /// The device chosen, upright, and whether it is being held sideways.
  final Size? device;
  final bool landscape;

  /// The two of them together: what the canvas is actually laid out at.
  final Size? preview;

  final ValueChanged<UiCanvas> onCanvas;
  final void Function(UiNode Function(UiNode)) onElement;
  final ValueChanged<UiElement> onAdd;
  final ValueChanged<int> onSplit;
  final ValueChanged<Size?> onDevice;
  final ValueChanged<bool> onLandscape;

  /// Whether a container turns into a column on a narrow screen.
  static bool _stacks(UiNode node) =>
      node.classes.split(RegExp(r'\s+')).contains('md:row');

  /// The containers a split means anything for.
  ///
  /// Splitting a piece of text into three columns is not a layout, it is a
  /// question nobody asked.
  static const _containers = {'column', 'row', 'stack', 'box'};

  /// Screens worth one press. Every one is a real device rather than a round
  /// number, and between them they cross every breakpoint there is — which is
  /// the point of having them one press apart.
  static const _devices = <String, Size?>{
    'Canvas': null,
    'Phone': Size(390, 844),
    'Tablet': Size(834, 1112),
    'Laptop': Size(1440, 900),
    'Desktop': Size(1920, 1080),
    'TV': Size(3840, 2160),
  };

  /// Sizes worth having one press away. Every one is a real screen somebody
  /// ships to, rather than a round number.
  /// What each fit means, since the name is three words and the behaviour is
  /// the thing somebody is choosing between.
  static const _fitExplains = <CanvasFit, String>{
    CanvasFit.responsive:
        'Laid out at whatever size the screen is. Prefixed '
        'classes decide what changes, and text and spacing grow with the '
        'screen instead of the whole picture being magnified.',
    CanvasFit.width:
        'The width always fills the screen. The bottom of a '
        'taller screen is empty and a shorter one cuts the bottom off.',
    CanvasFit.height:
        'The height always fits. A wider screen has space at '
        'the sides and a narrower one cuts them off.',
    CanvasFit.contain:
        'All of it fits, with space where the shape does not '
        'match. Nothing is ever cut off.',
    CanvasFit.none: 'Not scaled. Pixels are pixels, however big the screen is.',
  };

  static const _sizes = <String, (double, double)>{
    '1920 × 1080': (1920, 1080),
    '2560 × 1440': (2560, 1440),
    '1280 × 720': (1280, 720),
    '390 × 844': (390, 844),
    '1024 × 768': (1024, 768),
  };

  @override
  Widget build(BuildContext context) {
    final selected = element;

    return Container(
      width: 296,
      decoration: const BoxDecoration(
        color: OrblitColors.surface,
        border: Border(left: BorderSide(color: OrblitColors.lineSoft)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            height: 30,
            padding: const EdgeInsets.symmetric(horizontal: Space.md),
            alignment: Alignment.centerLeft,
            decoration: const BoxDecoration(
              border: Border(bottom: BorderSide(color: OrblitColors.lineSoft)),
            ),
            child: Text('INTERFACE', style: OrblitText.section),
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.symmetric(vertical: Space.sm),
              children: [
                _add(),
                if (selected != null) _properties(selected),
                _canvas(),
                _grid(),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _add() {
    return OrblitSection(
      title: 'Add',
      icon: Icons.add,
      child: Wrap(
        spacing: Space.xs,
        runSpacing: Space.xs,
        children: [
          for (final what in UiElement.values)
            _Chip(
              label: what.label,
              icon: what.icon,
              onTap: () => onAdd(what),
            ),
        ],
      ),
    );
  }

  // The selected element's own properties, which depend on its kind.
  Widget _properties(UiNode selected) {
    return OrblitSection(
      title: selected.type,
      icon: _TreeRow._iconFor(selected.type),
      child: Column(
        children: [
          if (selected.text != null ||
              selected.type == 'text' ||
              selected.type == 'button')
            FieldRow(
              label: 'Words',
              child: ValueField(
                value: selected.text ?? '',
                onChanged: (value) => onElement(
                  (node) => node.copyWith(text: value),
                ),
              ),
            ),
          FieldRow(
            label: 'Classes',
            child: ValueField(
              value: selected.classes,
              mono: true,
              hint: 'p-4 flex-1 md:row lg:text-2xl',
              onChanged: (value) => onElement(
                (node) => node.copyWith(classes: value),
              ),
            ),
          ),
          FieldRow(
            label: 'CSS',
            child: ValueField(
              value: selected.css,
              mono: true,
              hint: 'padding: 8px 12px',
              onChanged: (value) =>
                  onElement((node) => node.copyWith(css: value)),
            ),
          ),
          if (_containers.contains(selected.type))
            FieldRow(
              label: 'Narrow',
              child: _Chip(
                label: _stacks(selected) ? 'Stacks' : 'Row',
                tooltip:
                    'Whether this turns into a column on a '
                    'screen narrower than the md breakpoint. '
                    'Written as the classes col md:row, so it '
                    'can be changed by hand as well.',
                selected: _stacks(selected),
                onTap: () => onElement(
                  (node) => node.copyWith(
                    classes: _stacks(node)
                        ? _flowing(node.classes)
                        : '${_flowing(node.classes)} col md:row'
                              .trim(),
                  ),
                ),
              ),
            ),
          if (_containers.contains(selected.type))
            FieldRow(
              label: 'Split',
              child: Row(
                children: [
                  for (final count in const [2, 3, 4]) ...[
                    _Chip(
                      label: '$count',
                      tooltip:
                          'Make this a row of $count equal '
                          'columns. What is in it goes into the '
                          'first one.',
                      onTap: () => onSplit(count),
                    ),
                    const SizedBox(width: Space.xs),
                  ],
                ],
              ),
            ),
          FieldRow(
            label: 'Handler',
            child: ValueField(
              value: '${selected.props['onPressed'] ?? ''}',
              mono: true,
              hint: 'what script calls',
              onChanged: (value) => onElement(
                (node) => node.copyWith(
                  props:
                      {
                        ...node.props,
                        if (value.trim().isNotEmpty)
                          'onPressed': value.trim()
                        else
                          ...{},
                      }..removeWhere(
                        (key, _) =>
                            key == 'onPressed' &&
                            value.trim().isEmpty,
                      ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _canvas() {
    return OrblitSection(
      title: 'Canvas',
      icon: Icons.aspect_ratio,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Wrap(
            spacing: Space.xs,
            runSpacing: Space.xs,
            children: [
              for (final entry in _sizes.entries)
                _Chip(
                  label: entry.key,
                  selected:
                      document.canvas.width == entry.value.$1 &&
                      document.canvas.height == entry.value.$2,
                  onTap: () => onCanvas(
                    document.canvas.copyWith(
                      width: entry.value.$1,
                      height: entry.value.$2,
                    ),
                  ),
                ),
            ],
          ),
          const SizedBox(height: Space.sm),
          Text('ON SCREEN', style: OrblitText.section),
          const SizedBox(height: Space.xs),
          // Chips that wrap rather than four segments sharing one
          // row: "Match height" does not fit in a quarter of a
          // 296-wide panel, and a label clipped in half is a
          // control nobody can read.
          Wrap(
            spacing: Space.xs,
            runSpacing: Space.xs,
            children: [
              for (final fit in CanvasFit.values)
                _Chip(
                  label: fit.label,
                  tooltip: _fitExplains[fit],
                  selected: document.canvas.fit == fit,
                  onTap: () =>
                      onCanvas(document.canvas.copyWith(fit: fit)),
                ),
            ],
          ),
          const SizedBox(height: Space.sm),
          SliderRow(
            label: 'Safe area',
            value: document.canvas.safeArea * 100,
            min: 0,
            max: 20,
            unit: '%',
            onChanged: (value) => onCanvas(
              document.canvas.copyWith(safeArea: value / 100),
            ),
          ),
          const SizedBox(height: Space.sm),
          Text('PREVIEW ON', style: OrblitText.section),
          const SizedBox(height: Space.xs),
          // A view rather than a property of the file. A
          // responsive interface is a different layout at every
          // width, so one that could only be looked at in its own
          // reference size would be the one screen nobody worried
          // about.
          Wrap(
            spacing: Space.xs,
            runSpacing: Space.xs,
            children: [
              for (final entry in _devices.entries)
                _Chip(
                  label: entry.key,
                  tooltip: entry.value == null
                      ? 'The size this was drawn against.'
                      : '${entry.value!.width.round()} × '
                            '${entry.value!.height.round()} upright',
                  // Against the device, not the size being shown:
                  // a phone held sideways is still the phone.
                  selected: device == entry.value,
                  onTap: () => onDevice(entry.value),
                ),
            ],
          ),
          const SizedBox(height: Space.xs),
          // Held which way. Its own control rather than two more
          // device chips, because every device has both and a
          // list with each of them twice is a list nobody reads.
          Row(
            children: [
              for (final sideways in const [false, true]) ...[
                _Chip(
                  label: sideways ? 'Landscape' : 'Portrait',
                  selected: landscape == sideways,
                  onTap: () => onLandscape(sideways),
                ),
                const SizedBox(width: Space.xs),
              ],
            ],
          ),
        ],
      ),
    );
  }

  Widget _grid() {
    return OrblitSection(
      title: 'Grid',
      icon: Icons.view_week_outlined,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SliderRow(
            label: 'Columns',
            value: document.canvas.columns.toDouble(),
            min: 1,
            max: 24,
            onChanged: (value) => onCanvas(
              document.canvas.copyWith(columns: value.round()),
            ),
          ),
          SliderRow(
            label: 'Gutter',
            value: document.canvas.gutter,
            min: 0,
            max: 80,
            unit: 'px',
            onChanged: (value) =>
                onCanvas(document.canvas.copyWith(gutter: value)),
          ),
          // What is actually drawn, which on a phone is fewer
          // than what is authored. Said out loud because a grid
          // that quietly changed its mind is a grid somebody
          // mistakes for their own layout.
          _GridCount(document: document, preview: preview),
          // The grid sits inside the safe area, so the outer
          // margin and the edge a television eats are one
          // measurement rather than two that disagree.
          const SizedBox(height: Space.sm),
          Text('FLUID RANGE', style: OrblitText.section),
          const SizedBox(height: Space.xs),
          SliderRow(
            label: 'Smallest',
            value: document.canvas.minScale * 100,
            min: 40,
            max: 100,
            unit: '%',
            onChanged: (value) => onCanvas(
              document.canvas.copyWith(minScale: value / 100),
            ),
          ),
          SliderRow(
            label: 'Largest',
            value: document.canvas.maxScale * 100,
            min: 100,
            max: 300,
            unit: '%',
            onChanged: (value) => onCanvas(
              document.canvas.copyWith(maxScale: value / 100),
            ),
          ),
        ],
      ),
    );
  }
}
