namespace Orblit.Editor.Core;

/// <summary>What the view shows of a model's skeleton.</summary>
/// <param name="Bones">Draws the bones of a selected model, at rest or in the clip's pose.</param>
/// <param name="Onion">
/// Draws the pose at moments either side of the playhead as well. Only shown with bones, since there is
/// nothing to ghost without them.
/// </param>
/// <param name="Before">How many ghosts come from before the playhead.</param>
/// <param name="After">How many come from after it.</param>
/// <param name="Stride">Frames between one ghost and the next.</param>
public sealed record SkeletonSettings(bool Bones = false, bool Onion = false, int Before = 2, int After = 2, int Stride = 2)
{
    /// <summary>Bones on or off. Taking the bones away takes the ghosts with them.</summary>
    public SkeletonSettings WithBonesToggled() => this with { Bones = !Bones, Onion = Bones ? false : Onion };

    /// <summary>Onion skin on or off. Turning it on brings the bones, since there is nothing to ghost without them.</summary>
    public SkeletonSettings WithOnionToggled() => this with { Bones = !Onion || Bones, Onion = !Onion };
}

/// <summary>
/// The skeleton settings the timeline's chips write and the view reads, kept in one place so neither
/// has to know the other.
/// </summary>
public sealed class SkeletonLook
{
    private SkeletonSettings _value = new();

    public event Action? Changed;

    public SkeletonSettings Value
    {
        get => _value;
        set
        {
            if (_value == value)
            {
                return;
            }

            _value = value;
            Changed?.Invoke();
        }
    }
}
