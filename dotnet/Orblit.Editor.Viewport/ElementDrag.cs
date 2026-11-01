namespace Orblit.Editor.Viewport;

using Orblit.Editor.Core;
using Orblit.Mesh;

/// <summary>One frame of a drag on the parts of a mesh, which the shell writes as a step to undo.</summary>
/// <param name="Mesh">The whole mesh as the drag has taken it.</param>
/// <param name="Selection">What is selected now. An extrude replaces it with the faces it made.</param>
/// <param name="What">What the step is called: "Move", "Turn" or "Extrude".</param>
/// <param name="Merge">Whether it folds into the step the first frame began.</param>
public sealed record ElementDrag(Mesh Mesh, ElementSelection Selection, string What, bool Merge);
