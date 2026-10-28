namespace Orblit.Editor.Core;

using Doc = Orblit.Scene;

// Prefabs: made from an object, placed, and kept in step with the file every instance of one came from.
//
// An instance is a link and what is different about it, and all the working out of that lives in the
// engine. What is here is the editor's side: which file, which scene, what to say, and every change made
// as one step that can be undone. Each is done at rest, off the clip's pose: a prefab made or applied
// mid-clip would otherwise keep wherever the playhead was.
public sealed partial class EditorSession
{
    // The outermost instance an id is or is part of, and where it is.
    private sealed record Instance(SceneEntry Entry, EditorScene Scene, SceneObject Head, string Asset);

    /// <summary>
    /// Saves object <paramref name="id"/> and everything under it as a prefab in <paramref name="directory"/>.
    /// The object becomes the first instance of it, or the thing on screen would look like the prefab while
    /// quietly not being one.
    /// </summary>
    public void MakePrefab(string id, string directory) => AtRest(() =>
    {
        if (Prefabs is not { } files || Workspace.SceneHolding(id) is not { Scene: { } scene } open || scene[id] is not { } made)
        {
            return;
        }

        if (made.Kind == ObjectKind.Scene)
        {
            Say("A scene is already a file. Save it instead.", SayLevel.Info);
            return;
        }

        var path = AssetTree.Available(directory, made.Name + PrefabFiles.Extension);
        var asset = Assets.Relative(path);
        Doc.PrefabEdit edit;
        try
        {
            edit = Doc.Instances.MakePrefab(SceneDocument.DocumentOf(scene), id, asset, files.Find, made.Name);
        }
        catch (Doc.PrefabException error)
        {
            Say(error.Message, SayLevel.Info);
            return;
        }

        if (files.Write(asset, edit.Prefab) is { } problem)
        {
            Say($"Could not save the prefab: {problem}", SayLevel.Error);
            return;
        }

        ChangeScene(open, edit.Document, $"Make prefab {made.Name}");
        Follow(edit.Renamed);
        Say($"Saved {Path.GetFileName(path)}. {made.Name} is now an instance of it.", SayLevel.Info);
        Report(edit.Problems);
    });

    /// <summary>
    /// Puts an instance of the prefab at <paramref name="path"/> into the scene being worked in, under the
    /// selected group or beside the selected object, like anything else added.
    /// </summary>
    public void PlacePrefab(string path)
    {
        if (Prefabs is not { } files)
        {
            return;
        }

        if (Working is not { Scene: { } scene } open)
        {
            Say("There is no scene loaded to add to.", SayLevel.Warning);
            return;
        }

        var asset = Assets.Relative(path);
        var read = files.Read(asset);
        if (read.Prefab is not { } prefab)
        {
            Say(read.Problem ?? $"{Path.GetFileName(path)} is not a readable prefab.", SayLevel.Info);
            return;
        }

        var selected = Primary is { } primary ? scene[primary] : null;
        var parent = selected is null ? null : selected.Kind == ObjectKind.Group ? selected.Id : selected.ParentId;

        // Placed folded and opened at once, so a new instance is made exactly the way one read from a
        // file is and cannot come out any different.
        var id = NextObjectId();
        var before = SceneDocument.DocumentOf(scene);
        var link = new Doc.SceneEntity(
            id,
            UniqueName(scene, prefab.Name),
            parent,
            components: new Dictionary<string, Doc.SceneComponent> { [Doc.SceneComponents.Prefab] = new Doc.PrefabComponent(asset: asset) });
        var placed = Doc.Instances.Expand(before.CopyWith(entities: [.. before.Entities, link]), files.Find, [id]);

        ChangeScene(open, placed.Document, $"Place {prefab.Name}");
        Report(placed.Problems);
        SelectObject(id);
    }

    /// <summary>
    /// Writes what instance <paramref name="id"/> looks like now into its prefab, and brings every other
    /// instance of it up to date, in this scene and in every other one in memory. Each keeps what was
    /// changed about it: a lamp somebody made red stays red when the lamp post grows a second arm.
    /// </summary>
    public void ApplyPrefab(string id) => AtRest(() =>
    {
        if (Prefabs is not { } files || InstanceAt(files, id) is not { } instance)
        {
            return;
        }

        var (entry, scene, head, asset) = instance;
        // What every other instance was opened against, needed to work out what each of them had
        // changed before the prefab changes under them.
        var was = files.Find(asset);
        Doc.PrefabEdit edit;
        try
        {
            edit = Doc.Instances.Apply(SceneDocument.DocumentOf(scene), head.Id, files.Find);
        }
        catch (Doc.PrefabException error)
        {
            Say(error.Message, SayLevel.Info);
            return;
        }

        if (files.Write(asset, edit.Prefab) is { } problem)
        {
            Say(problem, SayLevel.Error);
            return;
        }

        var here = InstancesUsing(scene, asset, except: head.Id);
        ChangeScene(entry, edit.Document, $"Apply {head.Name}");
        Follow(edit.Renamed);
        var (elsewhere, problems) = RefreshOthers(files, instance, was, edit.Prefab.Name);
        var touched = here + elsewhere;
        var file = Path.GetFileName(asset);
        Say(touched == 0 ? $"Saved {file}." : $"Saved {file} and updated {touched} other instance{(touched == 1 ? "" : "s")}.", SayLevel.Info);
        Report([.. edit.Problems, .. problems]);
    });

