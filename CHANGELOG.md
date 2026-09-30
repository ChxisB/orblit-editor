# Changelog

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
