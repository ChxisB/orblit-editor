using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>What a menu on one file can do to it. Build is null for a file that cannot be built.</summary>
internal sealed record AssetActions(Action Open, Action Rename, Action Delete, Action? Build);

/// <summary>
/// The menu an asset offers, which is the same one wherever it is raised from.
/// </summary>
/// <remarks>
/// What somebody can make does not depend on whether the pointer happened to be over a file when they
/// asked. What a tile adds is what can be done to it, at the top where it reads first.
/// </remarks>
internal static class AssetMenu
{
    public static ContextMenu Build(Action<NewAsset> create, AssetActions? on = null)
    {
        var menu = new ContextMenu();
        if (on is not null)
        {
            // Build first, because somebody who right-clicked a script wants to know whether it compiles
            // more often than they want to rename it.
            if (on.Build is not null)
            {
                menu.Items.Add(Command("Build", on.Build));
            }

            menu.Items.Add(Command("Open", on.Open));
            menu.Items.Add(Command("Rename", on.Rename));
            menu.Items.Add(Command("Delete", on.Delete));
            menu.Items.Add(new Separator());
        }

        menu.Items.Add(Make(NewAsset.Folder, create));
        menu.Items.Add(new Separator());
        foreach (var group in NewAssetGroup.Values)
        {
            var submenu = new MenuItem { Header = group.Label, Tag = group };
            foreach (var what in group.Members)
            {
                submenu.Items.Add(Make(what, create));
            }

            menu.Items.Add(submenu);
        }

        menu.Items.Add(new Separator());
        foreach (var what in NewAssetGroup.Loose.Where(one => one != NewAsset.Folder))
        {
            menu.Items.Add(Make(what, create));
        }

        return menu;
    }

    private static MenuItem Command(string label, Action act)
    {
        var item = new MenuItem { Header = label, Tag = label };
        item.Click += (_, _) => act();
        return item;
    }

    private static MenuItem Make(NewAsset what, Action<NewAsset> create)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Md };
        row.Children.Add(new TextBlock { Text = what.Label, FontSize = OrblitText.Label });
        // The extension is what somebody is really choosing between, so it is shown and not guessed
        // from the name.
        if (what.Extension.Length > 0)
        {
            row.Children.Add(new TextBlock { Text = what.Extension, FontSize = OrblitText.Caption, Foreground = OrblitColors.InkDim });
        }

        var item = new MenuItem { Header = row, Tag = what };
        item.Click += (_, _) => create(what);
        return item;
    }
}
