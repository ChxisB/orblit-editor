# Changelog

## Unreleased

- Add a C# port of the editor shell in `dotnet/`. `Orblit.Editor.Core` 0.1.0
  holds the scene document, commands and history, the dock layout, projects,
  the key map and the palette, with no widget. `Orblit.Editor` 0.1.0 is the
  Avalonia app: a window, a dock host of our own, the command palette and
  the launcher. The Dart editor stays until the migration ends.
- Remove the Examples page from the launcher, in both editors. The worked
  examples are in the `orblit-examples` repository, and the editor no longer
  depends on `orblit_examples`.
- Add `Orblit.Editor.Viewport` 0.1.0, the viewport's arithmetic with no
  widget: the orbit and fly cameras, the view ray and projection, the gizmo
  maths, the input order, the selection outline, snapping, the grid, and the
  scene as the renderer takes it, with its sky, fog, rain and cloud, and the
  joint, body and sound gizmos as lines in the view.
- In the C# library a `GizmoType` returns strokes and a tone, where the Dart
  one returns a widget. A stroke is a set of lines in the view and a tone is
  a name for a theme colour. The window will paint them, so the line maths
  can be tested without a control. The skeleton and limb gizmos are not in
  this package, because they need the animation panel.
- The C# editor draws the scene view. A `SceneViewport` holds the renderer
  surface from `Orblit.Filament.Avalonia`, the gizmo overlay, the tool strip
  and the chips. It orbits, flies, picks, drags the handles and snaps, as the
  Dart view does. The window keeps one view for each panel across a layout
  change, and the dock host skips a layout whose text has not changed.
- A pick made by the Dart editor and the C# one match. The exporter
  `tool/parity/pick_test.dart` writes the ray, the object and the handle for a
  grid of pixels from three cameras, and `DartPickTests` reads them back.
- Two finger scroll on a trackpad only zooms in C#. Avalonia gives no scroll
  phases, so the view cannot orbit or pan from it as Flutter's pan and zoom
  does. The state class has both, so they need only the event. The hint chip
  in the view says what works. A pinch zooms.
- A click in the view ends a flight begun with the backtick, because any
  pointer going up ends flying in the Dart view and the state class keeps that.
- The C# scene gets its material and surface keys from FNV-1a. Dart used
  `Object.hash`, which promises no value, so the keys differ and nothing
  reads them. The rain and snow colour is a double lerp of the two tints.
- The grid tile is encoded by `Orblit.Sprite`, so its bytes differ from
  Flutter's. The lines it draws read back the same.
- A project, scene, layout and layout library written by the Dart editor
  read back in C# and write the same bytes. `tool/parity/dart_files_test.dart`
  writes the fixtures.
- What is not ported yet: the skeleton and limb gizmos, mesh editing, every
  panel body, the terrain, cinematics and interface modes, the camera preview
  window, panel drag, and closing a project back to the
  launcher. Panels other than the scene view show their name.
- No icon set or font is bundled, because that is a decision for Chris. The
  mode rail, the tabs and the palette rows are words. The 622 KB logo
  picture is not bundled, so the splash is drawn in code.
- The C# editor keeps its recent projects in `Orblit/recent.json` under the
  application data folder. The Dart editor keeps `recent_projects.json` in
  its own support folder, so the two lists are separate.
- Differences from the Dart editor:
  - A folder argument that holds no `orblit.project.json` shows the launcher
    in both editors. Dart also prints a line saying so, and C# does not.
  - The palette search lower-cases one character at a time. A label with a
    capital dotted I lights a different span from Dart.
  - Sibling scenes in a project are sorted by name. Dart used folder order.
  - `ProjectStore.Forget` works with no recents folder. Dart threw.
  - .NET reads a few loose dates that Dart refuses.
  - A Save as that cannot write leaves the scene on the file it had. Dart
    keeps the new path.
  - `PanelKind.Icon` and `PaletteEntry.Icon` are names, where Dart held an
    `IconData`.
  - Text on the system clipboard keeps the held copy when a world in it is
    not sixteen numbers or names no copied object. Dart clears the copy
    first and throws on the next paste.
