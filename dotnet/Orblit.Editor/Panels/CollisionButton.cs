using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Asset;

/// <summary>A button that reads geometry when it is pressed, then hands the finished collider on.</summary>
internal static class CollisionButton
{
    public static StackPanel Build(Func<Task<CollisionMesh>> read, Action<CollisionMesh> put)
    {
        var problem = Fields.Note("", OrblitColors.Bad);
        problem.Name = "collision_problem";
        problem.IsVisible = false;
        Button button = null!; // Assigned on the next line, before a press can reach the handler.
        button = Fields.Button("Collide with this model", "Use the model triangles as a fixed collision surface.", () => _ = Collide(button, problem, read, put));
        return Fields.Column([button, problem]);
    }

    private static async Task Collide(Button button, TextBlock problem, Func<Task<CollisionMesh>> read, Action<CollisionMesh> put)
    {
        problem.IsVisible = false;
        button.IsEnabled = false;
        button.Content = "Reading model…";
        try
        {
            var geometry = await read();
            // A press that outlived its panel has nothing to put the collider on.
            if (TopLevel.GetTopLevel(button) is not null)
            {
                put(geometry);
            }
        }
        catch (Exception error)
        {
            // A model that cannot be read is a message for the person, not a crash from a click handler.
            problem.Text = $"Could not make this collider: {error.Message}";
            problem.IsVisible = true;
        }
        finally
        {
            button.IsEnabled = true;
            button.Content = "Collide with this model";
        }
    }
}
