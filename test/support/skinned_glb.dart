import 'dart:convert';
import 'dart:typed_data';

// A model with a skeleton and nothing else, as a test can write it to disk.

/// [document] as the bytes of a `.glb` file.
Uint8List glb(Map<String, Object?> document) {
  final json = utf8.encode(jsonEncode(document));
  final padded = Uint8List.fromList([
    ...json,
    ...List.filled((4 - json.length % 4) % 4, 0x20),
  ]);
  final header = ByteData(20)
    ..setUint32(0, 0x46546C67, Endian.little)
    ..setUint32(4, 2, Endian.little)
    ..setUint32(8, 20 + padded.length, Endian.little)
    ..setUint32(12, padded.length, Endian.little)
    ..setUint32(16, 0x4E4F534A, Endian.little);
  return Uint8List.fromList([...header.buffer.asUint8List(), ...padded]);
}

/// A skeleton of bones [names], each the child of the one before and a
/// metre above it, as a `.glb` file.
Uint8List chainGlb(List<String> names) => glb({
  'asset': {'version': '2.0'},
  'scene': 0,
  'scenes': [
    {
      'nodes': [0],
    },
  ],
  'nodes': [
    for (var i = 0; i < names.length; i++)
      {
        'name': names[i],
        'translation': [0, i == 0 ? 0 : 1, 0],
        if (i + 1 < names.length) 'children': [i + 1],
      },
  ],
  'skins': [
    {'joints': List.generate(names.length, (i) => i)},
  ],
});

/// A person standing in Y up and facing +Z, so their left is +X, as a
/// `.glb` file: hips, a spine, two arms and two legs with the bones a limb
/// needs and no more.
///
/// Names are the plain ones. Which bone is which comes from how they hang
/// and where they stand, which is what an imported model has to be read by.
Uint8List personGlb() {
  final nodes = <Map<String, Object?>>[];
  final children = <int, List<int>>{};

  int add(String name, int parent, double x, double y, double z) {
    final index = nodes.length;
    nodes.add({
      'name': name,
      'translation': [x, y, z],
    });
    if (parent >= 0) children.putIfAbsent(parent, () => []).add(index);
    return index;
  }

  final hips = add('hips', -1, 0, 1, 0);
  final spine = add('spine', hips, 0, 0.15, 0);
  final chest = add('chest', spine, 0, 0.2, 0);
  final neck = add('neck', chest, 0, 0.25, 0);
  add('head', neck, 0, 0.12, 0);
  for (final (side, sign) in [('left', 1.0), ('right', -1.0)]) {
    final shoulder = add('$side shoulder', chest, sign * 0.06, 0.2, 0);
    final upper = add('$side upper arm', shoulder, sign * 0.1, 0, 0);
    final lower = add('$side lower arm', upper, sign * 0.28, 0, 0.02);
    add('$side hand', lower, sign * 0.25, 0, -0.02);
    final thigh = add('$side thigh', hips, sign * 0.1, -0.05, 0);
    final shin = add('$side shin', thigh, 0, -0.45, 0.02);
    final foot = add('$side foot', shin, 0, -0.42, -0.02);
    add('$side toes', foot, 0, -0.05, 0.12);
  }
  for (final entry in children.entries) {
    nodes[entry.key]['children'] = entry.value;
  }
  return glb({
    'asset': {'version': '2.0'},
    'scene': 0,
    'scenes': [
      {
        'nodes': [0],
      },
    ],
    'nodes': nodes,
    'skins': [
      {'joints': List.generate(nodes.length, (i) => i)},
    ],
  });
}