- Faults the C# port copied from the Dart editor, for decision 80:
  - `displayPath` shortens a folder that only shares a prefix with the home
    folder, so `/Users/chrisfoo` shows as `~foo` for the home `/Users/chris`.
  - Opening a closed panel is not saved with the layout until the layout
    next changes.
  - Undo and redo drop every selected id that the changed scene does not
    hold, so undoing a step in one scene clears a selection made in another.
  - A main scene that cannot be read opens as an empty starter with no path.
    The same file is also listed as an unloaded sibling.
- Add `Orblit.Editor.Panels` 0.1.0, the state of the panels with no widget.
  It holds the hierarchy first: the rows of every open scene, the filter,
  the folds and where a drop lands.
- The C# editor has a hierarchy panel. A click selects, command-click adds
  to the selection and shift-click takes everything between. Filter by
  name, fold a parent, remove an object from its row, and drag a row to
  move it. The plus button opens the palette on the add commands.
- Cut, copy, paste, duplicate and delete are in the Edit menu and on keys.
  A copy leaves text on the system clipboard, so another window or editor
  can paste it. A copy that cannot reach the clipboard says so in the status
  line. The Add menu makes an object of each kind and each shape.
- The edit keys are left to a text box while one has the keyboard. A click on
  a row takes the keyboard back from it.
- A message can be a warning, drawn in the warning colour. Adding with no
  scene loaded says so as a warning.
- A new object's id is `o`, the time in microseconds, an underscore and a
  counter. The C# `OutlinerRow` calls the object it holds `Item`, because
  the analyser rejects `Object` as a name.
- Double-click a scene row to load it, and close it from the button that
  shows when the pointer is over the row. A scene with changes asks first:
  Save, Discard or Cancel. A scene that has no file yet asks for a name
  before it is saved. File, New scene and the command and N key start an
  empty scene named Untitled. The view's camera starts again when a
  different scene takes the place of the loaded one.
- Differences from the Dart editor:
  - The shared row has no close button. Dart draws one that only forgets
    the shared set's undo steps.
  - Closing any scene clears the selection, as Dart does, and loading one
    clears the selected scene row.
- Faults copied from the Dart editor, for decision 80: a scene file that is
  gone loads as the starter scene at that path and is marked as written.
- Not ported yet with the scene row: `_preview.forget()`, the cleared
  report notes and the bench change, which belong to rows 4 and later. The
  window does not ask about unsaved scenes when it closes.
- The inspector sits in the dock. It shows what is selected, or the scene
  itself when nothing is. A heading says what kind of thing it is, and a
  click on a section's title folds it. A vector's three numbers wear the
  letters X, Y and Z.
- Sections for the transform, the mesh, the light, the weather and the
  scene's own settings, then sound, particles, vehicle, wheel, zone, body
  and joint. A section is offered where it means something. A zone is for a
  body that stays put and a wheel is for the child of a car. A file that
  already holds one on something else still shows it, so it can be taken
  off. A body is fitted to what is drawn when it is added. A compound body
  has its own parts, and a part can be a hull. A joint has a row for each
  way its kind may move, and a spring and a drive where they apply.
- One drag through a number is one undo step. Choosing what is already
  chosen is not a step.
- Differences from the Dart editor:
  - A path with no points asks for the next one past the origin. The Dart
    reads the last point of an empty list and throws.
- Not wired in the window yet: the data, interface and prefab buttons, the
  size of a mesh and the model collision button. The inspector takes them
  as callbacks from the shell port, so those rows are absent until then.
  The shader, terrain and blend sections, and the key button for an
  animation, come with rows 3, 4, 5 and 7.
- Add the asset model to `Orblit.Editor.Core`. `AssetTree` reads a project
  folder, lists its folders, and renames, deletes and makes files. It tells
  the browser when the folder changes, and joins a burst of changes into one
  call. `NewAsset` holds every kind the browser makes, each with a starter
  that works the moment it is written. A C++ source is written with its
  header. `AssetKind` and `SizeLabel` say what a file is and how big.
  `DataObject` is only as big as a new file needs. Row 10 fills it in.
- Differences from the Dart editor:
  - A rename to a name that differs only in case is refused when a
    different file already has that name on a case-sensitive disk. Dart's
    rename replaced that file.
  - A new C++ pair keeps a header that is already there. Dart wrote over it.
  - A name with a `/` is refused on Windows too. Dart refused only the
    host's own separator.
  - The folder watch asks for a fresh read when it fails. Dart's stopped
    quietly.
  - A new folder inside a folder that has been deleted makes the parents.
    Dart reported an error.
