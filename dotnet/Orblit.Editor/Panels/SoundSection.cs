using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Scene;

/// <summary>
/// The section for sound: a source, where sound is heard from, and what an object is made of when two
/// things touch. Offered on every object, since any of them can make a sound.
/// </summary>
internal static class SoundSection
{
    private static readonly string[] NoYes = ["No", "Yes"];

    private static readonly string[] Fades = ["Inverse", "Linear"];

    public static Control Build(Fields fields, InspectorTarget target)
    {
        var name = target.Item.Name;
        var source = new ComponentRows<SoundSourceComponent>(fields, target, SceneComponents.SoundSource);
        var listener = new ComponentRows<SoundListenerComponent>(fields, target, SceneComponents.SoundListener);
        var surface = new ComponentRows<SoundSurfaceComponent>(fields, target, SceneComponents.SoundSurface);
        return fields.Section(
            "Sound",
            Fields.Column(
            [
                .. Source(source, name),
                fields.Choice(
                    "Listener",
                    NoYes,
                    listener.Current is null ? "No" : "Yes",
                    chosen =>
                    {
                        if ((chosen == "Yes") == (listener.Current is null))
                        {
                            listener.Put(chosen == "Yes" ? new SoundListenerComponent() : null, $"Set {name} listener");
                        }
                    }),
                fields.Entry(
                    "Surface",
                    surface.Current?.Name ?? "",
                    // An empty name is no surface, so the file does not keep one.
                    text => surface.Put(text.Length == 0 ? null : new SoundSurfaceComponent(text), $"Set {name} surface"),
                    "wood"),
            ]));
    }

    private static List<Control> Source(ComponentRows<SoundSourceComponent> rows, string name)
    {
        if (rows.Current is null)
        {
            return [rows.Add("Add sound source", "Make this object a place a sound comes from.", new SoundSourceComponent(), $"Add sound source to {name}")];
        }

        return
        [
            rows.Text("Clip", source => source.Clip ?? "", SoundModel.WithClip, "sounds/fire.ogg"),
            rows.Text("Bus", source => source.Bus, (source, text) => source.CopyWith(bus: text.Length == 0 ? "master" : text), "master"),
            rows.Toggle("Loop", source => source.Loop, (source, on) => source.CopyWith(loop: on)),
            rows.Drag("Volume", source => source.Volume, (source, value) => source.CopyWith(volume: value), new DragOptions { Minimum = 0, Step = 0.05 }),
            rows.Drag("Near", source => source.Near, SoundModel.WithNear, new DragOptions { Minimum = SoundModel.Gap, Step = 0.1 }),
            rows.Drag("Far", source => source.Far, SoundModel.WithFar, new DragOptions { Minimum = SoundModel.Gap * 2, Step = 0.5, Decimals = 1 }),
            rows.Choice(
                "Fade",
                Fades,
                source => source.Curve == SoundCurve.Linear ? "Linear" : "Inverse",
                (source, option) => source.CopyWith(curve: option == "Linear" ? SoundCurve.Linear : SoundCurve.Inverse)),
            rows.Remove("Remove sound source", "Stop this object making a sound.", $"Remove sound source from {name}"),
        ];
    }
}
