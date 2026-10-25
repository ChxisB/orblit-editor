# Parity

One row per Dart test file that has been ported. A row is added when the file
is built.

Dart declared counts the test declarations in the file. Dart run counts the
tests that execute, because a loop runs one declaration many times. C# ported
counts the C# tests that carry a Dart test over. Added in C# counts tests with
no Dart twin. A file is done when C# ported equals Dart run.

The rules are the engine's `dotnet/PORTING.md`, in the `orblit` repository. The
engine packages come from a folder. The engine's `tool/pack_local.sh` packs
them, into `~/.orblit/feed` unless `ORBLIT_FEED` names another.
`Directory.Packages.props` pins the versions.

## Row 1, the scene document

| Dart test file | C# class | Dart declared | Dart run | C# ported | Added in C# | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `boundary_test.dart` | `BoundaryTests` | 15 | 15 | 15 | 1: a kind and an offset read back what was written | done |
| `surface_test.dart` | `SurfaceTests` | 10 | 10 | 10 | 0 | done |
| none, the Dart has no test | `ColourTests` | 0 | 0 | 0 | 10: hex in and out, the fallback, the tint round trip and the clamp | done |
| `scene_document_test.dart` | `SceneDocumentTests`, `ProjectTests` | 33 | 35 | 35 | 0 | done. The template loop runs three times, so Dart run is 35. The five tests that read a project template came with the launcher project in row 4 and live in `ProjectTests` |
| `prefab_test.dart` | `PrefabTests` | 12 | 12 | 12 | 0 | done. The three that copy an instance and the one that moves it came with the clipboard and the commands in row 2 |
| `picking_test.dart` | `PickingTests` | 9 | 9 | 9 | 0 | done |
| `euler_test.dart` | `EulerTests` | 1 | 1 | 1 | 0 | done. The loop of 200 stays inside the one test |
| `scene_test.dart` | `SceneTests` | 20 | 20 | 2 | 0 | partial. The other 18 read `toRenderScene`, which belongs to the viewport task 15 |
| `scene_weather_test.dart` | `SceneWeatherTests` | 34 | 34 | 11 | 0 | partial. The other 23 read `toRenderScene`, which belongs to the viewport task 15 |

`grid_test.dart` (9) and `selection_outline_test.dart` (7) also belong to task
15, with `scene_render.dart`.

## Row 2, commands and history

| Dart test file | C# class | Dart declared | Dart run | C# ported | Added in C# | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `history_test.dart` | `HistoryTests` | 33 | 33 | 33 | 0 | done. The workspace tests of row 3 that this file holds came with it |
| `clipboard_test.dart` | `ClipboardTests` | 26 | 26 | 26 | 0 | done. The five odd texts stay in one test, as in Dart |
| `scene_diff_command_test.dart` | `SceneDiffCommandTests` | 7 | 7 | 7 | 0 | done |
| none, Dart tests these through widgets | `ObjectCommandTests` | 0 | 0 | 0 | 23: appearance, light, link, geometry, component, time and weather commands, each applied, undone and merged where a drag merges | done |

## Row 3, the workspace

| Dart test file | C# class | Dart declared | Dart run | C# ported | Added in C# | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `scene_playback_test.dart` | `ScenePlaybackTests` | 5 | 5 | 5 | 0 | done. `ClipPreview` and `SceneCameras` came with it, and the Core now reads the Motion, Stage and Filament packages for the cutscene camera |
| `dock_test.dart` | `DockTests` | 40 | 40 | 40 | 0 | done. `PanelKind`, `DockPanel`, the dock nodes and `DockLayout` came with it. `PanelKind.Icon` is a name string where Dart held `IconData` |
| `registry_test.dart` | `RegistryTests` | 6 | 6 | 6 | 0 | done. `Throws StateError` became `InvalidOperationException`, and the unmodifiable list became a read-only collection |
| `layout_library.dart` (no Dart test) | `LayoutLibraryTests` | 0 | 0 | 0 | 7 | added. Covers save, find, remove, reopen, a damaged file and a file in the shape Dart writes |
| `panel_registry.dart` (no Dart test) | none | 0 | 0 | 0 | 0 | done. `PanelType` is data only, so `build` and `UnregisteredPanel` wait for the shell, and `showsMovement` is not ported because it only exists to skip a Flutter rebuild |

## Row 4, settings, projects and paths

| Dart test file | C# class | Dart declared | Dart run | C# ported | Added in C# | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `host_paths_test.dart` | `HostPathsTests` | 8 | 8 | 8 | 0 | done |
| `store_settings_test.dart` | `StoreSettingsTests` | 6 | 6 | 6 | 3: no temporary file left, a file in the shape Dart writes, and the text written byte for byte | done. The address rules come from `KitStoreClient.AddressProblem`, so the Core now reads the Asset package |
| `launcher_test.dart` | `ProjectTests` | 16 | 16 | 4 | 0 | partial. The four in "a project on disk" are here. The other twelve are widget tests of the projects list and the create dialog, and wait for the launcher in row 7 |
| none, the Dart has no test | `ProjectTests` | 0 | 0 | 0 | 14: create, refuse, folder names, the recents list, a Dart-shaped time and file | added |

`ProjectStore` takes the path of the recents file, where Dart asked the platform
for the application support folder. `DisplayPath` takes the home folder and
`ProjectStore` takes a `TimeProvider`, because the environment and the clock are
IO (F10). `Touched` is a `with` expression. The store is synchronous, because
nothing in it waits any more.

## Row 5, shortcuts, the palette and the key map

| Dart test file | C# class | Dart declared | Dart run | C# ported | Added in C# | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `command_shortcuts_test.dart` | `CommandShortcutsTests` | 12 | 16 | 16 | 1: Command is still seen with Shift held | done. The two widget tests hold a key in a live keyboard, so they pass the held modifiers in |
| `command_palette_test.dart` | `CommandPaletteTests` | 13 | 13 | 6 | 2: a group's title, and the entry handed back unrun | partial. The six search tests are here. The seven dialog tests are widget tests and wait for the shell in row 6 |
| `editor_shell_palette_test.dart` | none | 5 | 5 | 0 | 0 | waits for the shell in row 6 |
| none, the Dart has no test | `ShortcutMapTests` | 0 | 0 | 0 | 12: the map of keystrokes to verbs | added |

The key map moved out of `editor_shell_intents.dart` and the `_nudges` table in
`editor_shell_modelling.dart` into `ShortcutMap.For(platform)`, so it is data in
the core and a test can read it. A keystroke is a `KeyChord`, a verb is an
`EditorVerb`, and a nudge carries its signed squares. What each verb does stays
in the shell, because it changes shell state.

`HostPlatform` replaces `defaultTargetPlatform`. Every key function takes the
platform, and `HostPlatforms.Current` reads the operating system once, where the
shell is wired (F10). The palette search lower-cases one character at a time, so
a label with a capital dotted I lights up a different span from Dart.
`PaletteEntry.Icon` is a name where Dart held an `IconData`.
