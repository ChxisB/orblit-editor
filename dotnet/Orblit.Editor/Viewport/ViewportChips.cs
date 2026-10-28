using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orblit.Editor;

using Orblit.Editor.Viewport;

/// <summary>One chip over the view: its words, whether it is lit, and what a tap does.</summary>
internal sealed record ChipSpec(string Text, bool On = false, string? Tip = null, Action? Tap = null);

/// <summary>The small labels along the top of the view, and the words they use.</summary>
internal static class ViewportChips
{
    private static readonly IBrush Idle = new ImmutableSolidColorBrush(Color.FromUInt32(0xCC161A21));

    /// <summary>A grid step as somebody would say it: millimetres below a centimetre, centimetres below a metre.</summary>
    public static string GridLabel(double step)
    {
        if (step < 0.01)
        {
            return $"{Rounded(step * 1000)}mm";
        }

        if (step < 1)
        {
            return $"{Rounded(step * 100)}cm";
        }

        var decimals = step % 1 == 0 ? "0" : "0.0";
        return step.ToString(decimals, CultureInfo.InvariantCulture) + "m";
    }

    /// <summary>The value with its sign shown, so a change reads as a change.</summary>
    public static string Signed(double value, int decimals)
    {
        var text = value.ToString("F" + decimals, CultureInfo.InvariantCulture);
        return text.StartsWith('-') ? text : "+" + text;
    }

    /// <summary>The rate, and the slower half of a frame with which half it is.</summary>
    public static string Frames(FrameReading? reading)
    {
        if (reading is not { } read)
        {
            return "— fps";
        }

        var ms = read.GpuBound ? read.GpuMs : read.BuildMs;
        return $"{Rounded(read.Fps)} fps · {ms.ToString("0.0", CultureInfo.InvariantCulture)} ms {(read.GpuBound ? "gpu" : "cpu")}";
    }

    public static Border Build(ChipSpec spec)
    {
        var chip = new Border
        {
            Background = spec.On ? OrblitColors.EmberWash : Idle,
            BorderBrush = spec.On ? OrblitColors.Ember : OrblitColors.LineSoft,
            BorderThickness = new Thickness(1),
            CornerRadius = Radii.Control,
            Padding = new Thickness(Space.Sm, 3),
            Margin = new Thickness(0, 0, Space.Xs, Space.Xs),
            Child = new TextBlock
            {
                Text = spec.Text,
                FontSize = 11,
                Foreground = spec.On ? OrblitColors.Ember : OrblitColors.InkMid,
            },
        };

        if (spec.Tip is not null)
        {
            ToolTip.SetTip(chip, spec.Tip);
        }

        if (spec.Tap is { } tap)
        {
            chip.Cursor = new Cursor(StandardCursorType.Hand);
            // Handled, so a tap on a chip is not also a tap on the scene behind it.
            chip.PointerPressed += (_, e) =>
            {
                e.Handled = true;
                tap();
            };
        }

        return chip;
    }

    // Away from zero at the half, as the Dart view rounds.
    private static string Rounded(double value) =>
        Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
}
