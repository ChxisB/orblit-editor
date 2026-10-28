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
| `editor_shell_workspaces_test.dart` | `EditorSessionTests`, `EditorSessionInterfacesTests`, `EditorWindowInterfacesTests` | 16 | 16 | 10 | 3: a new interface leaving the one the scene shows, a file that is not an interface, and focusing the canvas | partial. The order of the rail, the panels for each job, reset, each remembering its arrangement and the Interface group are here. The C# top bar has no Saved status, object count or frame rate, so the top bar case checks only the name. The rest need icons, terrain, cinematics or the modelling panel |
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
because a binary asset is Chris's call. The launcher has no Examples page. The
examples live in the `orblit-examples` repository, and the editor does not show them.

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
| `clip_edits_test.dart` | `ClipEditsTests` | 15 | 15 | 15 | 0 | done. `ChannelAddress`, `KeyRef` and the edit functions are the editor's own, because the motion package keeps its address type internal |
| `clip_bench_test.dart` | `ClipBenchTests` | 13 | 13 | 13 | 3: cutscene shots and keys, the object a track belongs to and its scope, and the playhead clamp | done. The four preview tests had no C# port and sit here. `OpenDocument` replaces `openClip` and `openCutscene`, because a method named `OpenClip` hides the type |
| `timeline_test.dart` | `TimelinePanelTests`, `KeyButtonTests` | 12 | 12 | 12 | 0 | done. Ten are widget tests on the panel and two are the key button on the inspector's fields. Playing runs on a test clock. The real frame loop has not been run live |
| `editor_shell_clips_test.dart` | `EditorSessionClipsTests`, `TimelinePanelTests`, `EditorWindowRetargetTests` | 6 | 6 | 6 | 0 | done. The clip menu test is two: the menu calls `OnNew` in the panel tests, and a made clip replaces the open one in the session tests. The two retargeting tests open the dialog from the clip menu in a window over a real project, and pick the models through the dropdowns |
| `cinematics_test.dart` | `CinematicsTests` | 18 | 18 | 18 | 3: a cut that shortens the shot running, a cut that leaves an earlier shot alone, and a camera under a parent the scene lacks | done |
| `editor_shell_cinematics_test.dart` | `EditorWindowCinematicsTests` | 7 | 7 | 7 | 11: the shelf in the cinematics mode and nowhere else, the camera menu marking the camera in use and recutting a shot, a camera the scene lacks, a start or length taken only as a number within ten minutes, a typed number that changes nothing, a keystroke in a card the shots have moved on from, the cards letting go of looking, a renamed camera, changes elsewhere not flying the view back, the inspector keying the cutscene, and the playhead leaving a box being typed in alone | done |
| none | `EditorSessionCinematicsTests` | 0 | 0 | 0 | 30: the panels and layout of the mode, opening, making and refusing a cutscene, Add shot and Use this view as undo steps, looking through a camera, steering it as one step and letting go when it is deleted or its add is undone, the shelf, unsaved changes, and keying the cutscene without an owner | added |
| `ui_canvas_test.dart` | `InterfaceCanvasTests`, `InterfaceWorkspaceTests` | 20 | 20 | 20 | 5: a click that shakes does not move what it chooses, pointing at an element marks it, each panel saying what to do with nothing open, an element in a stack away from the page edge snapping to the grid on the page, and a drag the canvas leaves half way being a step of its own | done. The canvas follows a bench, so not designing is the bench previewing. Dart finds a control by its words and C# by its name |
| `ui_responsive_test.dart` | `InterfaceCanvasTests`, `InterfaceWorkspaceTests` | 16 | 16 | 16 | 0 | done. The grid count says "4 across this screen. 12 is too fine here." with a full stop where Dart has a dash |
| `interface_strings_test.dart` | `InterfaceBenchTests`, `InterfaceWorkspaceTests` | 11 | 11 | 11 | 0 | done |
| none | `InterfaceBenchTests` | 0 | 0 | 0 | 16: the keys the shown interface asks for, adding inside the selection and its undo, adding after a button rather than inside it, a place away from the last element on a stack, the grid going up for a column and not for a button, a split and a split of a placed element, a drag as one step on a whole pixel, removing and the root, typing as one step per element, saving and opening again, a file gone or not an interface, the preview held either way, an element's name, and flowing | added |
| `editor_shell_data_test.dart` | `EditorSessionInterfacesTests`, `EditorWindowInterfacesTests`, `EditorWindowDataTests`, `EditorSessionDataTests`, `EditorWindowConsoleTests` | 22 | 22 | 21 | 3: a change shown over the scene before it is saved, an interface file that cannot be read, and a data object's panel kept through a change elsewhere | partial. Waiting: a refusal on the console tab, which drops a png with nothing selected and waits with the other drops. No drop is ported, so `PutInterfaceOnScene` and `AttachData`, which are what the drops do, stand in for them. A data object is made through the browser's `MakeNew` and selected through the session. Saving after an attach is a session test. The console shares the bottom group with the project browser, so "folded under the view" is "behind the project tab" |
| `editor_shell_motion_test.dart` | `EditorSessionClipsTests` | 4 | 4 | 3 | 0 | partial. Waiting: Play in four views, which needs the game view. No row owns the game view yet |
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
| none | `EditorWindowTests` | 0 | 0 | 0 | 5: the project panel in the dock, a scene opened from it, the timeline in the Animation mode, a clip opened from the browser, and the dot on the File menu while a clip has changes | added |
| none | `TimelineModelTests` | 0 | 0 | 0 | 48: the tick, play and pause, stepping, home and end, deleting keys, typing a length, the rate, what happens at the end of each kind, a hold, slopes, removing a channel, the rows, the time axis and the words | added |
| none | `TimelinePanelTests`, `SkeletonSettingsTests` | 0 | 0 | 0 | 24: no clip open, a tap on the ruler, the keys acting only with focus and a held arrow, a playing clip moving the playhead, the play button, a typed length, the rate menu, the owner warning, a heading over its channels and named again after a rename, the channel close button, an empty clip, and the bones, onion and ghost chips on a narrow panel | added |
| none | `MarkModelTests`, `TimelineStripTests` | 0 | 0 | 0 | 23: adding, renaming, moving and deleting a mark with its prompts and menu, a flag standing where the ruler puts it, flags not made again while the playhead moves, and the shot bars of a cutscene | added |
| none | `SheetModelTests`, `DopeSheetTests` | 0 | 0 | 0 | 40: the hit with a 7 px reach, a heading that picks every channel on the moment, a box, a tap with Shift, a drag by whole frames as one step, pixel reads of the drawn keys and the playhead, and a clip with a rate that rounds to no whole frames still drawing | added |
| none | `CurveModelTests`, `CurveViewTests` | 0 | 0 | 0 | 52: the frame and range, a 6 px reach, the handles of picked keys, a tap with Shift, a drag in time and value, a bend by a handle with Alt to break it, a handle held in front of its key, the grid, a view too short or a clip with no length, the chips and pixel reads of keys, curve and playhead | added |
| none | `EditorSessionClipsTests` | 0 | 0 | 0 | 5: a file that is not a clip, a clip attached twice, a scene saved as it rests wherever the playhead is, the window told only when whether a clip has changes flips, and a clip with changes counting as unsaved | added |
| none | `KeyButtonTests` | 0 | 0 | 0 | 3: no buttons with no clip open, buttons appearing when a clip opens without the inspector being built again, and a key as one step on the undo stack | added |
| none | `MotionSectionTests` | 0 | 0 | 0 | 5: an object with no animation attaching only a clip that is open, opening a clip the object plays, On start choosing which clip starts, removing the animation unlinking and keeping the files, and the scene having none | added |
| `terrain_bench_test.dart` | `TerrainBenchTests` | 19 | 19 | 19 | 2: a drag that leaves the ground carrying on at the height it was last at, and each tool having its words and naming its stroke | done |
| `editor_shell_terrain_test.dart` | `EditorWindowTerrainTests` | 4 | 4 | 4 | 3: the shelf and the brush panel showing one brush and following each other, the ring leaving the ground with the mode, and a drag outside the terrain mode never shaping the ground | done |
| none | `EditorSessionTerrainTests` | 0 | 0 | 0 | 9: Add Terrain writing its file at once, a second terrain getting its own folder, undo leaving the file, no scene to add to, saving the ground before the scene, the unsaved flip said once each way, the brush finding the selected terrain, the mode's arrangement and only that mode working the brush. | added |
| none | `BrushRingTests` | 0 | 0 | 0 | 8: 64 pieces joined end to end, a ring following the ground, a piece behind the camera left out, and the inner ring only when it would not sit on the edge or the centre | added |
| none | `BrushPanelModelTests`, `TerrainBrushPanelTests` | 0 | 0 | 0 | 14: the words and sums behind the panel, and the terrain it names, the tool it explains, its sets and tints, and each brush setting changing the bench without a step on the undo stack | added |
| none | `TerrainSectionModelTests`, `TerrainSectionTests` | 0 | 0 | 0 | 26: the region map, the sets, the automatic cover and the blend, each change one step | added |
| none | `TerrainPicturesTests` | 0 | 0 | 0 | 8: a picture drawing plain until it is read and then arriving as pixels, one picture named twice read once and not again when the sets change, a picture that cannot be read said once with the ground left plain and tried again when the sets change, a terrain read from disk asking for its pictures, undo drawing the earlier pictures at once, the real decoder giving the size the renderer takes, and no decoder meaning no asking | added |
| none | `SkiaPictureDecoderTests` | 0 | 0 | 0 | 10: a picture of the right size coming back byte for byte with straight alpha, a stretch from 4 by 4, 4 by 2, 64 by 64 and two odd sides, a JPEG of 16, 64 and 100 pixels coming back opaque, and bytes that are not a picture being refused | added |
| `mesh_edit_test.dart` | `ElementSelectionTests`, `MeshPickerTests` | 16 | 16 | 16 | 8: a point that has gone skipped, clearing, the order things were picked in kept, a marquee over a shape taking only what can be seen, seeing through taking the far side, an edge taken whole or not at all, a face taken by its middle, and a box holding its top left and not its far edges | done. `ElementPainter` is the view and comes with part 6f. `MeshPicker` holds the mesh's points in the world from its first question, where Dart moved each point again for every ray |
| `model_bounds_test.dart` | `ModelBoundsTests` | 3 | 3 | 3 | 2: a broken `.glb` and `.gltf` saying nothing, and a miss or an answer kept until forgotten or cleared | done |
| `mesh_tools_test.dart` | `MeshToolsTests` | 16 | 16 | 16 | 3: a bridge winding from the first edge picked and ignoring a third, a bridge with fewer than two edges doing nothing, and every action having words, an icon and its own name | done. The icon names are drawn by the panel in part 6f |
| `mesh_handles_test.dart` | `ElementDragTests` | 12 | 12 | 12 | 13: a drag working from a copy and never changing the mesh it was given, a turn spinning the corners about their own centre, shift while turning not extruding, the handles sitting on the selection, a drag in a turned object moving along the world axis, a box in vertex and edge mode, the box being there while dragging and gone after, a tap on a face, on a vertex and on nothing, hovering and leaving, an empty drag outside editing turning the view while a tap picks objects, and a shape with nothing in it having nothing to edit | done. Events replace Dart's object list. The drag readout is not shown for parts |
| `drawing_test.dart` | `SketchTests`, `DrawingSessionTests`, `DrawingInputTests`, `EditorWindowTests` | 20 | 20 | 20 | 28: a plane kept as its own copy, starting again dropping what was there, taking back a point keeping the plane while others remain, a click on nothing landing on the ground and a click on a face taking its plane, a cut starting only on a face, the line following the pointer and not asking for a draw when it has not moved, a drag while drawing not turning the view, a cut following the object to where it stands, a cut needing two points, the parts needing a shape, and escape leaving the geometry only after the drawing is given up | done. Enter is covered in the window test. The drawing painter and the tool buttons are part 6f |
| `uv_editor_test.dart` | `UvSessionTests`, `UvPanelTests`, `ShapeExportTests` | 9 | 9 | 9 | 38: the view framing the faces with a quarter to spare, halfway to a face far from the square and at a fixed size with nothing to frame, a point and its coordinate being each other's inverse with up the view being up in V, a span of nothing refused, a drag moving by the pointer's travel since the last frame and scaling and turning by what it swept about the middle, scaling from the middle itself dividing by nothing, scaling and turning folding into one step, releasing going back to the rule, a slider being one step and two clicks two, nothing happening with no faces or in the wrong mode, parts starting in face mode, no mesh without a shape, freezing from the panel dropping the rule numbers, a fill choice changing the face, a gesture choice changing what a drag does, a drag in the square being one step to undo, and the panel opening from the dock, an export of each format writing a file over 80 bytes, an OBJ with materials bringing its library and saying both names, a name with a path refused, nothing to export said, a folder that cannot be made said with its reason, the format choice with a line on what it keeps, the prompt starting with the shape's name, and a cancelled or empty name writing nothing | done. The buttons are text only, with no icons |
| `modelling_panel_test.dart` | `ModellingPanelTests`, `ModellingSessionTests` | 6 | 6 | 6 | 16: a selected object that is not a shape being named, the shelves following what is selected, snapping being one toggle and a choice that follows a change made elsewhere, an action running from its button and undoing as one step, an amount being a slider under its button, a material listing and painting, no paint button with nothing to paint, a material saying how many faces wear it, an action on the whole shape needing no selection, an action doing nothing without a shape, an amount from what was set or what it starts with, see through being said to the views, painting with nothing selected changing nothing, and a slider run on a material being one step | done. The buttons are text only, with no icons |
| `nudge_test.dart` | `NudgeTests`, `EditorWindowKeysTests` | 6 | 6 | 6 | 11: a nudge moving every selected object together as one step, scene settings having no place to move, the shift being the same inside a turned parent, the bracket keys changing the grid and switching it on, G going into the geometry and escape backing out, and keys left to a focused text box | done. The distance is the window's grid step, so the key tests press keys and the session tests pass metres. With nothing focused, Avalonia spends the first arrow on focus navigation, so the key tests click the view first |
| `editor_shell_geometry_test.dart` | `EditorSessionObjectsTests`, `ShapeSectionTests`, `ModellingPanelTests`, `ModellingSessionTests`, `EditorWindowKeysTests`, `GeometryStoreTests`, `MeshToolsTests` | 13 | 13 | 13 | 0 | done. The C# cases sit in classes that the rows for the files they were written beside already count. A shape being written out for the renderer is `GeometryStoreTests`, and G is `EditorWindowKeysTests` |
| `blend_model_test.dart` | `BlendModelTests` | 39 | 39 | 39 | 9: a name no state has, what a kind is made of and reads, a number with no trailing zeros, a condition that is nothing nesting too, a through keeping its laps only from a through, a source naming its first clip, renaming a graph state renaming its leaves' changes, a state that is not there not being changed, and moving a change of a blend with none | done |
| `blend_bench_test.dart` | `BlendBenchTests` | 10 | 10 | 10 | 7: nothing changing with no blend shown, a run in another blend not tied to this one, a chosen change cleared when its index is gone, choosing the same thing twice telling nobody twice, showing another blend clearing the choice, closing the shown blend showing the last one open, and what could not be written being said | done |
| `blend_inspector_test.dart` | `BlendInspectorTests` | 6 | 6 | 6 | 25: a state with no other state, deleting a change, an input added by name, a state added by clip, starting in another state, a speed below nought, a number that is not finite, an edit sealed when the panel leaves, sync marks, what happens at the end, a clip becoming a mix, a plane left to the file, points of a line, the condition of a change and its kind menu, its number, its input, a group of tests and a part of one, a fade, a change from any state, the changes of a state listed, the clip box of each point and the clip box of a moved point following it | done |
| `editor_shell_blends_test.dart` | `EditorWindowBlendsTests` | 7 | 7 | 7 | 0 | done |
| `retarget_model_test.dart` | `RetargetModelTests` | 9 | 9 | 8 | 1: a file whose name starts with a dot keeping it whole | done. The draft test is not ported, because the C# draft is a record and `with` is the language's |
| `retarget_files_test.dart` | `RetargetFilesTests`, `AssetsTests` | 7 | 7 | 7 | 1: a save that cannot make its folder says why | done. The bone map kind is one row of the kind theory in `AssetsTests` |
| `retarget_dialog_test.dart` | `RetargetDialogTests` | 16 | 16 | 16 | 15: the models listed, a bone given back to its name, a map's unreadable things counted, the two switches, the title, four stale bones named, one skeleton read once per model, what pairing by body could not do, a target kept, the model named when neither has a skeleton, the pairs hidden again, the choices of a bone, a menu opened twice, and a choice made after the click has returned | done. The dropdowns are `MenuFlyout`s, which open headless. The choice is posted, which headless cannot show is needed |
| none | `BlendCanvasTests`, `BlendSketchTests` | 0 | 0 | 0 | 32: a box for each state and the flag on the start, the arrow labels and the shared ones, a label centred on its arrow or hung under a loop, the grid and the size of the sketch, the arrows that go forward and back, a change to a state that is not there left off, a state found under a spot, pressing a state, a gap or an arrow and stepping along its changes, a link dragged onto a state, onto its own or onto nothing, one that moves only a hair, the box under a drag marked, the chosen arrow in ember, the canvas following edits made elsewhere, and two state names that differ only in punctuation | added |
| none | `BlendGraphPanelTests`, `EditorSessionBlendsTests` | 0 | 0 | 0 | 17: no blend open, the graph with the fields beside it or under it, the tabs and their close and plus, the animation mode, a file that is not a blend or not there, what could not be read, the clip names offered, the mark until saved, a graph saving itself, and a blend that cannot be written | added |
| none | `ModelSkeletonsTests` | 0 | 0 | 0 | 7: the bones of a model in file order, a rooted path, a file with no skin or no model, the skin with the most bones, a tie, a glTF with or without its buffer beside it, and a file that changes read again | added |
| none | `EditorSessionClipsTests`, `EditorWindowRetargetTests` | 0 | 0 | 0 | 10: a made clip written in `clips/` and shown, never over another, its problems counted, put on the chosen object only for the model it moves, the model of the clip's owner and not of the chosen object, a clips folder that cannot be made, a clip that cannot be written, and the dialog starting on the owner's model | added |
| `console_test.dart` | `EditorLogTests` | 12 | 12 | 11 | 2: an entry stamped by the clock, and what the session says being kept | done. "What Flutter throws" is what the UI thread throws. Giving the handler back and nothing arriving after it are one C# test. The closed log test is not ported, because a C# event has nothing to throw |
| none | `ConsolePanelTests` | 0 | 0 | 0 | 7: each filter's count, the newest at the top, a repeat's count and time, opening an entry's detail, an open entry kept through a new one, Copy oldest first, and letting the log go on close | added |
| `data_object_test.dart` | `DataObjectTests` | 16 | 16 | 16 | 2: the TypeScript and the C++ checked against the text Dart writes | done |
| none | `EditorSessionDataTests` | 0 | 0 | 0 | 11: the session side of the data objects group, a scene row putting the inspector back, attaching with nothing selected, a file that is not a data object, detaching, and a file that has gone | added |
| none | `DataPanelTests` | 0 | 0 | 0 | 11: a typed value waiting for the typing to stop, text that is not a number, an empty key, a value added of each kind with a name of its own, one axis of a vector, removing, what is waiting written on close, the bindings of what was just typed, and a file that cannot be read | added |
| `profile_panel_test.dart` | `ProfilePanelTests` | 5 | 5 | 4 | 2: closing stops the reads, and the window's panel times the frames the scene view draws | done. The clock is wound by hand. Handing a shown panel another profiler is not ported, because the window builds the panel once over its own |
| `cpp_binding_test.dart` | `ScriptBuilderTests`, `DataObjectTests`, `AssetsTests` | 14 | 14 | 14 | 0 | done. The nine that compile real C++ are `ScriptBuilderTests`. The four that read the header's text are the C++ golden in `DataObjectTests`. Which files offer a build is a theory in `AssetsTests` |
| none | `CodeEditorTests` | 0 | 0 | 0 | 17: the first editor on the path, an app with no command on a Mac, looking once, `where` on Windows, a file opened in its folder, a file given as a path to an editor without `--goto`, a folder alone, opening by bundle, handing on to the next, no editor, a folder that has gone, revealing on each desktop, and what cannot be revealed | added |
| none | `EditorWindowCodeTests` | 0 | 0 | 0 | 9: the File menu's labels, opening the project, Show in Finder, no editor, a script opened from Project, the palette, a build that fails, a window closed during a build, and a build with warnings | added |