- Faults copied from the Dart editor, for decision 80: a linked file or
  folder is left out of the browser, so a project that links in a shared
  folder does not show it.
- Add `CookStatusIndex`. It asks the cook where each asset stands without
  cooking any of them, through a `CookProject` over the project's assets
  folder and the machine's own cache. The work runs on the pool. Two asks at
  once make one more pass, not two. `CookStateLook` gives a state its label,
  its reason and the colour of its dot. The colour is a name, `CookMark`,
  and the window picks the brush.
- A cook that fails reads as the exception's message. Dart printed the type
  name before it.
- Add the shader cook and the shader section. `ShaderCook` stages the
  project's `.oshader` files and what they include, cooks only those, and
  keeps each good package in `.orblit/shaders/packages` so a failed save
  leaves the last one on disk. `SurfaceCooks` holds the last good cook of
  each shader, cooks again only when a shader file has changed, and tells
  each new complaint once. The inspector's Shader section offers the
  uniforms an author marked `instance`: a swatch row for `source_color`, a
  slider for `hint_range`, a drag for the rest. The window fills
  `InspectorLinks.Surfaces` with the shell, so the section is not on
  screen yet.
- Differences from the Dart editor:
  - A colour uniform is the editor's swatch row. Dart opened a free colour
    row.
  - The cook runs on the thread pool. Dart ran it in an isolate.
  - A failed cook with no error reads "<shader> did not cook.".
- `SurfaceCooks` is driven from the interface thread. `CookStatusIndex`
  can raise `Changed` from the pool.
- Add the asset browser and the asset preview to the dock's Project panel.
  The browser lists the project's folders and files. Each file has a mark
  for where it stands with the cook, the header counts what is left to do,
  and a filter keeps the files whose name has what was typed. A texture's
  tile shows the picture, and any other file shows an icon. A right click
  offers what can be made or done to a file. A narrow panel puts the
  folders over the files and drops the filter. A double click opens a
  folder, or a scene.
- The preview sits beside the files where the panel is 560 wide or more. A
  model is drawn by the renderer from the file on disk, framed by the size
  the file declares and turning slowly. A drag moves the camera round it
  and a scroll moves it in and out. A texture is shown as itself over a
  chequer. Any other file is named by its kind. A button in the header
  shuts the preview and frees the renderer.
- Differences from the Dart editor:
  - A model that is stopped stays where it is. Dart took back the turn it
    had made and the model jumped.
  - The extent of a model shows where there is no renderer too.
  - A texture is shown from a copy decoded to 480 pixels wide, through the
    cache the tiles use. Dart showed the file at full size.
  - Deleting or renaming the file that is shown clears the selection and
    the preview. Dart left the preview on the old path.
  - Selecting a folder in the grid tells the editor that nothing is
    selected.
  - Icons are drawn from path data, because no icon font is bundled
    (decision 84). The menu rows have none and the cook summary has no
    warning mark.
  - Thumbnails decode on the pool, and the cache keeps a fixed number. Any
    file that will not decode keeps its icon.
  - The menu follows `NewAssetGroup.Loose`, and an object dropped on a
    folder passes the object's id.
  - A write under a folder whose name starts with a dot, such as `.git` or
    `.orblit`, does not make the browser read again. Dart reread for every
    write, and a shader cook writes under `.orblit`.
  - `AssetTree.Write` refuses a name that holds a separator or is `.` or
    `..`. Dart wrote wherever the name led.
- Not in the browser yet: dragging a file out of it. The window wires only
  opening a scene or a clip and the line that says a file operation failed.
  Opening the other kinds, Build, making a prefab and the data object come
  with rows 5 to 13.
- Add the timeline to the dock. A bar plays, steps and sets the length, the
  rate and what happens at the end. A ruler, the marks and a cutscene's
  shots sit over the tracks, and Keys and Curves chips swap the dope sheet
  for the curve view. A key button follows each transform field and a
  light's power. The animation section in the inspector links clips to an
  object and picks the one that plays on start. Opening a clip from the
  browser, or making one with an object chosen, shows it on the timeline.
  Saving writes the scene as it rests, with the pose lifted off first, and
  the clips with changes beside it. The File menu shows a dot while a scene
  or a clip has changes that are not on disk.
