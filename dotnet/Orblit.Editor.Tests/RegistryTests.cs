namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

public sealed class RegistryTests
{
    private readonly Registry<Thing> _registry = new();

    private string[] Names() => [.. _registry.All.Select(thing => thing.Name)];

    [Fact]
    public void KeepsThingsInTheOrderTheyWereRegistered()
    {
        _registry.Register(new Thing("outliner"));
        _registry.Register(new Thing("scene"));
        _registry.Register(new Thing("inspector"));

        Assert.Equal(["outliner", "scene", "inspector"], Names());
    }

    [Fact]
    public void FindsAThingByItsNameAndNothingByAnyOther()
    {
        var scene = new Thing("scene");
        _registry.Register(scene);

        Assert.Same(scene, _registry["scene"]);
        Assert.Null(_registry["terrain"]);
    }

    [Fact]
    public void PutsAThingAheadOfTheOneItIsRegisteredBefore()
    {
        _registry.Register(new Thing("light"));
        _registry.Register(new Thing("data"));
        _registry.Register(new Thing("shape"), before: "data");

        Assert.Equal(["light", "shape", "data"], Names());
    }

    [Fact]
    public void RegisteredBeforeSomethingMissingItGoesLast()
    {
        _registry.Register(new Thing("light"));
        _registry.Register(new Thing("shape"), before: "data");

        Assert.Equal(["light", "shape"], Names());
    }

    [Fact]
    public void RefusesASecondThingUnderANameAlreadyTaken()
    {
        _registry.Register(new Thing("scene"));

        Assert.Throws<InvalidOperationException>(() => _registry.Register(new Thing("scene")));
        Assert.Equal(["scene"], Names());
    }

    [Fact]
    public void CannotBeChangedThroughWhatItHandsOut()
    {
        _registry.Register(new Thing("scene"));

        Assert.Throws<NotSupportedException>(() => ((IList<Thing>)_registry.All).Add(new Thing("game")));
    }

    private sealed record Thing(string Name) : IRegistered;
}
