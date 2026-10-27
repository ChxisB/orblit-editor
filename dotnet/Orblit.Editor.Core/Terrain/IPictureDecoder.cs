namespace Orblit.Editor.Core;

/// <summary>
/// What the terrain bench needs to turn a picture file into the pixels the renderer takes. The window
/// supplies it, so the core does not depend on an image library.
/// </summary>
public interface IPictureDecoder
{
    /// <summary>
    /// The picture in <paramref name="file"/> as <paramref name="side"/> by <paramref name="side"/>
    /// RGBA bytes with straight alpha, stretched to fit. Called off the UI thread.
    /// </summary>
    /// <exception cref="InvalidDataException">The bytes are not a picture it can read.</exception>
    byte[] Decode(byte[] file, int side);
}
