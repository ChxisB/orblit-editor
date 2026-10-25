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
| `clipboard_test.dart` | `ClipboardTests` | 26 | 26 | 26 | 2: a world that is not numbers, and a world for an object that is not there | done. The five odd texts stay in one test, as in Dart. Both added tests are faults Dart has, fixed in C# only because the system clipboard is anybody's to write |
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
| `launcher_test.dart` | `ProjectTests`, `LauncherWindowTests` | 16 | 16 | 16 | 0 | done. The four in "a project on disk" are in `ProjectTests`. The twelve widget tests of the projects list and the create form are in `LauncherWindowTests`, row 7 |
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
| `command_palette_test.dart` | `CommandPaletteTests`, `PaletteDialogTests` | 13 | 13 | 13 | 5: a group's title, the entry handed back unrun, the lit span of a label, one heading for each group, and the keys and meta beside a row | done. The six search tests are in `CommandPaletteTests` and the seven dialog tests in `PaletteDialogTests` |
| `editor_shell_palette_test.dart` | `EditorWindowTests` | 5 | 5 | 4 | 0 | partial, counted in row 6 |
| none, the Dart builds the entries inside the shell widget | `PaletteEntriesTests` | 0 | 0 | 0 | 7: every mode by name, undo and redo with their labels, the keys the host uses and Save as, an object selected, an object's parent, the focus entry | added |
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

The dialog gives back the entry it was asked for and the window runs it once the
dialog has closed, as Dart does. A row is chosen by a tap, which is a press and
a release on the same row. `PaletteEntries.For` lists the commands the session
can do so far. Add, delete, copy and frame join them with their panels in task
16 and the viewport in task 15.

## Row 6, the editor shell

| Dart test file | C# class | Dart declared | Dart run | C# ported | Added in C# | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `editor_shell_palette_test.dart` | `EditorWindowTests` | 5 | 5 | 4 | 0 | partial. "Go to terrain" became "go to modelling", because terrain joins the modes in task 16. "A command that changes the scene can be undone" waits for the add commands in task 16 |
| `editor_shell_layouts_test.dart` | `EditorSessionTests` | 3 | 3 | 2 | 0 | partial. Per-mode arrangements and focus are here. "Performance numbers are behind Stats" is tested only as the toggle, because the numbers are drawn by the viewport in task 15 |
| `editor_shell_workspaces_test.dart` | `EditorSessionTests` | 16 | 16 | 4 | 0 | partial. The order of the rail, the panels for each job, reset and each remembering its arrangement are here. The rest need icons, terrain, cinematics, interface or the modelling panel |
| `editor_shell_panels_test.dart` | `EditorSessionTests`, `EditorWindowTests` | 11 | 11 | 5 | 0 | partial. Closing and opening a panel, remembering, locking, a damaged layout and a layout from before workspaces are here. The rest need the viewport, the game view or the profile panel |
| none, the Dart has no test | `EditorSessionTests`, `EditorWindowTests` | 0 | 0 | 0 | 51: the session without a window, and the window reading and writing it. Save as, its prompt and the quiet shared save are 13 of them | added |

`editor_shell_test.dart` (15) is not counted. Most of it reads the outliner, the
inspector and the project browser, which are task 16. Its save, unsaved marker
and refused-newer-file tests have behaviour twins in `EditorSessionTests` and
`ProjectWorkspaceTests`, and are left out of the count until the panels join
them.

`EditorSession` holds what the Dart `EditorShell` kept in widget state, so a
test can drive the editor with no window. `EditorWindow` draws it. The dock is a
small host of our own, which reads and writes the same layout JSON as Dart.
The window acts only on Undo, Redo, Save, Save as and Palette, and lets every other key
pass. Panels show their name until they are ported. The rail and the tabs are
words, because no icon set is bundled yet (decision 84).

## Row 7, the launcher

| Dart test file | C# class | Dart declared | Dart run | C# ported | Added in C# | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `launcher_test.dart` | `LauncherWindowTests`, `ProjectTests` | 16 | 16 | 16 | 0 | done. Four are in `ProjectTests`, counted in row 4 |
| `launcher_view_test.dart` | `LauncherWindowTests` | 4 | 4 | 4 | 0 | done |
| none, the Dart has no test | `LauncherWindowTests` | 0 | 0 | 0 | 29: the splash, the complaint, the grid, keyboard reach for the forget button, defaults, and the create and browse flow | added |
| none, the Dart kept this in widgets | `LauncherSessionTests` | 0 | 0 | 0 | 25: the view, the template, the search, open, forget and create, with no control | added |

The launcher session holds the view, the template, the search and the rules for
opening and creating, so a test reaches them with no widget. The splash is drawn
in code and covers the launcher only. The 622 KB logo picture is not bundled,
because a binary asset is Chris's call. There is no Examples page until task 17.

## Files the Dart editor wrote

| Dart test file | C# class | Dart declared | Dart run | C# ported | Added in C# | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `tool/parity/dart_files_test.dart` (an exporter, not a test) | `DartFilesTests` | 0 | 0 | 0 | 10: a project file, three template scenes, four layouts, the layouts the code builds, and a layout library | added |

The exporter writes the files the Dart editor saves into
`dotnet/Orblit.Editor.Tests/Fixtures/dart`. Each C# test reads one and writes
it again, and the two texts must be equal byte for byte. That proves a Dart
project opens in the C# editor and, because the C# text is the Dart text, the
reverse. The reverse was not run in the Dart editor.

To rebuild the fixtures, run this from the Dart `orblit-editor` package, where
the packages are resolved:

```
ORBLIT_PARITY_OUT=<worktree>/dotnet/Orblit.Editor.Tests/Fixtures/dart \
  flutter test <worktree>/tool/parity/dart_files_test.dart
```
