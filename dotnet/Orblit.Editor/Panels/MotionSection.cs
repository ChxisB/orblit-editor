using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Scene;

/// <summary>
/// The clips an object plays: the ones it is linked to, which one starts with it, and the links to
/// the clip open on the timeline. The files are made and edited on the timeline. This only links them.
/// </summary>
internal static class MotionSection
{
    private const string NoClip = "None";

    public static bool AppliesTo(InspectorTarget target) => target.Item.Kind != ObjectKind.Scene;

    public static Control Build(Fields fields, InspectorTarget target)
    {
        var motion = EditorSession.MotionOf(target.Item);
        var clips = motion?.Clips ?? [];
        var rows = new List<Control>();
        if (clips.Count == 0)
        {
            rows.Add(Fields.Note("Make a clip in Animation to move this object. You can also attach the clip open on the Timeline."));
        }

        rows.AddRange(clips.Select(path => Fields.Button(
            path,
            "Open this object’s clip in Animation.",
            () => target.OnOpenClip?.Invoke(path))));
        if (motion is not null && clips.Count > 0)
        {
            rows.Add(OnStart(fields, target, motion));
        }

        rows.Add(Attach(target));
        if (motion is not null)
        {
            var remove = Fields.Button(
                "Remove animation",
                "Unlink the clips from this object. Keep the files.",
                () => fields.Host.Apply(() => Put(target, null)));
            rows.Add(remove);
        }

        return fields.Section("Animation clips", Fields.Column(rows));
    }

    private static Control OnStart(Fields fields, InspectorTarget target, MotionComponent motion) => fields.Choice(
        "On start",
        [NoClip, .. motion.Clips],
        motion.Autoplay is { } first && motion.Clips.Contains(first) ? first : NoClip,
        chosen => Put(target, new MotionComponent(motion.Clips, chosen == NoClip ? null : chosen)));

    private static Button Attach(InspectorTarget target)
    {
        var attach = Fields.Button(
            "Attach open clip",
            "Let this object play the clip open on the Timeline.",
            () => target.OnAttachClip?.Invoke());
        attach.IsEnabled = target.OnAttachClip is not null;
        return attach;
    }

    // One step with the change on either side, so it is not folded into an edit before or after it.
    private static void Put(InspectorTarget target, MotionComponent? motion)
    {
        target.History.Seal();
        target.History.Run(new SetObjectComponent(
            target.SceneId,
            target.Item.Id,
            $"Change animation on {target.Item.Name}",
            SceneComponents.Motion,
            EditorSession.MotionOf(target.Item),
            motion));
        target.History.Seal();
    }
}