    // Brings the instances of an applied prefab in every other scene in memory up to date, and counts them.
    // The shared set is one of them. Left on the old prefab, it would be saved with the old parts as its
    // changes, and the apply would be undone in every scene.
    private (int Count, List<string> Problems) RefreshOthers(PrefabFiles files, Instance applied, Doc.PrefabDocument? was, string name)
    {
        var count = 0;
        var problems = new List<string>();
        foreach (var other in Workspace.Entries.Append(Workspace.SharedEntry))
        {
            if (ReferenceEquals(other, applied.Entry) || other.Scene is not { } scene)
            {
                continue;
            }

            var held = InstancesUsing(scene, applied.Asset);
            if (held == 0)
            {
                continue;
            }

            var refreshed = Doc.Instances.Refresh(SceneDocument.DocumentOf(scene), applied.Asset, files.WithOne(applied.Asset, was), files.Find);
            ChangeScene(other, refreshed.Document, $"Update {name}");
            problems.AddRange(refreshed.Problems);
            count += held;
        }

        return (count, problems);
    }

    /// <summary>
    /// Throws away what instance <paramref name="id"/> changed about its prefab. It keeps where it stands,
    /// and whatever was hung on it from outside stays hung on it.
    /// </summary>
    public void RevertPrefab(string id) => AtRest(() =>
    {
        if (Prefabs is not { } files || InstanceAt(files, id) is not { } instance)
        {
            return;
        }

        var reverted = Doc.Instances.Revert(SceneDocument.DocumentOf(instance.Scene), instance.Head.Id, files.Find);
        ChangeScene(instance.Entry, reverted.Document, $"Revert {instance.Head.Name}");
        Report(reverted.Problems);
    });

    /// <summary>
    /// Cuts instance <paramref name="id"/> loose from its prefab: its parts become ordinary objects, with
    /// ids of their own, that the prefab no longer changes.
    /// </summary>
    public void UnpackPrefab(string id) => AtRest(() =>
    {
        if (Prefabs is not { } files || InstanceAt(files, id) is not { } instance)
        {
            return;
        }

        var (document, renamed) = Doc.Instances.Unpack(SceneDocument.DocumentOf(instance.Scene), instance.Head.Id, files.Find, NextObjectId);
        ChangeScene(instance.Entry, document, $"Unpack {instance.Head.Name}");
        Follow(renamed);
    });

    // A part answers for the outermost instance it is in: that is the one the scene links to, and a
    // prefab inside another is changed by opening the outer one. An instance whose prefab cannot be read
    // is only a link until it can be, so that is said and nothing is found.
    private Instance? InstanceAt(PrefabFiles files, string id)
    {
        if (Workspace.SceneHolding(id) is not { Scene: { } scene } entry
            || scene[Doc.EntityPath.InstanceOf(id) ?? id] is not { Prefab.Asset: { } asset } head)
        {
            return null;
        }

        if (head.Prefab.State != Doc.PrefabState.Open)
        {
            Say(
                files.Read(asset).Problem is { } problem
                    ? $"{problem} Until it is, {head.Name} is only a link."
                    : $"{head.Name} has not been opened. Reload the scene.",
                SayLevel.Info);
            return null;
        }

        return new Instance(entry, scene, head, asset);
    }

    // How many outermost instances in a scene use the asset, directly or by holding a prefab that does.
    private static int InstancesUsing(EditorScene scene, string asset, string? except = null) =>
        scene.Objects
            .Where(one => one.Prefab?.Asset == asset && (except is null || !Doc.EntityPath.Within(except, one.Id)))
            .Select(one => Doc.EntityPath.InstanceOf(one.Id) ?? one.Id)
            .Distinct()
            .Count();

    // Makes the scene in an entry into another document, as one step that can be undone.
    private void ChangeScene(SceneEntry entry, Doc.SceneDocument after, string label)
    {
        if (entry.Scene is not { } scene || Doc.SceneDiff.Between(SceneDocument.DocumentOf(scene), after) is not { IsEmpty: false } diff)
        {
            return;
        }

        Run(new ApplySceneDiff(entry.Id, label, diff));
    }

    // Carries the selection, and the object the clip plays on, across ids that changed.
    private void Follow(IReadOnlyDictionary<string, string> renamed)
    {
        if (renamed.Count == 0)
        {
            return;
        }

        Choose([.. Selected.Select(one => renamed.GetValueOrDefault(one, one))], Primary is { } primary ? renamed.GetValueOrDefault(primary, primary) : null);
        if (Clips.Owner is { } owner && renamed.TryGetValue(owner, out var now))
        {
            Clips.Owner = now;
        }
    }
}