- Differences from the Dart editor in the timeline:
  - A clip with a length that is not a number is turned away. Dart let it
    through.
  - A clip that cannot be made is said at the error level. Dart said it at
    the default.
  - Scrolling is not carried between the Keys and Curves views. Dart shares
    one controller.
  - A press that moves under 4 px is a tap. Dart leaves that to its gesture
    arena.
  - Not in the timeline yet: playing the scene with Play, Stop and the game
    view, which no row owns, and the retarget dialog, which comes with row
    7.
- Add the terrain mode to the C# editor window. Add > Terrain makes the
  ground and a scene object that names it. In the terrain mode a shelf of
  brush tools sits over the view, the brush panel sits beside the inspector,
  and a drag in the view shapes the ground as one undo step. A ring on the
  ground shows where the brush lands. The title shows unsaved terrain.
- Read the pictures of a terrain set in the C# editor with SkiaSharp 3.119.4.
  A picture is read off the UI thread at 512 by 512 with straight alpha, four
  at a time, and a source over 8192 pixels a side is refused. One that cannot
  be read is said once, the ground stays plain, and the next change to the
  sets tries it again.
- Not in the terrain mode yet: dropping a picture on a set, which needs a
  drag source in the project browser.
- Add the selection model of the C# modelling workspace: the vertices, edges
  and faces chosen in one mesh, the picker that finds what a click or a box
  over the view landed on, and the size an imported model says it is. There
  is no panel or tool on it yet.
- Add the list of mesh actions to the C# modelling workspace: twenty-five
  things to do to faces, edges, vertices and whole shapes, each with its
  words, its icon name, the modes it appears in and how many elements it
  needs. The selection keeps the order things were picked in, which is the
  order a bridge is wound in. There is no panel on it yet.
- Add editing by part to the C# viewport. With a shape in Edit, the pointer
  lights the face, edge or vertex under it, a click picks it, a box takes
  everything inside it, and the handles move or turn what is selected.
  Shift on a face extrudes it first. A drag is one undo step. There is no
  panel or session on it yet.
- Add drawing a shape and cutting a face to the C# editor. A click puts a
  point on the ground or on the face under the pointer, and the first
  point fixes the plane. Enter or a click back on the first point makes
  the shape. A cut needs two points on the edge of a face. Backspace
  takes a point back and escape gives up. There are no tool buttons or
  painter on it yet.
- Add the UVs panel to the C# editor. It draws where the texture of the
  selected faces sits on the texture's own square. A drag in it moves,
  scales or turns the texture, and is one undo step. Buttons freeze the
  rule into coordinates, put it back, fit it and project it. A single
  face that follows a rule shows the rule's numbers. Parts now start in
  face mode, as they do in Dart.
- Add the modelling panel to the C# editor, in the modelling workspace. It
  holds the grid and snapping, the drawing tools, the mesh actions in
  groups with the number under the button that uses it, the materials and
  an Export section. The inspector gets a shape section with each kind's own
  numbers, and a button that goes to the modelling workspace. The buttons are
  words only, where the Dart panel draws an icon beside each.
- Draw the edited mesh over the C# viewport: the wire, the chosen and lit
  parts, the outline being drawn and the selection box. A click, a box or a
  drag on parts goes to the session, and a drag is one undo step.
- Add keys to the C# editor. The arrows move the selection by one grid
  square, shift moves it up and down, and alt does ten squares. Each press is
  its own undo step. G goes into the geometry and then round vertex, edge
  and face. The bracket keys change the grid and switch it on. A focused
  text box keeps all of them. The shift in a parent's frame is one function
  in `SceneMath`, shared by the viewport and the nudge.
- With nothing focused, Avalonia spends the first arrow key on moving focus
  into the window, so the first arrow after a fresh start does not nudge.
  A click in the view puts the keyboard there. The editor does not take the
  arrow back, because that would break keyboard navigation.
- Add export of a shape to the C# editor. A format choice (OBJ, glTF binary,
  STL, PLY) and a name prompt write the file into `exports/` in the project.
  An OBJ with materials brings its `.mtl`, and the message names both files.
- The C# modelling workspace fixes seven faults that the Dart one has.
  Choosing another object clears the selected parts, which were numbers on
  the first mesh. The Cut tool opens the geometry, where faces are found.
  A drawing that has its plane takes no point from a ray that misses it.
  Turning and scaling in the UVs panel hold the middle of the faces still.
  The UV view holds the unit square and the faces together. Shift on a
  face handle extrudes it. Live surface, outline and boundary edits fold
  into one undo step.
