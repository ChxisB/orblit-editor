part of 'inspector.dart';

// The weather fields, which edit the scene rather than an object: sky,
// cloud, wind and air. Every one of them writes through a setter that
// records a single undoable step.

extension _Weather on InspectorTarget {
  Widget _weather() {
    return OrblitSection(
      title: 'Weather',
      icon: Icons.cloud_outlined,
      child: Column(
        children: [
          ..._condition(),
          ..._clouds(),
          ..._precipitation(),
          ..._wind(),
          _caption(),
        ],
      ),
    );
  }

  List<Widget> _condition() => [
    // Six across one row would be six words nobody can read. Split at
    // the point they split anyway: the ones you can see through, and
    // the ones you cannot.
    ChoiceRow(
      label: 'Condition',
      options: [
        for (final condition in WeatherCondition.values.take(4))
          condition.label,
      ],
      selected: object.condition.label,
      onSelect: (value) => _setCondition(value),
    ),
    ChoiceRow(
      label: '',
      options: [
        for (final condition in WeatherCondition.values.skip(4))
          condition.label,
      ],
      selected: object.condition.label,
      onSelect: (value) => _setCondition(value),
    ),
  ];

  List<Widget> _clouds() {
    final air = object.weather;

    return [
      SliderRow(
        label: 'Cloud',
        value: air.cloudCover,
        min: 0,
        max: 1,
        decimals: 2,
        onChanged: (value) => _setAir(air.copyWith(cloudCover: value)),
        onSettled: history.seal,
      ),
      if (air.cloudCover > 0.01) ...[
        // What shape it is, which is a different question from how much
        // of it there is. Automatic follows the condition, so somebody
        // who has not made a choice still gets a new sky when the
        // weather changes.
        ChoiceRow(
          label: 'Cloud',
          options: const ['Auto', 'Cumulus', 'Stratocumulus'],
          selected: _cloudLabel(object.cloudKind, 0),
          onSelect: _setCloudKind,
        ),
        ChoiceRow(
          label: '',
          options: const ['Stratus', 'Cirrus', 'Cumulonimbus'],
          selected: _cloudLabel(object.cloudKind, 1),
          onSelect: _setCloudKind,
        ),
        SliderRow(
          label: 'Cloud height',
          value: air.cloudHeight,
          min: 40,
          max: 8000,
          decimals: 0,
          unit: ' m',
          onChanged: (value) => _setAir(air.copyWith(cloudHeight: value)),
          onSettled: history.seal,
        ),
      ],
    ];
  }

  List<Widget> _precipitation() {
    final air = object.weather;

    return [
      SliderRow(
        label: 'Rain',
        value: air.rain,
        min: 0,
        max: 1,
        decimals: 2,
        onChanged: (value) => _setAir(air.copyWith(rain: value)),
        onSettled: history.seal,
      ),
      SliderRow(
        label: 'Snow',
        value: air.snow,
        min: 0,
        max: 1,
        decimals: 2,
        onChanged: (value) => _setAir(air.copyWith(snow: value)),
        onSettled: history.seal,
      ),
      SliderRow(
        label: 'Lightning',
        value: air.lightning,
        min: 0,
        max: 1,
        decimals: 2,
        onChanged: (value) => _setAir(air.copyWith(lightning: value)),
        onSettled: history.seal,
      ),
    ];
  }

  List<Widget> _wind() {
    final air = object.weather;

    return [
      SliderRow(
        label: 'Wind',
        value: air.windSpeed,
        min: 0,
        max: 25,
        decimals: 1,
        unit: ' m/s',
        onChanged: (value) => _setAir(air.copyWith(windSpeed: value)),
        onSettled: history.seal,
      ),
      SliderRow(
        label: 'Bearing',
        value: object.windDirection,
        min: 0,
        max: 360,
        unit: '°',
        onChanged: (value) => _setWind(direction: value),
        onSettled: history.seal,
      ),
      SliderRow(
        label: 'Changes over',
        value: object.transitionSeconds,
        min: 0,
        max: 60,
        decimals: 1,
        unit: ' s',
        onChanged: (value) => _setWind(transition: value),
        onSettled: history.seal,
      ),
    ];
  }

  // Which of the three things worth saying about this weather applies.
  Widget _caption() {
    final air = object.weather;
    final spare = scene.hasSpareWeather && !identical(object, scene.weather);

    return Padding(
      padding: const EdgeInsets.fromLTRB(Space.md, Space.xs, Space.md, 0),
      child: Text(
        spare
            ? 'Another Weather object is already deciding what the air '
                  'is doing. This one is ignored — a scene answers that '
                  'question once.'
            : air.rain > 0 && air.snow > 0
            ? 'Both at once is the temperature where both are '
                  'falling. The curtain is one thing part of the way '
                  'from streaks to flakes.'
            : 'Cloud takes the strength out of whatever is above '
                  'the scene and spreads it across the sky. Shadows '
                  'lose their edges before they lose their depth.',
        style: OrblitText.caption,
      ),
    );
  }

