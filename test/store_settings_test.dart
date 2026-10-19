import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/store/store_settings.dart';
import 'package:path/path.dart' as p;

late Directory scratch;
late File file;

void main() {
  setUp(() {
    scratch = Directory.systemTemp.createTempSync('orblit_store_settings_');
    file = File(p.join(scratch.path, 'support', 'store.json'));
  });
  tearDown(() => scratch.deleteSync(recursive: true));

  test('has the default store and nothing else before anything is saved', () {
    final settings = StoreSettings(file);
    expect(settings.repositories, [defaultStoreAddress]);
    expect(settings.showIncompatible, isFalse);
  });

  test('keeps an added store and the choice for the next run', () {
    StoreSettings(file)
      ..addRepository('https://kits.example/shop')
      ..setShowIncompatible(true);
    final again = StoreSettings(file);
    expect(again.repositories, [
      defaultStoreAddress,
      Uri.parse('https://kits.example/shop/'),
    ]);
    expect(again.showIncompatible, isTrue);
  });

  test('forgets a removed store, and never the default', () {
    final settings = StoreSettings(file)
      ..addRepository('https://kits.example/shop/')
      ..removeRepository(Uri.parse('https://kits.example/shop/'))
      ..removeRepository(defaultStoreAddress);
    expect(settings.repositories, [defaultStoreAddress]);
    expect(StoreSettings(file).repositories, [defaultStoreAddress]);
  });

  test('says why an address cannot be added', () {
    final settings = StoreSettings(file)
      ..addRepository('https://kits.example/shop/');
    for (final text in [
      'http://kits.example/shop/',
      'kits.example/shop',
      'https://kits.example/shop/?a=1',
      'https://kits.example/shop',
      'https://orblitengine.com/store',
      '',
    ]) {
      expect(settings.repositoryProblem(text), isNotNull, reason: text);
      expect(() => settings.addRepository(text), throwsArgumentError);
    }
    expect(settings.repositoryProblem(' https://other.example/ '), isNull);
  });

  test('opens on a damaged file, and on one with other things in it', () {
    file.parent.createSync(recursive: true);
    for (final text in ['{not json', '[]', '{"repositories":"x"}']) {
      file.writeAsStringSync(text);
      expect(StoreSettings(file).repositories, [defaultStoreAddress]);
    }
    file.writeAsStringSync(
      '{"repositories":["http://bad.example/",3,"https://ok.example/"]}',
    );
    expect(StoreSettings(file).repositories, [
      defaultStoreAddress,
      Uri.parse('https://ok.example/'),
    ]);
  });

  test('works for this run when the file cannot be written', () {
    // A folder where the file belongs.
    Directory(file.path).createSync(recursive: true);
    final settings = StoreSettings(file)
      ..addRepository('https://kits.example/shop/');
    expect(settings.repositories, hasLength(2));
  });
}