- Add the blend graph to the C# editor, in the animation workspace. A blend
  opened from the project panel is drawn as a box for each state and an
  arrow for each change, with the fields beside it, or under it in a narrow
  panel. Each open blend is a tab, and a tab with changes is marked until it
  is saved. Drag from a state onto another to make a change. The fields
  edit a state, a change, its conditions, a fade and the inputs, and each
  edit is one undo step.
- Add Retarget to the clip menu of the C# timeline. A dialog asks which model
  the clip was made for and which one it should move, shows the target's
  bones with what moves each, and takes a pairing by hand, by name or by the
  parts of a person. A bone map is saved under `retargets/` and loaded
  again. The made clip is written into `clips/` as `name (model)` and never
  over another. It goes on the chosen object only when that object wears the
  model the clip moves. A model's bones are read again each time the dialog opens.
- The C# blend graph fixes two faults that the Dart one has. Moving a change
  in a blend with no change answers the same blend, where Dart throws. A
  blend with one change reads "1 change", where Dart reads "1 changes".
- Add the cinematics mode to the C# editor. A shot list shows one card for
  each shot of the open cutscene, with its camera, start and length, and a
  way to look through the camera or delete the shot. A shelf makes a cutscene
  and cuts to the selected camera, or to the scene view with Use this view. A
  preview draws the scene through the shot at the playhead. Looking through a
  camera flies the scene view to it and steers it from the view, until
  something else stops it. The preview draws the scene only, with no
  interface or sound, because the game view is not in C# yet.
- The C# shot list keeps a card where it is while its start is typed, and
  re-sorts the cards when the box lets go. The Dart one moves the card as
  soon as the start is typed. A card whose shot an undo has taken back does nothing when
  typed in.
- Add the Interface workspace to the C# editor. An interface opens from
  Project, from the inspector of the scene's canvas or as a new one. It is
  laid out at the size of the device previewed, held either way. A tree
  lists the elements. The design side adds an element, edits the one
  selected, splits a container into columns, picks a language, and sets the
  canvas size, fit and grid. A drag snaps to the columns and is one undo
  step. The top bar names the interface by its place in the project.
- The C# scene view draws the interface its canvas names, and a chip puts
  it aside without changing the scene. The Dart view paints an opaque
  ground and dark bars under it, which hide the scene. The C# view draws
  the interface alone.
- The C# Interface workspace differs from the Dart one in small ways. The
  top bar has no Saved status, object count or frame rate. The fit chips
  wrap. The grid count has a full stop where Dart has a dash. The Interface
  chip belongs to each scene view, not to the shell. An interface cannot be
  dropped onto the scene view yet. With a button or text selected, Add puts
  the new element after it, where Dart puts it inside. A drag in a stack
  away from the page edge snaps to the grid on the page, where Dart snaps
  it from the stack's edge.
- Add the console to the C# editor. It shares the bottom group with the
  project browser. It keeps what the editor said, what the UI thread
  threw, and a run of the same message as one entry with a count. Each
  level has a filter with its count, and an entry opens to its detail.
- Add data objects to the C# editor. One is made from Project with a
  value of each kind, edited in the inspector and attached to the
  selection as one undo step. Each edit writes the file and the
  TypeScript and C++ bindings beside it.
- Add the profile panel to the C# editor. It reads the frames the scene
  view draws, the gauges, the counts and the memory, and says how fast a
  growing heap grows.
- The C# editor opens a script, a style or a data file in the code editor
  it finds, from Project, the File menu or the palette. Show in Finder
  shows the project folder. Build compiles a C++ script with the engine's
  own compiler and headers and shows what the compiler said.
- The C# data objects, profile panel and code editor differ from the
  Dart ones in small ways. Sublime Text and Zed are handed a script
  without `--goto`, which they do not take. A new data object's kind is picked from a menu, not a
  dialog. A number that is not finite is refused. Selecting a scene row
  puts the inspector back. The profile panel times no spans, and a gauge
  reads "12" where Dart reads "12.0".
- Add the export dialog to the C# editor. File › Export… and the
  palette open it. It cooks a pack for each chosen target, or a patch
  on a pack already exported, and shows each target as it lands. It
  lists what the exports folder holds and what a pack serves, and it
  cannot be closed while a cook runs.
