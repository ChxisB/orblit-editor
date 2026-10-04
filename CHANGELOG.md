# Changelog

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