  Widget _air() {
    final air = object.weather;

    return OrblitSection(
      title: 'Air',
      icon: Icons.foggy,
      child: Column(
        children: [
          ColourRow(
            label: 'Colour',
            value: air.fogColour.colour,
            onChanged: (value) {
              _setAir(air.copyWith(fogColour: value.tint));
              history.seal();
            },
          ),
          SliderRow(
            label: 'Density',
            value: air.fogDensity,
            min: 0,
            max: 0.4,
            decimals: 3,
            onChanged: (value) => _setAir(air.copyWith(fogDensity: value)),
            onSettled: history.seal,
          ),
          if (air.fogDensity > 0) ...[
            SliderRow(
              label: 'Height',
              value: air.fogHeight,
              min: -20,
              max: 20,
              decimals: 1,
              unit: ' m',
              onChanged: (value) => _setAir(air.copyWith(fogHeight: value)),
              onSettled: history.seal,
            ),
            SliderRow(
              label: 'Falloff',
              value: air.fogFalloff,
              min: 0.02,
              max: 2,
              decimals: 2,
              onChanged: (value) => _setAir(air.copyWith(fogFalloff: value)),
              onSettled: history.seal,
            ),
            SliderRow(
              label: 'Ground mist',
              value: air.mist,
              min: 0,
              max: 1,
              decimals: 2,
              onChanged: (value) => _setAir(air.copyWith(mist: value)),
              onSettled: history.seal,
            ),
            if (air.mist > 0)
              SliderRow(
                label: 'Mist size',
                value: air.mistSize,
                min: 2,
                max: 120,
                decimals: 0,
                unit: ' m',
                onChanged: (value) => _setAir(air.copyWith(mistSize: value)),
                onSettled: history.seal,
              ),
            Padding(
              padding: const EdgeInsets.fromLTRB(
                Space.md,
                Space.xs,
                Space.md,
                0,
              ),
              child: Text(
                air.mist > 0
                    ? 'Mist is the air at ground level given a shape, moving '
                          'with the wind. The cloud in the sky is the setting '
                          'above — these are two different pieces of weather.'
                    : 'Density is the even haze that distance looks like. '
                          'Mist gives it a shape near the ground.',
                style: OrblitText.caption,
              ),
            ),
          ],
        ],
      ),
    );
  }

  /// Which of the two rows shows a tick, so the chosen one is lit and the
  /// other is not — a row that always answers would show two.
  String _cloudLabel(CloudKind? kind, int row) {
    final name = kind == null ? 'Auto' : kind.label;
    const rows = [
      ['Auto', 'Cumulus', 'Stratocumulus'],
      ['Stratus', 'Cirrus', 'Cumulonimbus'],
    ];
    return rows[row].contains(name) ? name : '';
  }

  /// Where a shape starts out, read off the shape itself rather than written
  /// down twice.
  double _heightFor(CloudKind? kind) => switch (kind) {
    null || CloudKind.none => 900,
    CloudKind.cumulus => OrblitClouds.cumulus().altitude,
    CloudKind.stratocumulus => OrblitClouds.stratocumulus().altitude,
    CloudKind.stratus => OrblitClouds.stratus().altitude,
    CloudKind.cirrus => OrblitClouds.cirrus().altitude,
    CloudKind.cumulonimbus => OrblitClouds.cumulonimbus().altitude,
  };

  void _setCloudKind(String label) {
    final wanted = label == 'Auto'
        ? null
        : CloudKind.values.firstWhere((kind) => kind.label == label);
    if (wanted == object.cloudKind) return;
    history
      ..run(
        SetCloudKind(
          sceneId: sceneId,
          id: object.id,
          from: object.cloudKind,
          to: wanted,
          fromHeight: object.weather.cloudHeight,
          toHeight: _heightFor(wanted),
        ),
      )
      ..seal();
  }

  void _setCondition(String label) {
    final wanted = WeatherCondition.values.firstWhere((c) => c.label == label);
    if (wanted == object.condition) return;
    history
      ..run(
        SetWeatherCondition(
          sceneId: sceneId,
          id: object.id,
          from: object.condition,
          to: wanted,
          fromState: object.weather,
          toState: WeatherState.of(wanted),
        ),
      )
      ..seal();
  }

  void _setAir(WeatherState air) {
    history.run(
      SetWeatherValues(
        sceneId: sceneId,
        id: object.id,
        from: object.weather,
        to: air,
      ),
    );
  }

  void _setWind({double? direction, double? transition}) {
    history.run(
      SetWeatherWind(
        sceneId: sceneId,
        id: object.id,
        from: (
          direction: object.windDirection,
          transition: object.transitionSeconds,
        ),
        to: (
          direction: direction ?? object.windDirection,
          transition: transition ?? object.transitionSeconds,
        ),
      ),
    );
  }
}
