# orblit-editor

The [Orblit](https://github.com/ChxisB/orblit) editor.

```sh
flutter run -d macos
```

It opens a launcher rather than an empty editor. An editor with no project has
nothing honest to show: every panel would be an empty state, and a screen full
of empty states is worse than a screen that asks one question.

## What works

- **Launcher.** Template cards to start from, and recent projects with their
  paths and when they were last opened. A project whose folder has moved is
  marked rather than hidden, and you can open a folder that already holds a
  project.
- **New project.** Name, location and a template, with the folder path shown
  before it is created, so nobody is surprised by where their project went.
- **Editor shell.** A top bar, a workspace rail, and panels for the outliner,
  viewport, inspector and project. Cmd+K searches commands and objects. The
  play controls sit in the scene view's header.
- **Workspaces.** Scene, Modelling, Terrain, Animation and Interface each
  have their own panels. View saves named layouts, resets the current
  workspace and focuses the view. Stats reveals performance numbers.
- **Animation.** Select an object and use Animation's Make a clip button.
  The Inspector assigns clips and chooses which starts automatically.
  Play, Pause and Stop preview scene properties without editing the scene.

The 3D viewport draws on macOS. The animation preview does not run scripts,
physics or skeletal animation. Games wire their own clip players.

## Design

One accent, spent deliberately. Everything that is merely present is grey. The
ember is kept for what is selected, what is primary, and what is being edited
right now, because an interface where everything is highlighted has highlighted
nothing. Spacing is on a four-point scale, because editors go wrong when each
panel picks its own padding.

`lib/src/theme/orblit_theme.dart` is the whole system.

## Licence

MPL-2.0, © 2026 Chris Beckett. That is the Mozilla Public License, and it is
open source. Use it, fork it and ship games with it, including commercial ones.
Your game stays yours, and the licence does not reach into it. What it asks is
that changes to this repository's own files ship under the same licence, so
engine work stays in the open.

The editor links the renderer, so builds carry Filament's Apache 2.0 licence
too. See [LICENSE](LICENSE).
