using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Editor.Viewport;
using Orblit.Weather;

/// <summary>
/// What the air is doing, in two sections: the weather, which is the sky and what falls out of it, and
/// the air, which is the haze between the viewer and everything else.
/// </summary>
internal sealed class WeatherSection(Fields fields, InspectorTarget target)
{
    public Control Weather()
    {
        var rows = new List<Control>();
        rows.AddRange(Conditions());
        rows.AddRange(Clouds());
        rows.AddRange(Falling());
        rows.AddRange(Wind());
        rows.Add(Fields.Note(WeatherModel.Caption(target.Item.Weather, WeatherModel.IsSpare(target.Scene, target.Item))));
        return fields.Section("Weather", Fields.Column(rows));
    }

    public Control Air()
    {
        var air = target.Item.Weather;
        var rows = new List<Control>
        {
            fields.Palette(
                "Colour",
                Colour.FromTint(air.FogColour),
                colour => Change(state => state with { FogColour = colour.ToTint() })),
            Value("Density", air.FogDensity, (state, value) => state with { FogDensity = value }, new SliderOptions { Min = 0, Max = 0.4, Decimals = 3 }),
        };
        if (air.FogDensity > 0)
        {
            rows.AddRange(Haze());
        }

        rows.Add(Fields.Note(WeatherModel.AirNote(air.Mist)));
        return fields.Section("Air", Fields.Column(rows));
    }

    // Each change reads the weather as it stands, so a drag that merges into one step still ends where the hand did.
    private void Change(Func<WeatherState, WeatherState> change) =>
        target.History.Run(new SetWeatherValues(target.SceneId, target.Item.Id, target.Item.Weather, change(target.Item.Weather)));

    private Control Value(string label, double current, Func<WeatherState, double, WeatherState> set, SliderOptions options) =>
        fields.Slider(label, current, value => Change(state => set(state, value)), options);

    private List<Control> Conditions()
    {
        var item = target.Item;
        return [.. Enumerable.Range(0, 2).Select(row => fields.Choice(
            row == 0 ? "Condition" : "",
            WeatherModel.ConditionRow(row),
            item.Condition.Label(),
            chosen =>
            {
                var wanted = WeatherModel.ConditionFor(chosen);
                if (wanted != item.Condition)
                {
                    target.History.Run(new SetWeatherCondition(target.SceneId, item.Id, item.Condition, wanted, item.Weather, WeatherState.Of(wanted)));
                }
            }))];
    }

    private List<Control> Clouds()
    {
        var item = target.Item;
        var rows = new List<Control>
        {
            Value("Cloud", item.Weather.CloudCover, (state, value) => state with { CloudCover = value }, new SliderOptions { Min = 0, Max = 1, Decimals = 2 }),
        };
        // Nothing to shape while there is no cloud.
        if (item.Weather.CloudCover <= 0.01)
        {
            return rows;
        }

        rows.AddRange(Enumerable.Range(0, 2).Select(row => fields.Choice(
            row == 0 ? "Cloud kind" : "",
            WeatherModel.CloudRow(row),
            WeatherModel.CloudLabel(item.CloudKind, row),
            chosen => ChooseCloud(WeatherModel.CloudKindFor(chosen)))));
        rows.Add(Value("Cloud height", item.Weather.CloudHeight, (state, value) => state with { CloudHeight = value }, new SliderOptions { Min = 40, Max = 8000, Unit = " m" }));
        return rows;
    }

    private void ChooseCloud(CloudKind? wanted)
    {
        var item = target.Item;
        if (wanted != item.CloudKind)
        {
            target.History.Run(new SetCloudKind(target.SceneId, item.Id, item.CloudKind, wanted, item.Weather.CloudHeight, CloudShapes.StartHeight(wanted)));
        }
    }

    private List<Control> Falling()
    {
        var air = target.Item.Weather;
        var unit = new SliderOptions { Min = 0, Max = 1, Decimals = 2 };
        return
        [
            Value("Rain", air.Rain, (state, value) => state with { Rain = value }, unit),
            Value("Snow", air.Snow, (state, value) => state with { Snow = value }, unit),
            Value("Lightning", air.Lightning, (state, value) => state with { Lightning = value }, unit),
        ];
    }

    private List<Control> Wind()
    {
        var item = target.Item;
        void Set(Func<WindValues, WindValues> change)
        {
            var now = new WindValues(item.WindDirection, item.TransitionSeconds);
            target.History.Run(new SetWeatherWind(target.SceneId, item.Id, now, change(now)));
        }

        return
        [
            Value("Wind", item.Weather.WindSpeed, (state, value) => state with { WindSpeed = value }, new SliderOptions { Min = 0, Max = 25, Decimals = 1, Unit = " m/s" }),
            fields.Slider("Bearing", item.WindDirection, value => Set(wind => wind with { Direction = value }), new SliderOptions { Min = 0, Max = 360, Unit = "°" }),
            fields.Slider("Changes over", item.TransitionSeconds, value => Set(wind => wind with { Transition = value }), new SliderOptions { Min = 0, Max = 60, Decimals = 1, Unit = " s" }),
        ];
    }

    private List<Control> Haze()
    {
        var air = target.Item.Weather;
        var rows = new List<Control>
        {
            Value("Height", air.FogHeight, (state, value) => state with { FogHeight = value }, new SliderOptions { Min = -20, Max = 20, Decimals = 1, Unit = " m" }),
            Value("Falloff", air.FogFalloff, (state, value) => state with { FogFalloff = value }, new SliderOptions { Min = 0.02, Max = 2, Decimals = 2 }),
            Value("Ground mist", air.Mist, (state, value) => state with { Mist = value }, new SliderOptions { Min = 0, Max = 1, Decimals = 2 }),
        };
        if (air.Mist > 0)
        {
            rows.Add(Value("Mist size", air.MistSize, (state, value) => state with { MistSize = value }, new SliderOptions { Min = 2, Max = 120, Unit = " m" }));
        }

        return rows;
    }
}
