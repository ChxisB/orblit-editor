using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TinyGame;

// A control made from XAML, which includes a style of the game's own by an avares address.
public sealed partial class Faces : UserControl
{
    public Faces() => AvaloniaXamlLoader.Load(this);
}
