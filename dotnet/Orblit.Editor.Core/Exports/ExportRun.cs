namespace Orblit.Editor.Core;

using Orblit.Asset;
using IoDirectory = System.IO.Directory;
using IoPath = System.IO.Path;

/// <summary>What to export: which targets, and whether each is a whole pack or a patch.</summary>
public sealed record ExportRequest
{
    public required string ProjectDirectory { get; init; }

    /// <summary>Target names, each cooked and packed in turn.</summary>
    public required IReadOnlyList<string> Targets { get; init; }

    /// <summary>
    /// The pack in the exports folder a patch is built on, or null for a whole pack. A patch is for the one
    /// target that pack is for.
    /// </summary>
    public string? PatchOf { get; init; }

    /// <summary>Where cooked bytes are kept, or null for the machine's own cache.</summary>
    public string? CachePath { get; init; }
}

/// <summary>How one target's export ended.</summary>
public enum ExportOutcome
{
    /// <summary>A pack was written.</summary>
    Packed,

    /// <summary>The cook worked and the result is what the exports folder already holds, so nothing was written.</summary>
    Unchanged,

    /// <summary>An asset did not cook. Nothing was packed for the target.</summary>
    CookFailed,

    /// <summary>Something else stopped it, such as a missing folder or a bad base.</summary>
    Failed,
}

/// <summary>An asset that did not cook, and what the importer said.</summary>
public sealed record ExportFailure(string Id, string Reason);

/// <summary>What exporting one target came to, as plain values.</summary>
public sealed record TargetExport
{
    public required string Target { get; init; }

    public required ExportOutcome Outcome { get; init; }

    /// <summary>One sentence for an outcome that is not <see cref="ExportOutcome.Packed"/>.</summary>
    public string Message { get; init; } = "";

    /// <summary>The cook's own count, such as <c>3 cooked, 1 cached</c>.</summary>
    public string Cooked { get; init; } = "";

    public IReadOnlyList<ExportFailure> Failures { get; init; } = [];

    /// <summary>The file written, or for <see cref="ExportOutcome.Unchanged"/> the file that already says the same.</summary>
    public string? Path { get; init; }

    public PackSummary? Summary { get; init; }

    /// <summary>The file name of the pack this is a patch of, or null for a whole pack.</summary>
    public string? PatchOf { get; init; }
}

/// <summary>Cooking a project and packing it into its exports folder.</summary>
public static class ExportRun
{
    /// <summary>Cooks the project for each of the request's targets and packs the result.</summary>
    /// <remarks>
    /// Each target runs on the thread pool, so the result of one is on screen while the next cooks.
    /// <paramref name="onTarget"/> is told as each lands, on the caller's thread. <paramref name="cook"/>
    /// is how one target is run, and a test passes one that fails.
    /// </remarks>
    public static async Task<IReadOnlyList<TargetExport>> RunAsync(
        ExportRequest request,
        Action<TargetExport>? onTarget = null,
        Func<ExportRequest, string, Task<TargetExport>>? cook = null)
    {
        cook ??= OnThePool;
        List<TargetExport> done = [];
        foreach (var target in request.Targets)
        {
            // No ConfigureAwait(false), so onTarget runs where the caller does and may touch the window.
            var result = await AttemptAsync(cook, request, target);
            done.Add(result);
            onTarget?.Invoke(result);
        }

        return done;
    }

    // Dart ran each target in an isolate, which also kept a cook that ran out of memory from taking the
    // editor down. A task keeps the window drawing but shares the editor's process, so it does not.
    private static Task<TargetExport> OnThePool(ExportRequest request, string target) =>
        Task.Run(() => ExportTargetAsync(request, target));

    private static async Task<TargetExport> AttemptAsync(
        Func<ExportRequest, string, Task<TargetExport>> cook, ExportRequest request, string target)
    {
        try
        {
            return await cook(request, target);
        }
        catch (Exception error)
        {
            // A cook passed in may throw before it reaches the catch in ExportTargetAsync.
            return new TargetExport
            {
                Target = target,
                Outcome = ExportOutcome.Failed,
                Message = $"The cook for {target} stopped before it finished: {error.Message}",
            };
        }
    }

    /// <summary>Cooks and packs one target.</summary>
    /// <remarks>Never throws. Whatever goes wrong comes back as a value that says so, because a person waiting on a dialog reads it.</remarks>
    public static async Task<TargetExport> ExportTargetAsync(ExportRequest request, string name)
    {
        if (CookTargets.Find(name) is not { } target)
        {
            return new TargetExport
            {
                Target = name,
                Outcome = ExportOutcome.Failed,
                Message = $"Orblit cooks for {string.Join(", ", CookTargets.Names)}. \"{name}\" is not one.",
            };
        }

        try
        {
            return await ExportAsync(request, target).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            // Anything thrown inside must become a value, or the dialog waits on a task that failed.
            return new TargetExport { Target = name, Outcome = ExportOutcome.Failed, Message = error.Message };
        }
    }