- The C# export differs from the Dart one in small ways. Each target
  cooks on a task, not an isolate, so a cook that runs out of memory
  takes the editor with it. The progress line has no spinner.
- Add the kit store to the C# editor. File › Kit store… and the palette
  open it. It browses a store's kits with search, sort and filters, shows
  a kit's releases, notes and files, and installs the files chosen into a
  folder of the project. The `kits.lock` it writes is the one the Dart
  editor writes for the same kit, byte for byte.
- Add the publish dialog to the C# editor. File › Publish kit… and the
  palette open it. It packs a folder of the project as a kit and shows
  the store entry to copy. Nothing is uploaded.
- The C# store differs from the Dart one in small ways. A kit is packed
  on a task, not an isolate, and a pack cannot be cancelled. The store's
  settings and downloads sit in the C# editor's own support folder, so a
  kit the Dart editor downloaded is fetched again.

## 1.26.0+27

- Size an `.obj` from its points. The asset preview frames it, and a model
  dropped into the scene is picked and outlined at its real size, where
  before it was a two-metre cube.
- Build a collision shape from an `.obj` in the project. An `.fbx` still has
  no size or collision shape here, because the editor does not cook it.

## 1.25.0+26

- Add a Key field to a text or a button. The canvas shows the words of that
  key in the language chosen, and the tree and Undo name the element by it.
- Read `strings/<locale>.json` from the project, one file for each language.
  Reopening an interface reads them again, and so does Reload.
- Add a Languages section to the Interface design panel. It has a chip for
  each language, lists the keys the chosen language has no text for, and
  says which files could not be read. It shows only when the interface has a
  key or the project has a language file.
- Show the chosen language in the scene view and the game view as well.
- Start in the first language the project has when it has no `en`.
- Report a `strings/` folder that cannot be listed, where it stopped the
  editor from opening an interface.
- The language shown is a view. It is not saved in the interface file.

## 1.24.0+25

- Add a Profile panel to the View menu. It shows the gaps between drawn
  frames, the time the scene view and the game view spend handing the
  scene to the renderer, and the editor's resident memory with its trend
  in megabytes a minute. A still editor draws no frames, so a long gap is
  rest.

## 1.23.0+24

- Draw an object with a shader somebody wrote. Drop a `.oshader` file on
  the selected object, or press it in the project browser to edit it. The
  editor cooks every shader in the project when one is saved.
- Add a Shader section to an object wearing one. It shows the uniforms
  marked `instance`: a row of colour swatches for `source_color`, a slider
  for `hint_range`, a field for the rest. Each object keeps its own values in
  the scene file, and two objects on one shader can differ.
- Keep drawing the last shader that compiled when a save does not. The
  console names the file and line of the error once.
- Add Lit shader and Unlit shader to the New menu.
- Pictures a shader reads are named in the scene file. There is no picker
  for one yet.

## 1.22.0+23

- Add a Particles section to an object. It adds or removes an emitter and
  sets its shape, rate, life, speed, gravity, drag, noise, space and
  the most it may hold alive. A life or speed range dragged past its other
  end takes that end along. The colour and size ramps, bursts, spin and
  duration stay in the scene file.

## 1.21.0+22

- Add a Sound section to every object. It adds and removes a sound source
  and sets its clip, bus, loop, volume, near and far distance and fade. It
  also marks the object as the listener and names the surface it is made of.
  The near end cannot pass the far end.
- Draw the range of a selected sound source as two rings, one where it is at
  full volume and one where it falls silent.
- Share the rows that read and write one component between the Vehicle and
  Sound sections.

## 1.20.0+21

- Add Scene > Publish kit. Name a folder of the project that holds a
  `kit.json`, the address the kit file will be hosted at and a note. The
  editor packs the folder, checks the file as CI will, and shows the entry
  that lists the kit, with its hash and size filled in. Copy puts the entry
  on the clipboard.
- Write the kit file to `exports/kits/` of the project, named after the end
  of the address. Nothing is uploaded and no entry is written for you.
- Add Publish kit to the command palette.

## 1.19.0+20

- Add Scene > Kit store. It reads a store of kits, shows each as a card with
  a picture, and opens one to show its notes, its releases, its licence and
  the engine it is for. Press Show files to list the files in it. Nothing is
  sent to the store until you press Go online.
- Search the kits, sort them, and narrow them by licence or tag. Hide the
  ones that hold scripts. A kit made for another engine version is hidden
  until you ask for it.
