namespace Orblit.Editor.Core;

/// <summary>The kinds of object the add menu and the palette offer, in the order they are listed.</summary>
public static class AddMenu
{
    // Not 'Cube': a mesh object draws a cube until it is given a mesh to draw instead, and there is a
    // real cube in the shapes.
    public static readonly IReadOnlyList<(ObjectKind Kind, string Label, string Icon)> Items =
    [
        (ObjectKind.Mesh, "Mesh object", "view_in_ar_outlined"),
        (ObjectKind.Light, "Light", "wb_sunny_outlined"),
        (ObjectKind.Camera, "Camera", "videocam_outlined"),
        (ObjectKind.Group, "Group", "folder_outlined"),
        (ObjectKind.Weather, "Weather", "cloud_outlined"),
    ];
}
