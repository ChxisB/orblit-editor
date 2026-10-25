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
| `scene_document_test.dart` | `SceneDocumentTests` | 33 | 33 | 30 | 0 | partial. Three tests open a project template and wait for the launcher project in row 4 |
| `picking_test.dart` | `PickingTests` | 9 | 9 | 9 | 0 | done |
| `euler_test.dart` | `EulerTests` | 1 | 1 | 1 | 0 | done. The loop of 200 stays inside the one test |
| `scene_test.dart` | `SceneTests` | 20 | 20 | 2 | 0 | partial. The other 18 read `toRenderScene`, which belongs to the viewport task 15 |
| `scene_weather_test.dart` | `SceneWeatherTests` | 34 | 34 | 11 | 0 | partial. The other 23 read `toRenderScene`, which belongs to the viewport task 15 |

`grid_test.dart` (9) and `selection_outline_test.dart` (7) also belong to task
15, with `scene_render.dart`.
