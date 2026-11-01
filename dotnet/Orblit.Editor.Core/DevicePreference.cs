namespace Orblit.Editor.Core;

using IoFile = System.IO.File;
using Orblit.Json;

/// <summary>
/// The device the game view shows, kept in a file in the project's editor folder. It is not in the
/// scene, because a device is a way of looking at the game and is not part of it.
/// </summary>
public sealed class DevicePreference
{
    private readonly string _file;

    // How a phone or a tablet was last held. A trip through the desktop does not forget it.
    private FrameOrientation _held = FrameOrientation.Portrait;

    public DevicePreference(string file)
    {
        _file = file;
        Frame = DeviceFrame.Desktop;
        try
        {
            if (IoFile.Exists(file) && JsonText.Decode(IoFile.ReadAllText(file)) is IReadOnlyDictionary<string, object?> raw)
            {
                Read(raw);
            }
        }
        catch (Exception error) when (error is JsonFormatException or IOException or UnauthorizedAccessException)
        {
            // A damaged file must not stop the project opening, so the game view starts on the desktop.
        }
    }

    /// <summary>Raised after the frame changes.</summary>
    public event Action? Changed;

    public DeviceFrame Frame { get; private set; }

    /// <summary>Shows <paramref name="kind"/>, held the way a phone or a tablet was last held.</summary>
    public void Choose(DeviceKind kind) => Set(new DeviceFrame(kind, _held));

    /// <summary>Holds the device the other way. A desktop has one way, so nothing happens.</summary>
    public void Turn() => Set(Frame.Turned());

    /// <summary>Holds the device <paramref name="orientation"/>. A desktop has one way, so nothing happens.</summary>
    public void Hold(FrameOrientation orientation) => Set(new DeviceFrame(Frame.Kind, orientation));

    private void Set(DeviceFrame next)
    {
        if (next == Frame)
        {
            return;
        }

        Frame = next;
        if (next.CanTurn)
        {
            _held = next.Orientation;
        }

        Write();
        Changed?.Invoke();
    }

    private void Read(IReadOnlyDictionary<string, object?> raw)
    {
        if (raw.GetValueOrDefault("device") is string device
            && Enum.TryParse<DeviceKind>(device, ignoreCase: true, out var kind)
            && Enum.IsDefined(kind)
            && raw.GetValueOrDefault("orientation") is string orientation
            && Enum.TryParse<FrameOrientation>(orientation, ignoreCase: true, out var held)
            && Enum.IsDefined(held))
        {
            Frame = new DeviceFrame(kind, held);
            _held = held;
        }
    }

    private void Write()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_file))!); // A full path to a file always has a folder.
            var pending = _file + ".tmp";
            IoFile.WriteAllText(
                pending,
                JsonText.Encode(
                    new OrderedDictionary<string, object?>
                    {
                        ["device"] = Frame.Kind.ToString().ToLowerInvariant(),
                        ["orientation"] = _held.ToString().ToLowerInvariant(),
                    }));
            IoFile.Move(pending, _file, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // shortcut: a choice that cannot be written holds for this run only.
        }
    }
}
