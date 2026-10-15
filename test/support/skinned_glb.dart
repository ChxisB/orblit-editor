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
