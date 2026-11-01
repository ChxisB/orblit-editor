using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Light;
using Orblit.Weather;

/// <summary>
/// A light's fields. Which of them are shown depends on the kind: a spot has a cone, a sun has neither
/// cone nor falloff, and the power control is in the unit the kind is actually measured in.
/// </summary>
internal static class LightSection
{
    public static Control Build(Fields fields, InspectorTarget target)
    {
        var item = target.Item;
        var type = item.LightType;
        var rows = new List<Control>();

        // The one light everything is lit from above by. A renderer draws one, so being that light is a
        // property of the scene and not of the object: the first directional light is it.
        if (type == LightType.Sun && ReferenceEquals(item, target.Scene.Celestial))
        {
            rows.AddRange(Celestial(fields, target));
        }

        rows.Add(Type(fields, target));
        rows.Add(fields.Palette(
            "Colour",
            item.Colour,
            value => target.History.Run(new SetColour(target.SceneId, item.Id, item.Name, item.Colour, value))));
        rows.Add(Power(fields, target));
        if (type == LightType.Spot)
        {
            rows.AddRange(Cone(fields, target));
        }

        rows.Add(Size(fields, target));
        rows.Add(InspectorSections.OffOn(
            fields,
            "Cast shadows",
            item.CastShadows,
            wanted => new SetCastShadows(target.SceneId, item.Id, item.Name, wanted)));
        rows.Add(Fields.Note(LightModel.Note(type)));
        return fields.Section("Light", Fields.Column(rows));
    }

    private static List<Control> Celestial(Fields fields, InspectorTarget target)
    {
        var item = target.Item;
        var scene = target.Scene;
        var bodies = Enum.GetValues<CelestialBody>();
        var rows = new List<Control>
        {
            // Nothing to choose while the day is running: it is above the horizon or it is not, and a
            // control that fights the clock is a control that loses.
            fields.Choice(
                "Body",
                [.. bodies.Select(body => body.Label())],
                scene.ActiveBody.Label(),
                scene.DayCycle
                    ? null
                    : chosen =>
                    {
                        var wanted = bodies.First(body => body.Label() == chosen);
                        if (wanted != item.Body)
                        {
                            target.History.Run(new SetCelestialBody(target.SceneId, item.Id, item.Name, item.Body, wanted));
                        }
                    }),
        };
        if (scene.DayCycle)
        {
            rows.Add(Fields.Note(
                "The day cycle is deciding: whichever body is above the horizon lights the scene, and its colour, strength and direction come from the hour."));
        }

        return rows;
    }

    private static Control Type(Fields fields, InspectorTarget target)
    {
        var item = target.Item;
        var type = item.LightType;
        return fields.Choice(
            "Type",
            [.. LightModel.Names.Select(one => one.Value)],
            LightModel.Names.First(one => one.Key == type).Value,
            chosen =>
            {
                var wanted = LightModel.Names.First(one => one.Value == chosen).Key;
                if (wanted == type)
                {
                    return;
                }

                // The number means something different on the other side of this change, so it is
                // restated and not carried: a sun is watts per square metre and the rest are watts,
                // and a thousand of the second is a hundred suns.
                target.History.Run(new SetLightType(
                    target.SceneId,
                    item.Id,
                    item.Name,
                    type,
                    wanted,
                    item.Power,
                    LightModel.PowerFor(wanted, type, item.Power)));
            });
    }

    private static Control Power(Fields fields, InspectorTarget target)
    {
        var item = target.Item;
        var isSun = item.LightType == LightType.Sun;
        return fields.Slider(
            "Power",
            item.Power,
            value => target.History.Run(new SetPower(target.SceneId, item.Id, item.Name, item.Power, value)),
            new SliderOptions
            {
                Min = 0,
                Max = isSun ? 400 : 5000,
                Decimals = isSun ? 1 : 0,
                Unit = isSun ? " W/m²" : " W",
                Trailing = InspectorSections.KeyFor(target, "light.power"),
            });
    }

    private static Control[] Cone(Fields fields, InspectorTarget target)
    {
        var item = target.Item;
        return
        [
            fields.Slider(
                "Cone",
                item.SpotSize,
                value => Reshape(target, shape => shape with { Size = value }),
                new SliderOptions { Min = 1, Max = 180, Unit = "°" }),
            fields.Slider(
                "Blend",
                item.SpotBlend,
                value => Reshape(target, shape => shape with { Blend = value }),
                new SliderOptions { Min = 0, Max = 1, Decimals = 2 }),
        ];
    }

    private static Control Size(Fields fields, InspectorTarget target)
    {
        var item = target.Item;
        if (item.LightType == LightType.Sun)
        {
            return fields.Slider(
                "Sun size",
                item.SunAngle,
                value => Reshape(target, shape => shape with { Sun = value }),
                new SliderOptions { Min = 0, Max = 12, Decimals = 2, Unit = "°" });
        }

        var isArea = item.LightType == LightType.Area;
        return fields.Slider(
            isArea ? "Size" : "Radius",
            item.SourceRadius,
            value => Reshape(target, shape => shape with { Radius = value }),
            new SliderOptions { Min = 0, Max = isArea ? 4 : 1, Decimals = 2, Unit = " m" });
    }

    /// <summary>Runs one shape edit, keeping the values that were not touched.</summary>
    private static void Reshape(InspectorTarget target, Func<LightShape, LightShape> change)
    {
        var was = LightModel.ShapeOf(target.Item);
        target.History.Run(new SetLightShape(target.SceneId, target.Item.Id, target.Item.Name, was, change(was)));
    }
}
