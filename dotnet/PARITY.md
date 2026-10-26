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
| `scene_test.dart` | `SceneTests` | 20 | 20 | 20 | 0 | done. The 18 that read `toRenderScene` came with the viewport task 15 |
| `scene_weather_test.dart` | `SceneWeatherTests` | 34 | 34 | 34 | 0 | done. The 23 that read `toRenderScene` came with the viewport task 15 |

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
| `command_palette_test.dart` | `CommandPaletteTests`, `PaletteDialogTests` | 13 | 13 | 13 | 6: a group's title, the entry handed back unrun, the lit span of a label, one heading for each group, the keys and meta beside a row, and the chosen row scrolled into view | done. The six search tests are in `CommandPaletteTests` and the seven dialog tests in `PaletteDialogTests` |
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
| none, the Dart has no test | `EditorSessionTests`, `EditorWindowTests` | 0 | 0 | 0 | 52: the session without a window, the window reading and writing it, and the inspector in the dock. Save as, its prompt and the quiet shared save are 13 of them | added |

`editor_shell_test.dart` (15) is not counted. Most of it reads the outliner, the
inspector and the project browser. Its save, unsaved marker and refused-newer-file
tests have behaviour twins in `EditorSessionTests` and `ProjectWorkspaceTests`.
Selecting an object into the inspector and dragging a transform number are in
`InspectorPanelTests`. The project browser is not ported.

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
| none, the Dart kept this in widgets | `LauncherSessionTests` | 0 | 0 | 0 | 26: the view, the template, the search, open, forget and create, with no control, and a fault in the opener not told as a folder that could not be written | added |

The launcher session holds the view, the template, the search and the rules for
opening and creating, so a test reaches them with no widget. The splash is drawn
in code and covers the launcher only. The 622 KB logo picture is not bundled,
because a binary asset is Chris's call. There is no Examples page until task 17.

## Row 8, the viewport

| Dart test file | C# class | Dart declared | Dart run | C# ported | Added in C# | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `orbit_camera_test.dart` | `OrbitCameraTests` | 8 | 8 | 8 | 0 | done |
| `fly_camera_test.dart` | `FlyCameraTests` | 10 | 10 | 10 | 0 | done |
| `gizmo_test.dart` | `GizmoTests` | 19 | 19 | 19 | 0 | done |
| `viewport_input_test.dart` | `ViewportInputTests` | 16 | 16 | 16 | 0 | done. The eight widget tests drive the stages and the gizmo types directly, with no control |
| `selection_outline_test.dart` | `SelectionOutlineTests` | 7 | 7 | 7 | 0 | done. The engine keeps the packed keys internal, so the Dart check of them became a check of `Primary` |
| `snapping_test.dart` | `SnappingTests` | 15 | 15 | 15 | 0 | done |
| `grid_test.dart` | `GridTests` | 9 | 9 | 9 | 1: the tile reads back as the lines the grid draws | done. The tile is encoded by `Orblit.Sprite`, so its bytes differ from Flutter's |
| `convex_outline_test.dart` | `ConvexOutlineTests` | 9 | 9 | 9 | 0 | done |
| `joint_test.dart` | `JointTests`, `JointModelTests`, `InspectorJointTests` | 18 | 18 | 18 | 0 | done. The four on what a joint holds and the nine of the gizmo are in `JointTests`. The two on the scene file are in `JointModelTests`. The section and the add and undo are in `InspectorJointTests` |
| `body_test.dart` | `BodyTests` | 19 | 19 | 19 | 0 | done |
| `sound_test.dart` | `SoundTests`, `ComponentModelTests`, `InspectorComponentTests` | 13 | 13 | 13 | 0 | done. The gizmo and the scene file are in `SoundTests`. The four on the range are on the model. The section and the five row tests are headless |
| `editor_shell_viewport_test.dart` | `EditorShellViewportTests` | 17 | 17 | 14 | 0 | partial. Flying, the wheel speed and the two finger gestures are ported. The three camera preview tests need the outliner, the inspector and the preview window, so they wait for task 16. Avalonia has no scroll phases, so the control does not turn a two finger scroll into an orbit or a pan. The four two finger tests send the gesture to the state |
| `viewport_tools_test.dart` | `ViewportToolsTests` | 6 | 6 | 6 | 0 | done. Five Dart cases come from a `drawing` helper and one is a plain test |
| none, the Dart view is one widget | `SceneViewportStateTests` | 0 | 0 | 0 | 15: flight, the wheel, looking, trackpad gestures, a drag on a ring, the grid switch and the clock, on the state that the control holds | added |
| none | `SceneViewportTests` | 0 | 0 | 0 | 8: a drag orbits, a tap does not, a resize, the snap chip, a key let go under a command key, a key held part way through a drag and the same view across a change, headless | added |
| none | `ViewportChipsTests` | 0 | 0 | 0 | 14 cases in 3 tests: the chips the control shows and the grid label | added |
| none | `DockHostTests` | 0 | 0 | 0 | 2: a layout with the same text is not drawn again, and one with other text is | added |
| none | `GeometryStoreTests` | 0 | 0 | 0 | 8: a shape is written inside the project for the renderer, when it is stale and when it is forgotten | added |
| `tool/parity/pick_test.dart` (an exporter, not a test) | `DartPickTests` | 0 | 0 | 0 | 4: three cameras and a grid of pixels each, the same ray, the same object and the same handle as Dart, and a check that the grid is not empty | added |

