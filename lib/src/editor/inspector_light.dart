part of 'inspector.dart';

// A light's fields. Which of them are shown depends on the kind: a
// spot has a cone, a sun has neither cone nor falloff, and the power
// control is in the unit the kind is actually measured in.

/// What each light type is called in the inspector, in the order they are
/// offered: the two an artist reaches for first, then the two that need
/// more said about them.
const Map<LightType, String> _lightNames = {
  LightType.point: 'Point',
  LightType.sun: 'Sun',
  LightType.spot: 'Spot',
  LightType.area: 'Area',
};

/// The same light, restated in the units of the type it is becoming.
///
/// Watts and watts per square metre are not interchangeable, and the ratio
/// between them here is the one the renderer already used: a point light's
/// lumens spread over a sphere. Converting keeps the scene looking as it
/// did, which is what somebody switching a type is expecting.
double _powerFor(
  LightType wanted, {
  required LightType from,
  required double power,
}) {
  const sphere = 4 * math.pi;
  if (wanted == from) return power;
  if (wanted == LightType.sun) return power / sphere;
  if (from == LightType.sun) return power * sphere;
  return power;
}

extension _Light on _Fields {
  Widget _light() {
    final type = object.lightType;
    final isSun = type == LightType.sun;
    final isSpot = type == LightType.spot;

    // The one light everything is lit from above by. A renderer draws one, so
    // being that light is a property of the scene rather than of the object:
    // the first directional light is it.
    final isCelestial = identical(object, scene.celestial);

    return OrblitSection(
      title: 'Light',
      icon: Icons.wb_sunny_outlined,
      child: Column(
        children: [
          if (isCelestial && isSun) ..._celestial(),
          _type(),
          _colour(),
          _power(),
          if (isSpot) ..._cone(),
          _size(),
          _shadows(),
          _note(),
        ],
      ),
    );
  }

  List<Widget> _celestial() => [
    ChoiceRow(
      label: 'Body',
      options: [for (final body in CelestialBody.values) body.label],
      selected: scene.activeBody.label,
      // Nothing to choose while the day is running: it is above the
      // horizon or it is not, and a control that fights the clock is
      // a control that loses.
      onSelect: scene.dayCycle
          ? null
          : (value) {
              final wanted = CelestialBody.values.firstWhere(
                (b) => b.label == value,
              );
              if (wanted == object.body) return;
              history
                ..run(
                  SetCelestialBody(
                    sceneId: sceneId,
                    id: object.id,
                    name: object.name,
                    from: object.body,
                    to: wanted,
                  ),
                )
                ..seal();
            },
    ),
    if (scene.dayCycle)
      Padding(
        padding: const EdgeInsets.fromLTRB(
          Space.md,
          Space.xs,
          Space.md,
          0,
        ),
        child: Text(
          'The day cycle is deciding: whichever body is above the '
          'horizon lights the scene, and its colour, strength and '
          'direction come from the hour.',
          style: OrblitText.caption,
        ),
      ),
  ];

  Widget _type() {
    final type = object.lightType;

    return ChoiceRow(
      label: 'Type',
      options: _lightNames.values.toList(),
      selected: _lightNames[type]!,
      onSelect: (value) {
        final wanted = _lightNames.entries
            .firstWhere((entry) => entry.value == value)
            .key;
        if (wanted == type) return;
        history
          ..run(
            SetLightType(
              sceneId: sceneId,
              id: object.id,
              name: object.name,
              from: type,
              to: wanted,
              fromPower: object.power,
              // The number means something different on the other side of
              // this change, so it is restated rather than carried: a sun
              // is watts per square metre and the rest are watts, and a
              // thousand of the second is a hundred suns.
              toPower: _powerFor(wanted, from: type, power: object.power),
            ),
          )
          ..seal();
      },
    );
  }

  Widget _colour() => ColourRow(
    label: 'Colour',
    value: object.colour,
    onChanged: (value) => history
      ..run(
        SetColour(
          sceneId: sceneId,
          id: object.id,
          name: object.name,
          from: object.colour,
          to: value,
        ),
      )
      ..seal(),
  );

  Widget _power() {
    final isSun = object.lightType == LightType.sun;

    return SliderRow(
      label: 'Power',
      value: object.power,
      min: 0,
      max: isSun ? 400 : 5000,
      decimals: isSun ? 1 : 0,
      unit: isSun ? ' W/m²' : ' W',
      onChanged: (value) => history.run(
        SetPower(
          sceneId: sceneId,
          id: object.id,
          name: object.name,
          from: object.power,
          to: value,
        ),
      ),
      onSettled: history.seal,
    );
  }

  List<Widget> _cone() => [
    SliderRow(
      label: 'Cone',
      value: object.spotSize,
      min: 1,
      max: 180,
      unit: '°',
      onChanged: (value) => _shape(size: value),
      onSettled: history.seal,
    ),
    SliderRow(
      label: 'Blend',
      value: object.spotBlend,
      min: 0,
      max: 1,
      decimals: 2,
      onChanged: (value) => _shape(blend: value),
      onSettled: history.seal,
    ),
  ];

  Widget _size() {
    final type = object.lightType;

    if (type == LightType.sun) {
      return SliderRow(
          label: 'Sun size',
          value: object.sunAngle,
          min: 0,
          max: 12,
          decimals: 2,
          unit: '°',
          onChanged: (value) => _shape(sun: value),
          onSettled: history.seal,
      );
    }

    final isArea = type == LightType.area;

    return SliderRow(
        label: isArea ? 'Size' : 'Radius',
        value: object.sourceRadius,
        min: 0,
        max: isArea ? 4 : 1,
        decimals: 2,
        unit: ' m',
        onChanged: (value) => _shape(radius: value),
        onSettled: history.seal,
    );
  }

  Widget _shadows() => ChoiceRow(
    label: 'Cast shadows',
    options: const ['Off', 'On'],
    selected: object.castShadows ? 'On' : 'Off',
    onSelect: (value) {
      final wanted = value == 'On';
      if (wanted == object.castShadows) return;
      history
        ..run(
          SetCastShadows(
            sceneId: sceneId,
            id: object.id,
            name: object.name,
            to: wanted,
          ),
        )
        ..seal();
    },
  );

  Widget _note() {
    final type = object.lightType;

    return Padding(
      padding: const EdgeInsets.fromLTRB(Space.md, Space.xs, Space.md, 0),
      child: Text(switch (type) {
        LightType.sun =>
          'Sun size is the width of the source in the sky. It is what '
              'makes a shadow crisp at your feet and soft at its end.',
        LightType.area =>
          'An area light is drawn as a point of the same power at the '
              'centre of the shape. The falloff is right; the soft '
              'shadow its surface would cast is not.',
        _ =>
          'Radius is how big the source is, not how bright. Power '
              'stays the same and spreads over a wider surface, which '
              'is what widens the penumbra.',
      }, style: OrblitText.caption),
    );
  }


  /// Runs one shape edit, keeping the values that were not touched.
  void _shape({double? size, double? blend, double? radius, double? sun}) {
    history.run(
      SetLightShape(
        sceneId: sceneId,
        id: object.id,
        name: object.name,
        from: (
          size: object.spotSize,
          blend: object.spotBlend,
          radius: object.sourceRadius,
          sun: object.sunAngle,
        ),
        to: (
          size: size ?? object.spotSize,
          blend: blend ?? object.spotBlend,
          radius: radius ?? object.sourceRadius,
          sun: sun ?? object.sunAngle,
        ),
      ),
    );
  }
}
