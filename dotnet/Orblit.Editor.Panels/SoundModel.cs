namespace Orblit.Editor.Panels;

using Orblit.Scene;

/// <summary>What the sound section decides about a source, apart from drawing its fields.</summary>
public static class SoundModel
{
    /// <summary>
    /// How close the near and far ends of a source may come, so that dragging one past the other leaves
    /// a range and not an error in the player.
    /// </summary>
    public const double Gap = 0.1;

    /// <summary>
    /// <paramref name="source"/> with its near end at <paramref name="near"/>, held below the far end. A
    /// range narrower than twice the gap keeps the near end above zero.
    /// </summary>
    public static SoundSourceComponent WithNear(SoundSourceComponent source, double near) =>
        source.CopyWith(near: Math.Min(near, source.Far - Math.Min(Gap, source.Far / 2)));

    /// <summary><paramref name="source"/> with its far end at <paramref name="far"/>, held above the near end.</summary>
    public static SoundSourceComponent WithFar(SoundSourceComponent source, double far) =>
        source.CopyWith(far: Math.Max(far, source.Near + Gap));

    /// <summary>
    /// <paramref name="source"/> with the clip <paramref name="text"/>, or with none for an empty one,
    /// since a source with no clip is how one is placed before its sound is chosen.
    /// </summary>
    public static SoundSourceComponent WithClip(SoundSourceComponent source, string text) => text.Length > 0
        ? source.CopyWith(clip: text)
        : new SoundSourceComponent(
            bus: source.Bus,
            loop: source.Loop,
            volume: source.Volume,
            near: source.Near,
            far: source.Far,
            curve: source.Curve);
}