Gap in row 5: a picture is shown in a set and can be cleared, but not dropped on one. The Dart section took a drop from the
asset browser, and the C# browser has no drag source yet. The decoder (part 5e) reads a picture with SkiaSharp 3.119.4,
off the UI thread, at 512 by 512 with straight alpha. It ran headless, and the real one only inside the bench test. The
window runs headless only. The ring, the shelf, a stroke and a textured set have not been seen in a real window with the
renderer.

Not in the inspector yet, and not counted: `editor_shell_prefabs_test.dart` (23)
for row 13, because it needs the shell. The inspector takes its prefab buttons,
the model reader and the size of a mesh from `InspectorLinks`, which the window
does not fill yet. `rebuild_scope_test.dart`
(5) tests how Flutter rebuilds, and is judged again with the shell.

Gaps in row 9: the Dart scene view and game view draw a plain interface over an opaque
ground with dark bars, which hides the scene. The C# scene view draws it with neither.
This is item 59 of decision 80. It draws under the boundary boxes, where Dart draws it over them.
The handles are on top in both. The Interface chip is a setting of each scene view, as the
outline chip is, where Dart keeps one for the shell. The Interface workspace has run
headless only, never in a real window with the renderer.

The row 9 review found five more, items 60 to 64 of decision 80. Two are fixed in C#.
With a button or text selected, Add puts the new element after it, where Dart puts it
inside. A drag in a stack away from the page edge snaps to the grid on the page, where
Dart snaps it from the stack's edge. Three are left as Dart has them. Only Delete acts
on the interface selection, and Cut, Copy, Paste, Duplicate and the arrows act on the
scene selection behind it. The scene view keeps an interface it has read until the
project opens again. New asks whether the loaded scene shows an interface, then puts
the canvas on the working one.

Gaps in row 10: a png dropped with nothing selected is not ported, and waits
with the other drops. A new data object's kind is picked from a menu, where Dart
opens a dialog. The store learns of an edit only when it is written, and the
panel writes what is still being typed before the bindings. Selecting a scene
row puts the inspector back. A number that is not finite is refused. The
profile panel counts only the frames the scene view draws, and times no spans.
A gauge reads "12" where Dart reads "12.0". Two faults are left as Dart has
them, items 65 and 66 of decision 80. The C++ header names its data object
without the folder, and every script built on a Mac with ld-27037 or later
has warnings, so there is no test of a clean build. The review adds items 67
to 71. C# passes `--goto` only to the VS Code family. The Orblit.Native package
does not copy the engine headers it carries, so the editor's project copies
them itself. The console, the data panel, the profile panel, the code editor
and the build have run headless only, never in a real window.

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