The air and the sky in `SceneAir` use FNV-1a keys for materials and surfaces,
where the Dart uses `Object.hash`. The key values are not part of the parity.
The rain and snow colour is a double-precision lerp of the two sRGB tints.

## Task 16, the panels

| Dart test file | C# class | Dart declared | Dart run | C# ported | Added in C# | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `editor_shell_hierarchy_test.dart` | `OutlinerModelTests`, `OutlinerPanelTests` | 8 | 8 | 8 | 0 | done. The six filter tests are on the model. The two on the plus button are on the window |
| `editor_shell_clipboard_test.dart` | `EditorSessionObjectsTests`, `OutlinerPanelTests`, `EditorSessionScenesTests` | 15 | 15 | 15 | 0 | done. The four that load a scene are session tests: copy between scenes, the clipboard surviving an unload, several into another scene, and a paste landing where it was |
| `editor_shell_scenes_test.dart` | `EditorSessionScenesTests`, `OutlinerPanelTests`, `InspectorSceneTests` | 17 | 17 | 14 | 0 | partial. Waiting: the two save as tests, which have twins in `EditorSessionTests` under other names (row 13), and the dropped mesh, which waits on dragging a file out of the browser. Selecting the scene, its saved settings and an unloaded scene offering to load are in `InspectorSceneTests`. The three drag tests call `Release`, as the hierarchy ones do |
| `inspector_sections_test.dart` | `InspectorPanelTests` | 6 | 6 | 6 | 0 | done |
| `editor_shell_body_test.dart` | `InspectorBodyTests`, `InspectorCompoundTests` | 8 | 8 | 8 | 0 | done. The model triangles and the compound parts are in `InspectorCompoundTests` |
| `editor_shell_body_controls_test.dart` | `InspectorBodyTests`, `InspectorSceneTests` | 7 | 7 | 7 | 0 | done. Naming a layer is on the scene's own fields, in `InspectorSceneTests` |
| `editor_shell_zone_test.dart` | `InspectorBodyTests`, `InspectorComponentTests` | 9 | 9 | 9 | 0 | done. The trigger undo is the last check of the place test, so four trigger tests are three in C# |
| `editor_shell_joint_test.dart` | `InspectorJointTests` | 5 | 5 | 5 | 0 | done |
| `zone_test.dart` | `ZoneTests` | 15 | 15 | 15 | 0 | done |
| `particles_test.dart` | `ParticlesTests`, `ComponentModelTests`, `InspectorComponentTests` | 10 | 10 | 10 | 0 | done. The file is in `ParticlesTests`. The four on ranges are on the model. Choosing what is already chosen is one C# test for this row and the sound one. The section and the other three rows are headless |
| `vehicle_test.dart` | `VehicleTests`, `InspectorComponentTests` | 7 | 7 | 7 | 0 | done. The file and the two edits are in `VehicleTests` |
| `collision_model_test.dart` | `ModelCollisionTests` | 2 | 2 | 2 | 0 | done |
| `assets_test.dart` | `AssetsTests` | 43 | 43 | 43 | 0 | done. The watch tests wait on the folder watcher and the settle timer, as Dart's wait on the stream |
| `cook_status_test.dart` | `CookStatusTests` | 16 | 16 | 16 | 0 | done. Each cooks for real over a temp project, with a cache of its own |
| `shader_test.dart` | `ShaderTests` | 17 | 15 | 17 | 0 | done. The two that compile with a real `matc` are skipped unless `ORBLIT_MATC` is set, in Dart as here. The five section tests are headless, and the colour is the editor's swatch row, not a free colour row |
| `asset_browser_test.dart` | `AssetBrowserTests` | 16 | 16 | 16 | 0 | done. The texture, the marks, the header line, the tooltip and the filter are headless over a real folder |
| `asset_preview_test.dart` | `AssetPreviewModelTests`, `AssetPreviewTests` | 10 | 10 | 10 | 0 | done. The four on framing, the fbx and what it can draw are on the model. The picture, the platform message, the kind, the shut preview and the short panel are on the window. "Draws" is the preview's stage, and the picture is a decoded copy, not the file |
| none | `OutlinerModelTests` | 0 | 0 | 0 | 15: the rows and their depth, the folds, a scene row with a filter, an unloaded scene, where a drop lands and what a drop refuses | added |
| none | `EditorSessionObjectsTests` | 0 | 0 | 0 | 26: selecting, adding and naming, ids, deleting, moving, duplicating, the clipboard labels and a clipboard that cannot be reached | added |
| none | `OutlinerPanelTests` | 0 | 0 | 0 | 18: the rows and the count, the selection drawn, a scene row, the filter message, a fold, the remove button, the edit keys with and without a text box, the Add menu, the Edit menu waiting for a selection and a drop, how an unloaded scene says to load it, the mark on a scene never written, a click that only selects, discarding in the leave dialog, the hover close button, no close on the shared row, and New scene from the menu and from the key | added |
| none | `EditorSessionScenesTests` | 0 | 0 | 0 | 17: loading, what a load clears, a scene with no file, a file that is gone or unreadable, prefabs named before a read, the default answer, Stop, Save and Discard, saving with no file, new scenes and their names, closing, and the selected scene row | added |
| none | `JointModelTests` | 0 | 0 | 0 | 17: the kinds, the seed a pulley or a path starts with, what a joint holds, the modes of a limit, an ordered range, the length a rope keeps, the labels, which ways give, driving, the next point of a path and a point set | added |
| none | `BodyPartsTests`, `BodyFitTests` | 0 | 0 | 0 | 23: the shapes of a part, the hull a part starts as, parts added and removed, turns read back as degrees, the collider, and what a body is fitted to | added |
| none | `ModelCollisionTests` | 0 | 0 | 0 | 2: a shape the object builds itself needs no file, and an object with nothing to read says so | added |
| none | `InspectorModelTests`, `InspectorWeatherTests`, `LightModelTests`, `WeatherModelTests`, `SceneFieldsModelTests`, `FieldsTests` | 0 | 0 | 0 | 53: the inspector's target, the sections it offers, the fields and their drags, the light, weather and scene settings | added |
| none | `InspectorPanelTests` | 0 | 0 | 0 | 26: no scene, the transform and its drag, renaming, the visible switch, a hidden parent, several selected, the renderer and the light, the prefab band, data links, a canvas, the folds, the header, the axis letters and a panel that leaves mid edit | added |
| none | `ComponentModelTests` | 0 | 0 | 0 | 3: the names of the emitter shapes, what a zone takes from its body and what it gives back | added |
| none | `InspectorSceneTests` | 0 | 0 | 0 | 8: the scene header, its sky, the day cycle, a few layer names, the counts and the shared scene | added |
| none | `InspectorBodyTests`, `InspectorCompoundTests`, `InspectorComponentTests`, `InspectorJointTests` | 0 | 0 | 0 | 17: what a body is offered on, a place in the tree, a fit to an editor size, a mesh body, the collider as its own step, a wheel with no car, the steer lock, a zone a file gave to a free body, and a joint that holds nothing | added |
| none | `CookStatusTests` | 0 | 0 | 0 | 3: ready before an ask and while asking, a path with dots in it, and a listener that throws leaving it usable | added |
| none | `PaletteEntriesTests` | 0 | 0 | 0 | 5: the Edit and Add entries and New scene | added |
| none | `AssetsTests` | 0 | 0 | 0 | 27: a write under a dot folder not being news, a written name that is a path being refused, the last listener stopping the watch, nothing written outside the project, a slash in a name, a rename that changes only the case, a folder renamed to its own name in another case, a header that is kept, a link left out, the data object's text, the size label, the kind of a name and which files build | added |
| none | `ShaderTests` | 0 | 0 | 0 | 3: a project with no shader is not cooked, without a compiler the cook reports a problem against the shader and does not throw, and a folder that will not open leaves the last cook standing | added |
| none | `AssetBrowserTests` | 0 | 0 | 0 | 20: a file that is not a picture, an empty folder, a click, opening a folder and going up, refresh, a mark that follows the cook, making a file and a folder, a name that is a path, deleting a file and one that is gone, renaming and renaming over another, a narrow and a wide panel, the grid's columns, the folder tree, the menu on a file and on empty space, and which files offer Build | added |
| none | `AssetBrowserModelTests` | 0 | 0 | 0 | 21: the top of the project, opening and going up, the filter, which files are shown as pictures, the count, the tip, and the cook line's words and colour | added |
| none | `AssetPreviewModelTests`, `AssetPreviewTests` | 0 | 0 | 0 | 20: a file that cannot be read, a tiny model, the camera's target and lens, a folder and nothing picked, no renderer, a picture where there is none, the words and facts for each kind, the lights, the turn and a drag that stops it, the drag, pitch and zoom limits, choosing another file, a picture that will not decode, the turn button and a file with nothing to turn, a narrow panel, and a model handed to a renderer | added |
| none | `EditorWindowTests` | 0 | 0 | 0 | 2: the project panel in the dock, and a scene opened from it | added |

Not in the inspector yet, and not counted: `editor_shell_terrain_test.dart` (4) for row 5, `blend_inspector_test.dart` (6) and
`editor_shell_blends_test.dart` (7) for row 7, and `editor_shell_prefabs_test.dart` (23) and
`editor_shell_data_test.dart` (22) for row 13, because they need the shell. The
inspector takes its prefab and data buttons, the model reader and the size of a
mesh from `InspectorLinks`, which the window does not fill yet. `rebuild_scope_test.dart`
(5) tests how Flutter rebuilds, and is judged again with the shell.

The Dart outliner test file is `editor_shell_hierarchy_test.dart`. There is no
`outliner_test.dart`. The drag itself is not driven headless. The tests call
`Release`, which the drop handler calls, so the drop logic is covered and the
pointer gesture is not.

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
