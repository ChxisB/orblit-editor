using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Stage;
using IoPath = System.IO.Path;

/// <summary>
/// The inspector's section for an object wearing an author-written shader.
/// </summary>
/// <remarks>
/// Offers the uniforms the author marked <c>instance</c>, as the control their hints ask for: a
/// colour for <c>source_color</c>, a slider for <c>hint_range</c>. Pictures are named in the scene
/// file, and there is no picker for one yet.
/// </remarks>
internal static class ShaderSection
{
    public static bool AppliesTo(InspectorTarget target) => target.Item.WearsSurface;

    public static Control Build(Fields fields, SurfaceCooks cooks, InspectorTarget target)
    {
        // A shader is a material asset, and AppliesTo has already said this one is.
        var asset = target.Item.MaterialAsset!;
        var cooked = cooks.CookedOf(asset);
        var problem = cooks.ProblemWith(asset);
        var offered = (cooked?.Params ?? []).Where(param => param.Instance && param.Type != "sampler2D").ToList();
        var rows = new List<Control> { Fields.Text("File", IoPath.GetFileName(asset)) };
        if (problem is not null)
        {
            rows.Add(Fields.Note(problem, OrblitColors.Bad));
        }
        else if (cooked is null)
        {
            rows.Add(Fields.Note("Cooking the shader."));
        }
        else if (offered.Count == 0)
        {
            rows.Add(Fields.Note("Nothing in this shader is set for each object."));
        }

        if (cooked is not null)
        {
            var shown = new ShaderRows(fields, target, cooked);
            rows.AddRange(offered.Select(shown.Row));
        }

        return fields.Section("Shader", Fields.Column(rows));
    }
}

/// <summary>One control for each uniform a shader lets an object state.</summary>
internal sealed class ShaderRows(Fields fields, InspectorTarget target, CookedSurface cooked)
{
    public Control Row(CookedParam param)
    {
        var width = cooked.Widths.GetValueOrDefault(param.Name, 1);
        if (param.IsColour && width >= 3)
        {
            return ColourRow(param);
        }

        if (param.Type == "bool")
        {
            return InspectorSections.OffOn(fields, param.Name, Read(param)[0] != 0, wanted => Command(param, [wanted ? 1.0 : 0.0]));
        }

        if (param.Range is { } range && width == 1)
        {
            return SliderRow(param, range);
        }

        var whole = param.Type == "int";
        return fields.Drag(
            param.Name,
            () => Read(param),
            values => target.History.Run(Command(param, values)),
            new DragOptions { Step = whole ? 1 : 0.01, Decimals = whole ? 0 : 2 });
    }

    private static int Byte(double channel) => (int)Math.Round(Math.Clamp(channel, 0, 1) * 255);

    private Control ColourRow(CookedParam param)
    {
        var read = Read(param);
        var alpha = read.Count == 4;
        var shown = Colour.FromArgb(alpha ? Byte(read[3]) : 255, Byte(read[0]), Byte(read[1]), Byte(read[2]));
        return fields.Palette(
            param.Name,
            shown,
            picked => target.History.Run(Command(param, alpha ? [picked.R, picked.G, picked.B, picked.A] : [picked.R, picked.G, picked.B])));
    }

    private Control SliderRow(CookedParam param, (double Min, double Max, double Step) range) => fields.Slider(
        param.Name,
        Read(param)[0],
        value =>
        {
            var snapped = range.Step == 0
                ? value
                : range.Min + (Math.Round((value - range.Min) / range.Step, MidpointRounding.AwayFromZero) * range.Step);
            target.History.Run(Command(param, [snapped]));
        },
        new SliderOptions { Min = range.Min, Max = range.Max, Decimals = param.Type == "int" ? 0 : 2 });

    private IReadOnlyList<double> Read(CookedParam param) => cooked.ValueOf(param.Name, target.Item.MaterialValues);

    /// <summary>States <paramref name="values"/> for <paramref name="param"/>, in the shape a scene file keeps that type in.</summary>
    private SetMaterialValue Command(CookedParam param, double[] values)
    {
        var from = target.Item.MaterialValues;
        object stored = param.Type switch
        {
            "float" => values[0],
            "int" => (long)Math.Round(values[0], MidpointRounding.AwayFromZero),
            "bool" => values[0] != 0,
            _ => values.Select(value => (object?)value).ToList(),
        };
        return new SetMaterialValue(
            target.SceneId,
            target.Item.Id,
            target.Item.Name,
            param.Name,
            from,
            new OrderedDictionary<string, object>(from) { [param.Name] = stored });
    }
}
