using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// The picture that stands in for a scene on a card: sky over floor, with a glow where the light is.
/// It is decoration, so its colours are not theme tokens.
/// </summary>
/// <param name="Sky">The upper part.</param>
/// <param name="Floor">The lower part, from a little over half way down.</param>
/// <param name="Glow">The light, at its middle.</param>
/// <param name="GlowAt">Where the glow sits, from 0 to 1 across and down.</param>
public sealed record SceneArt(Color Sky, Color Floor, Color Glow, Point GlowAt)
{
    private static readonly SceneArt Empty = new(Color.FromUInt32(0xFF171C25), Color.FromUInt32(0xFF12161D), Color.FromUInt32(0x0FFFFFFF), new Point(0.7, 0.4));
    private static readonly SceneArt Warm = new(Color.FromUInt32(0xFF1B2130), Color.FromUInt32(0xFF141922), Color.FromUInt32(0x73E5893F), new Point(0.72, 0.42));
    private static readonly SceneArt Green = new(Color.FromUInt32(0xFF17241F), Color.FromUInt32(0xFF111A16), Color.FromUInt32(0x4D7FD083), new Point(0.62, 0.44));
    /// <summary>A project whose folder has gone: a blank where the picture was.</summary>
    public static readonly SceneArt Missing = new(Color.FromUInt32(0xFF0E1116), Color.FromUInt32(0xFF0E1116), Colors.Transparent, new Point(0.5, 0.5));

    private static readonly SceneArt[] Projects =
    [
        Warm,
        new(Color.FromUInt32(0xFF241C2B), Color.FromUInt32(0xFF171320), Color.FromUInt32(0x66B58AF0), new Point(0.3, 0.4)),
        Green,
        new(Color.FromUInt32(0xFF2A2119), Color.FromUInt32(0xFF1B1611), Color.FromUInt32(0x66E5893F), new Point(0.68, 0.44)),
        new(Color.FromUInt32(0xFF16212D), Color.FromUInt32(0xFF111820), Color.FromUInt32(0x6162A0E8), new Point(0.34, 0.42)),
        Empty,
    ];

    /// <summary>The same name always draws the same picture, so a card is known by its look.</summary>
    public static SceneArt ForName(string name)
    {
        long hash = 0;
        foreach (var unit in name)
        {
            hash = (hash * 31 + unit) & 0x7fffffff;
        }

        return Projects[(int)(hash % Projects.Length)];
    }

    public static SceneArt ForTemplate(ProjectTemplate template) => template switch
    {
        ProjectTemplate.Empty => Empty,
        ProjectTemplate.Scene => Warm,
        ProjectTemplate.ThirdPerson => Green,
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    /// <summary>The picture as a control of the given height, with <paramref name="overlay"/> over it.</summary>
    public Border Picture(double height, Control? overlay = null)
    {
        var sky = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Sky, 0),
                new GradientStop(Sky, 0.56),
                new GradientStop(Floor, 0.56),
                new GradientStop(Floor, 1),
            ],
        };
        var glow = new RadialGradientBrush
        {
            Center = new RelativePoint(GlowAt, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(GlowAt, RelativeUnit.Relative),
            RadiusX = RelativeScalar.Parse("80%"),
            RadiusY = RelativeScalar.Parse("80%"),
            GradientStops = [new GradientStop(Glow, 0), new GradientStop(Color.FromArgb(0, Glow.R, Glow.G, Glow.B), 1)],
        };
        return new Border
        {
            Height = height,
            CornerRadius = Radii.Card,
            ClipToBounds = true,
            Background = sky,
            Child = new Border { Background = glow, Child = overlay },
        };
    }
}
