part of 'interface_mode.dart';

// The panel of properties for whichever element is selected. Which
// properties those are depends on the kind, so most of this file is the
// switch that decides.

/// The palette, the selected element's properties and the canvas's.
class _Side extends StatelessWidget {
  const _Side({required this.bench, required this.document});

  final InterfaceBench bench;
  final UiDocument document;

  /// Whether a container turns into a column on a narrow screen.
  static bool _stacks(UiNode node) =>
      node.classes.split(RegExp(r'\s+')).contains('md:row');

  /// The containers a split means anything for.
  ///
  /// Splitting a piece of text into three columns is not a layout, it is a
  /// question nobody asked.
  static const _containers = {'column', 'row', 'stack', 'box'};

  /// Screens worth one press. Every one is a real device rather than a round
  /// number, and between them they cross every breakpoint there is. That is
  /// the point of having them one press apart.
  static const _devices = <String, Size?>{
    'Canvas': null,
    'Phone': Size(390, 844),
    'Tablet': Size(834, 1112),
    'Laptop': Size(1440, 900),
    'Desktop': Size(1920, 1080),
    'TV': Size(3840, 2160),
  };

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

  /// Sizes worth having one press away. Every one is a real screen somebody
  /// ships to, rather than a round number.
  static const _sizes = <String, (double, double)>{
    '1920 × 1080': (1920, 1080),
    '2560 × 1440': (2560, 1440),
    '1280 × 720': (1280, 720),
    '390 × 844': (390, 844),
    '1024 × 768': (1024, 768),
  };

  /// [props] with `onPressed` naming [handler], or without it when [handler]
  /// is empty.
  static Map<String, Object?> _withHandler(
    Map<String, Object?> props,
    String handler,
  ) => {
    for (final entry in props.entries)
      if (entry.key != 'onPressed') entry.key: entry.value,
    if (handler.isNotEmpty) 'onPressed': handler,
  };

  @override
  Widget build(BuildContext context) {
    final selected = bench.element;
    return ListView(
      padding: const EdgeInsets.symmetric(vertical: Space.sm),
      children: [
        _add(),
        if (selected != null) _properties(selected),
        _canvas(),
        _grid(),
      ],
    );
  }

  Widget _add() {
    return OrblitSection(
      title: 'Add',
      child: Wrap(
        spacing: Space.xs,
        runSpacing: Space.xs,
        children: [
          for (final what in UiElement.values)
            _Chip(
              label: what.label,
              tooltip:
                  'Add ${what.label.toLowerCase()} to the selected container.',
              icon: what.icon,
              onTap: () => bench.add(what),
            ),
        ],
      ),
    );
  }

  // The selected element's own properties, which depend on its kind.
  Widget _properties(UiNode selected) {
    final container = _containers.contains(selected.type);
    return OrblitSection(
      title: selected.type,
      child: Column(
        children: [
          if (selected.text != null ||
              selected.type == 'text' ||
              selected.type == 'button')
            _typed(
              label: 'Words',
              step: 'Edit words',
              value: selected.text ?? '',
              mono: false,
              change: (node, value) => node.copyWith(text: value),
            ),
          _typed(
            label: 'Classes',
            step: 'Edit classes',
            value: selected.classes,
            hint: 'p-4 flex-1 md:row lg:text-2xl',
            change: (node, value) => node.copyWith(classes: value),
          ),
          _typed(
            label: 'CSS',
            step: 'Edit CSS',
            value: selected.css,
            hint: 'padding: 8px 12px',
            change: (node, value) => node.copyWith(css: value),
          ),
          if (container) _narrow(selected),
          if (container) _split(),
          _typed(
            label: 'Handler',
            step: 'Edit handler',
            value: '${selected.props['onPressed'] ?? ''}',
            hint: 'what script calls',
            change: (node, value) => node.copyWith(
              props: _withHandler(node.props, value.trim()),
            ),
          ),
        ],
      ),
    );
  }

  /// A field typed into. What is typed is one step to undo, which ends when
  /// the field is left.
  Widget _typed({
    required String label,
    required String step,
    required String value,
    required UiNode Function(UiNode node, String value) change,
    String? hint,
    bool mono = true,
  }) {
    return FieldRow(
      label: label,
      child: ValueField(
        value: value,
        mono: mono,
        hint: hint,
        onChanged: (typed) => bench.editElement(
          step,
          (node) => change(node, typed),
          gesture: step,
        ),
        onDone: bench.settle,
      ),
    );
  }

