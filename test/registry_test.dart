import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/registry.dart';

class _Thing implements Registered {
  const _Thing(this.name);

  @override
  final String name;
}

void main() {
  late Registry<_Thing> registry;

  List<String> names() => [for (final thing in registry.all) thing.name];

  setUp(() => registry = Registry<_Thing>());

  test('keeps things in the order they were registered', () {
    registry
      ..register(const _Thing('outliner'))
      ..register(const _Thing('scene'))
      ..register(const _Thing('inspector'));

    expect(names(), ['outliner', 'scene', 'inspector']);
  });

  test('finds a thing by its name, and nothing by any other', () {
    const scene = _Thing('scene');
    registry.register(scene);

    expect(registry['scene'], same(scene));
    expect(registry['terrain'], isNull);
  });

  test('puts a thing ahead of the one it is registered before', () {
    registry
      ..register(const _Thing('light'))
      ..register(const _Thing('interface'))
      ..register(const _Thing('shape'), before: 'interface');

    expect(names(), ['light', 'shape', 'interface']);
  });

  test('registered before something missing, it goes last', () {
    registry
      ..register(const _Thing('light'))
      ..register(const _Thing('shape'), before: 'interface');

    expect(names(), ['light', 'shape']);
  });

  test('refuses a second thing under a name already taken', () {
    registry.register(const _Thing('scene'));

    expect(
      () => registry.register(const _Thing('scene')),
      throwsA(isA<StateError>()),
    );
    expect(names(), ['scene']);
  });

  test('cannot be changed through what it hands out', () {
    registry.register(const _Thing('scene'));

    expect(() => registry.all.add(const _Thing('game')), throwsUnsupportedError);
  });
}
