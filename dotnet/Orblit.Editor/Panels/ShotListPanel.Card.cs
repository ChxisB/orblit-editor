using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Motion;

internal sealed partial class ShotListPanel
{
    private const string LookTip = "Fly the scene view to this camera, and steer the camera by moving the view.";

    private const string StopTip = "Stop steering this camera from the scene view.";

    private Control CardOf(Card card) => _fields.Section(
        $"Shot {card.Index + 1}",
        Fields.Column(
        [
            CameraRow(card),
            TimeRow(card, new TimeField("Starts at", "start", card.Shot.Start, text => TypeStart(card, text))),
            TimeRow(card, new TimeField("Length", "length", card.Shot.Duration, text => TypeLength(card, text))),
            Buttons(card),
        ]));

    private Grid CameraRow(Card card)
    {
        var camera = _session.Workspace.Loaded?.Scene?[card.Shot.Camera];
        var picker = Dropdown.Of(
            $"shot_{card.Index}_camera",
            camera?.Name ?? card.Shot.Camera,
            () => Choices(card),
            camera is null ? OrblitColors.Warn : null);
        if (camera is null)
        {
            ToolTip.SetTip(picker, "This scene has no such camera.");
        }

        return Fields.Row("Camera", picker, name: $"shot_{card.Index}_camera_row");
    }

    private IEnumerable<(string Text, Action Choose)> Choices(Card card) =>
        Cameras().Select(camera => (
            (camera.Id == card.Shot.Camera ? "✓ " : "    ") + camera.Name,
            (Action)(() => _host.Apply(() => Replace("Change shot camera", card, card.Shot.CopyWith(camera: camera.Id), typing: false)))));

    // A number of seconds typed into a card: what it is called, where it is named, and what it is now.
    private sealed record TimeField(string Label, string Part, double Seconds, Action<string> OnTyped);

    private Grid TimeRow(Card card, TimeField field)
    {
        var name = $"shot_{card.Index}_{field.Part}";
        var box = _fields.Box(name, field.Seconds.ToString("0.###", CultureInfo.InvariantCulture), field.OnTyped);
        var unit = new TextBlock { Text = "s", FontSize = OrblitText.Caption, Foreground = OrblitColors.InkDim, VerticalAlignment = VerticalAlignment.Center };
        return Fields.Row(field.Label, box, unit, $"{name}_row");
    }

    private Grid Buttons(Card card)
    {
        var looking = _session.Piloting?.Camera == card.Shot.Camera;
        var look = Fields.Button(
            looking ? "Stop looking" : "Look through",
            looking ? StopTip : LookTip,
            () => _host.Apply(() => _session.LookThrough(looking ? null : card.Shot.Camera)));
        look.Name = $"shot_{card.Index}_look";
        look.IsEnabled = looking || _session.Workspace.Loaded?.Scene?[card.Shot.Camera] is not null;
        if (looking)
        {
            look.Background = OrblitColors.EmberWash;
            look.Foreground = OrblitColors.Ember;
        }

        var delete = Fields.Button("Delete", "Take this shot out of the cutscene.", () => _host.Apply(() => Replace("Delete shot", card, null, typing: false)));
        delete.Name = $"shot_{card.Index}_delete";
        Grid.SetColumn(delete, 1);
        delete.Margin = new Thickness(Space.Xs, 0, 0, 0);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Margin = new Thickness(0, 6), Children = { look, delete } };
    }

    private void TypeStart(Card card, string text)
    {
        if (Seconds(text) is { } at and >= 0 and <= Limit && at != card.Shot.Start)
        {
            Replace("Move shot", card, card.Shot.CopyWith(start: at), typing: true);
        }
    }

    private void TypeLength(Card card, string text)
    {
        if (Seconds(text) is { } length and > 0 and <= Limit && length != card.Shot.Duration)
        {
            Replace("Change shot length", card, card.Shot.CopyWith(duration: length), typing: true);
        }
    }

    private static double? Seconds(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ? seconds : null;

    // Swaps the card's shot for <next>, or takes it out when next is null. A card the cutscene has moved
    // on from, by an undo while a box had focus, does nothing.
    private void Replace(string label, Card card, CutsceneShot? next, bool typing)
    {
        var was = card.Shot;
        if (_session.Cutscenes.Shown?.Shots?.Contains(was) != true)
        {
            return;
        }

        if (next is not null)
        {
            card.Shot = next;
        }

        _session.Cutscenes.EditShots(
            label,
            all => all.Select(one => ReferenceEquals(one, was) ? next : one).OfType<CutsceneShot>(),
            typing ? card.Gesture : null);
    }
}