  Widget _narrow(UiNode selected) {
    return FieldRow(
      label: 'Narrow',
      child: _Chip(
        label: _stacks(selected) ? 'Stacks' : 'Row',
        tooltip:
            'Whether this turns into a column on a '
            'screen narrower than the md breakpoint. '
            'Written as the classes col md:row, so it '
            'can be changed by hand as well.',
        selected: _stacks(selected),
        onTap: () => bench.editElement(
          'Change narrow layout',
          (node) => node.copyWith(
            classes: _stacks(node)
                ? flowing(node.classes)
                : '${flowing(node.classes)} col md:row'.trim(),
          ),
        ),
      ),
    );
  }

  Widget _split() {
    return FieldRow(
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
              onTap: () => bench.split(count),
            ),
            const SizedBox(width: Space.xs),
          ],
        ],
      ),
    );
  }

  /// A slider over one of the canvas's numbers. A drag is one step to undo.
  Widget _slider({
    required String label,
    required double value,
    required (double, double) range,
    required UiCanvas Function(double value) change,
    String? unit,
  }) {
    final step = 'Change ${label.toLowerCase()}';
    return SliderRow(
      label: label,
      value: value,
      min: range.$1,
      max: range.$2,
      unit: unit,
      onChanged: (moved) =>
          bench.setCanvas(step, change(moved), gesture: step),
      onSettled: bench.settle,
    );
  }

  Widget _canvas() {
    final canvas = document.canvas;
    return OrblitSection(
      title: 'Canvas',
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
                      canvas.width == entry.value.$1 &&
                      canvas.height == entry.value.$2,
                  onTap: () => bench.setCanvas(
                    'Change canvas size',
                    canvas.copyWith(
                      width: entry.value.$1,
                      height: entry.value.$2,
                    ),
                  ),
                ),
            ],
          ),
          const SizedBox(height: Space.sm),
          const Text('ON SCREEN', style: OrblitText.section),
          const SizedBox(height: Space.xs),
          // Chips that wrap rather than four segments sharing one
          // row: "Match height" does not fit in a quarter of a
          // narrow panel, and a label clipped in half is a
          // control nobody can read.
          Wrap(
            spacing: Space.xs,
            runSpacing: Space.xs,
            children: [
              for (final fit in CanvasFit.values)
                _Chip(
                  label: fit.label,
                  tooltip: _fitExplains[fit],
                  selected: canvas.fit == fit,
                  onTap: () => bench.setCanvas(
                    'Change how it fits',
                    canvas.copyWith(fit: fit),
                  ),
                ),
            ],
          ),
          const SizedBox(height: Space.sm),
          _slider(
            label: 'Safe area',
            value: canvas.safeArea * 100,
            range: (0, 20),
            unit: '%',
            change: (value) => canvas.copyWith(safeArea: value / 100),
          ),
          const SizedBox(height: Space.sm),
          const Text('PREVIEW ON', style: OrblitText.section),
          const SizedBox(height: Space.xs),
          _previewOn(),
        ],
      ),
    );
  }

  // A view rather than a property of the file, so none of it is a step to
  // undo. A responsive interface is a different layout at every width, so
  // one that could only be looked at in its own reference size would be the
  // one screen nobody worried about.
  Widget _previewOn() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
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
                selected: bench.device == entry.value,
                onTap: () => bench.device = entry.value,
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
                selected: bench.landscape == sideways,
                onTap: () => bench.landscape = sideways,
              ),
              const SizedBox(width: Space.xs),
            ],
          ],
        ),
      ],
    );
  }

  Widget _grid() {
    final canvas = document.canvas;
    return OrblitSection(
      title: 'Grid',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _slider(
            label: 'Columns',
            value: canvas.columns.toDouble(),
            range: (1, 24),
            change: (value) => canvas.copyWith(columns: value.round()),
          ),
          _slider(
            label: 'Gutter',
            value: canvas.gutter,
            range: (0, 80),
            unit: 'px',
            change: (value) => canvas.copyWith(gutter: value),
          ),
          // What is actually drawn, which on a phone is fewer
          // than what is authored. Said out loud because a grid
          // that quietly changed its mind is a grid somebody
          // mistakes for their own layout.
          _GridCount(document: document, preview: bench.preview),
          // The grid sits inside the safe area, so the outer
          // margin and the edge a television eats are one
          // measurement rather than two that disagree.
          const SizedBox(height: Space.sm),
          const Text('FLUID RANGE', style: OrblitText.section),
          const SizedBox(height: Space.xs),
          _slider(
            label: 'Smallest',
            value: canvas.minScale * 100,
            range: (40, 100),
            unit: '%',
            change: (value) => canvas.copyWith(minScale: value / 100),
          ),
          _slider(
            label: 'Largest',
            value: canvas.maxScale * 100,
            range: (100, 300),
            unit: '%',
            change: (value) => canvas.copyWith(maxScale: value / 100),
          ),
        ],
      ),
    );
  }
}