    private static async Task<TargetExport> ExportAsync(ExportRequest request, CookTarget target)
    {
        var assets = IoPath.Join(request.ProjectDirectory, "assets");
        if (!IoDirectory.Exists(assets))
        {
            return new TargetExport
            {
                Target = target.Name,
                Outcome = ExportOutcome.Failed,
                Message = "The project has no assets folder, so there is nothing to pack.",
            };
        }

        var project = new CookProject(assets, IoPath.Join(request.ProjectDirectory, "build", "assets"), target, request.CachePath);
        var report = await project.RunAsync().ConfigureAwait(false);
        if (!report.Ok)
        {
            return CookFailed(target, report);
        }

        var placing = new Placing(ExportFolder.PathOf(request.ProjectDirectory), target, report.Summary);
        var summary = await PackAsync(request, project, placing.Pending).ConfigureAwait(false);
        return request.PatchOf is { } patchOf ? placing.Patch(summary, patchOf) : await placing.WholeAsync(summary).ConfigureAwait(false);
    }

    private static TargetExport CookFailed(CookTarget target, CookReport report) => new()
    {
        Target = target.Name,
        Outcome = ExportOutcome.CookFailed,
        Cooked = report.Summary,
        Message = $"Nothing was packed for {target.Name}.",
        Failures = [.. report.WithStatus(CookStatus.Failed).Select(result => new ExportFailure(result.Id.ToString(), result.Error?.Message ?? ""))],
    };

    // The exports folder is open only for the length of the pack. Packing reads each base pack's
    // directory and never its blobs.
    private static async Task<PackSummary> PackAsync(ExportRequest request, CookProject project, string pending)
    {
        if (request.PatchOf is not { } path)
        {
            return await PackBundle.PackAsync(project.BundlePath, pending, project.Target).ConfigureAwait(false);
        }

        await using var folder = await OpenedExports.OpenAsync(request.ProjectDirectory).ConfigureAwait(false);
        var top = folder.At(path) ?? throw new ExportChainBrokenException(
            $"{IoPath.GetFileName(path)} is not a readable pack in the exports folder.");
        return await PackBundle.PackAsync(project.BundlePath, pending, project.Target, folder.ChainTo(top)).ConfigureAwait(false);
    }

    /// <summary>A pack written under a temporary name, and where it goes.</summary>
    /// <remarks>
    /// A whole pack is <c>game.&lt;target&gt;.opack</c>, the one an app ships. A patch, and a whole pack that
    /// a newer one replaces, are kept as <c>game.&lt;target&gt;.&lt;hash&gt;.opack</c>. Nothing is lost that a
    /// patch may still be built on, since a patch finds its base by hash and not by name.
    /// </remarks>
    private sealed class Placing
    {
        private readonly string _folder;
        private readonly CookTarget _target;
        private readonly string _cooked;

        public Placing(string folder, CookTarget target, string cooked)
        {
            _folder = folder;
            _target = target;
            _cooked = cooked;
            IoDirectory.CreateDirectory(folder);
            Pending = IoPath.Join(folder, $"{PackName.FileName(target)}.pending");
        }

        public string Pending { get; }

        /// <summary>Puts a whole pack where the app reads it, setting the one it replaces aside under its hash.</summary>
        public async Task<TargetExport> WholeAsync(PackSummary summary)
        {
            var current = IoPath.Join(_folder, PackName.FileName(_target));
            var old = await HashOfAsync(current).ConfigureAwait(false);
            if (old == summary.Hash)
            {
                File.Delete(Pending);
                return Result(ExportOutcome.Unchanged, current) with { Message = "The pack is the same as the last export." };
            }

            if (old is not null)
            {
                // The same hash is the same bytes, so a pack already set aside under it is replaced by its twin.
                File.Move(current, Named(old), overwrite: true);
            }

            File.Move(Pending, current);
            return Result(ExportOutcome.Packed, current) with { Summary = summary };
        }

        /// <summary>Keeps a patch under its own hash, or nothing when it holds nothing.</summary>
        public TargetExport Patch(PackSummary summary, string basedOn)
        {
            var name = IoPath.GetFileName(basedOn);
            if (summary.Assets == 0 && summary.Removed == 0)
            {
                File.Delete(Pending);
                return Result(ExportOutcome.Unchanged, null) with { Message = $"Nothing has changed since {name}.", PatchOf = name };
            }

            var destination = Named(summary.Hash);
            File.Move(Pending, destination, overwrite: true);
            return Result(ExportOutcome.Packed, destination) with { Summary = summary, PatchOf = name };
        }

        private string Named(ContentHash hash) => IoPath.Join(_folder, $"game.{_target.Name}.{ExportFolder.ShortHash(hash)}.opack");

        private TargetExport Result(ExportOutcome outcome, string? path) =>
            new() { Target = _target.Name, Outcome = outcome, Cooked = _cooked, Path = path };

        private static async Task<ContentHash?> HashOfAsync(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            await using var bytes = new FilePackBytes(path);
            return (await AssetPack.OpenAsync(bytes).ConfigureAwait(false)).Hash;
        }
    }
}
