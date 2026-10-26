namespace Orblit.Editor.Panels;

using Orblit.Editor.Core;
using Orblit.Weather;

/// <summary>What the inspector says about a scene itself, apart from drawing it.</summary>
public static class SceneFieldsModel
{
    public static SceneTime TimeOf(EditorScene scene) => new(scene.TimeOfDay, scene.DayCycle, scene.HoursPerSecond);

    /// <summary>The file a loaded scene is saved to, or that it has none yet.</summary>
    public static string FileLine(SceneEntry entry) =>
        entry.Path is null ? "Not saved to a file yet" : Path.GetFileName(entry.Path);

    /// <summary>Where a scene that is not open lives, which is all there is to say about it.</summary>
    public static string WhereLine(SceneEntry entry) => entry.Path ?? "Never saved";

    /// <summary>The time as it stands: the hour, and which body is up.</summary>
    public static string Now(EditorScene scene) =>
        $"{InspectorModel.Clock(scene.CurrentTimeOfDay)}  {scene.ActiveBody.Label()}";

    public static string TimeNote(bool cycle) => cycle
        ? "The time runs from where it is set, and the light above the scene is whichever body is up. The scene keeps the hour it was saved at."
        : "The scene sits at this hour. The light above it is whichever body it is set to be.";

    public static (int Objects, int Drawn, int Lights) Counts(EditorScene scene) => (
        scene.Length,
        scene.Objects.Count(one => one.IsDrawable),
        scene.Objects.Count(one => one.Kind == ObjectKind.Light));
}