- Install a kit into a folder of the project, `assets/kits/<kit>` unless you
  choose another. Pick the files you want. A file the project already has
  with other bytes is left alone unless you tick it. If the write fails, the
  dialog keeps your folder and your ticks.
- Show the scripts of a kit before anything is written. Installing never
  runs one.
- Keep a download that was cut and take it up where it stopped. A file that
  does not match its hash is refused and nothing reaches the project.
- Write `kits.lock` at the project root. It lists each kit installed and the
  hash of every file it wrote.
- Add another store by address, and forget it again. The editor reads
  `https://orblitengine.com/store/` until you do.
- Add Kit store to the command palette.

## 1.17.0+18

- Add a Vehicle section to a body the solver moves. It sets the steer lock,
  the anti-roll bar, the idle speed, the final drive, reverse and the
  differential lock. The torque curve and the gears stay in the scene file.
- Add a Wheel section to a direct child of a car. It sets whether the wheel
  steers, drives and takes the handbrake, and its radius, travel, spring,
  damping, grip and brake. Place the wheel where it sits at rest.

## 1.16.0+17

- Add Scene > Export. It cooks the project for each target chosen and packs
  the result into `exports/` beside the project. A whole pack is
  `game.<target>.opack`, which is the file an app ships.
- Build a patch against an export that is already there. It holds only what
  changed, is named by its hash, and is for the target of the pack it stands
  on. A patch with nothing in it is reported as unchanged and not written.
- Replace a whole pack without losing the old one. It is kept under its
  hash, so patches built on it still find their base.
- Report each target as it lands. A target that fails to cook names the
  assets and the reasons and leaves the last good pack as it was.
- List what an export serves, which pack each asset comes from, and what a
  patch takes away. It reads each pack's directory and no blobs.
- Add Export to the command palette.

## 1.15.0+16

- Pose a limb by dragging its hand or foot. With **Bones** on, a selected
  model that has a person's skeleton shows a ring on each hand and foot,
  over the pose the open clip gives at the playhead. Drag a ring and the
  arm or leg follows the pointer.
- A drag keys the two bones above the ring at the playhead, as one step of
  undo. Everything else of the pose is left as it was. A limb that cannot
  reach straightens towards the pointer.
- The rings are hidden while the clip plays, and in a cutscene, which
  cannot move a model's bones.
- A rig that cannot be read as a person gets no rings, nor does a limb
  with more bones than a person's. Key its bones from the timeline as
  before.

## 1.14.0+15

- Edit a blend as a graph. A `.oblend` file opens in the **Blend** panel
  beside the timeline. States are boxes and changes are arrows. Drag from
  a state's handle onto another to make a change.
- Choose a state or an arrow to edit it. A state sets what it plays, its
  speed, what happens at the end and its sync marks. A change sets where it
  leaves from and goes to, its condition and its fade. The order of the
  changes is the order they are tried in.
- Every edit is a step on the undo stack, saved with Cmd+S beside clips and
  cutscenes.
- Make a blend from the Project panel's New menu or the Blend panel's
  **New blend** button. New blends go in `blends/`.
- Show `.oblend` files in the asset browser as blends.

## 1.13.0+14

- Move a clip onto another model. **Retarget to another model…** in the
  clip menu opens a dialog with the model the clip was made for, the model
  to move, and a row for each bone of the second. A row says what moves the
  bone: the automatic match, a bone picked by hand, or nothing.
- Save and load a bone map. A map is an `.obonemap` file under
  `retargets/`, so the same pairing carries every clip made for one model
  onto the other. A pair a model lacks is named and left out.
- Make clip writes `clips/<clip> (<model>).oclip` and opens it. The clip is
  put on the selected object only when that object is the target model.
- Show `.obonemap` files in the asset browser as bone maps.

## 1.12.0+13

- Draw a model's bones in the view. **Bones** on the timeline toolbar
  shows the skeleton of each selected model, in the pose the open clip
  gives at the playhead, or at rest when the clip does not reach it.
- Add onion skin. **Onion** draws the poses before and after the playhead
  as ghosts, with a menu for how many and how many frames apart. A looping
  clip wraps and any other clip has no ghost past either end.
- Add a tooltip to every timeline toolbar chip.

## 1.11.0+12

