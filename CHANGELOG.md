# Changelog

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
- A model that is not a person, or whose arm or leg has another bone
  between its joints, gets no ring for that limb. Key its bones from the
  timeline as before.

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