- Add Gear, Rack, Pulley and Path to the joint kinds, with a ratio row for
  the first three, a second axis for gears and racks, two fixed points for a
  pulley and a list of points for a path.
- Give each way of a joint a Spring row with its frequency, damping ratio and
  rest, a Soft limit row, and a Goal row on a driven way. A drive set to
  Place holds a place instead of a speed.
- Draw a path as its line with the body's place on it, a pulley as its rope
  over the two points, and a gear or rack as a pinion.

## 1.10.0+11

- Offer Character as a body's motion, with its step height, steepest slope,
  skin, push strength and crouch height. A character shows no lock, place
  or belt rows, because the world sweeps it.

## 1.9.0+10

- Add a Continuous row to the Inspector's Body section. It stops a small fast
  body at a thin fixed wall, and costs a cast while the body moves.

## 1.8.0+9

- Redraw the editor to the new design. The top bar holds the file menus,
  the project name, the object count and search. The workspace tabs run
  down a rail beside the panels, and the play control sits in the scene
  view's header. Cmd+K opens one palette for commands and objects.
- Add a filter and an add menu to the Hierarchy, and a filter and a
  breadcrumb to the asset browser, with a preview beside it.
- Draw the viewport tool strip, orientation gizmo and a stats chip.
- Open the Inspector as collapsible sections with a Visible switch in the
  header. Open sections are remembered by title.
- Put the mode shelf in a rounded panel beside the rail.
- Redraw the launcher with a side rail, template cards and project cards
  that show a picture of the scene. A template card opens the create view
  with that template chosen.
- Remove the status bar. The last change is in the Undo tooltip.
- Test the command palette.

## 1.7.0+8

- Make fixed mesh colliders from imported models or edited geometry.
- Show triangle outlines and preserve collision geometry through save and undo.


## 1.6.0+7

- Add Compound to body shapes, controls for local parts and shape scaling, and collider outlines for their transformed geometry.


## 1.5.0

- Give a body a cylinder or a convex hull from the Shape rows. A cylinder has
  a radius and a height. Fit to mesh sizes it to the object.
- A hull is cut from the mesh the object draws, so a rock collides as the
  rock and not as its box. An object the editor holds no mesh for, such as an
  imported model, gets the corners of its box. The inspector counts the
  corners and says when the points enclose no volume, which is a body that
  does nothing.
- Both shapes are outlined in the scene view and scale with their object. A
  hull of more than 255 corners is drawn whole. The simulation keeps the 255
  that stand out most.

## 1.4.0

- Hold a free body to a plane or a line with move and turn locks on each
  axis. Set gravity, a speed cap and a spin cap for the body, and where its
  weight sits and how it resists turning. Zero is no cap.
- Choose a material for a body from Ice, Metal, Wood, Stone, Sandbag and
  Rubber. It sets friction and bounce, and reads as Custom once either is
  changed.
- Choose what a body is in and what it sees from a grid of thirty-two layer
  toggles. A pair meets when either body sees the other's layer.
- Name the physics layers in the scene's inspector. The names show on the
  layer toggles.

## 1.3.0

- Make a physics body a trigger. A trigger is a place: nothing collides with
  it, and it reports what enters and leaves. Stay events make it report
  every step as well.
- Set a belt speed on a fixed or driven body to carry what stands on it.
- Add a zone to a body that stays put, to change gravity and drag for what is
  inside it. Each field is the body's own until it is switched to the zone's.
  Priority decides which zone wins where two overlap.
- Triggers and zones are outlined in amber in the scene view, apart from
  solid bodies.

## 1.2.0

- Make cutscenes in the Cinematics workspace. Cut between the scene's
  cameras in a shot list, and key the scene on a timeline under the view.
- Put a camera where the scene view is with Use this view. Frame a shot by
  steering its camera with Look through.
- Watch the finished shot beside the scene. In play, a mark named after a
  cutscene starts it.
- Keep keyboard shortcuts working after Enter in a field.

## 1.1.0

- Create and assign animation clips from the Timeline. Select the starting
  clip in the Inspector, and preview scene animation with Play, Pause and Stop.
- Follow animated selections with the Timeline in Scene and Animation.
- Save, load and delete named layouts per workspace and project. Focus the
  view while retaining the surrounding panels.
- Label terrain tools, add guidance and creation buttons to empty panels,
  explain editing controls on hover, and put performance numbers behind Stats.
