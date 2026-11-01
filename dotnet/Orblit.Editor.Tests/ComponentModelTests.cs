namespace Orblit.Editor.Tests;

using Orblit.Editor.Panels;
using Orblit.Scene;
using Orblit.VectorMath;

/// <summary>The sound, particle and zone sections' decisions, with no control in sight.</summary>
public sealed class ComponentModelTests
{
    private static readonly SoundSourceComponent Source = new(near: 2, far: 10);

    private static readonly ParticleEmitterComponent Emitter = new(lifetimeMin: 1, lifetimeMax: 2, speedMin: 3, speedMax: 4);

    [Fact]
    public void TheNearEndStaysBelowTheFarEnd()
    {
        Assert.Equal(9.9, SoundModel.WithNear(Source, 50).Near, 9);
        Assert.Equal(3, SoundModel.WithNear(Source, 3).Near);
    }

    [Fact]
    public void ARangeNarrowerThanTheGapStillLeavesTheNearEndBelow()
    {
        var narrow = new SoundSourceComponent(near: 0.01, far: 0.05);

        Assert.Equal(0.025, SoundModel.WithNear(narrow, 1).Near, 9);
    }

    [Fact]
    public void TheFarEndStaysAboveTheNearEnd()
    {
        Assert.Equal(2.1, SoundModel.WithFar(Source, 0.5).Far, 9);
        Assert.Equal(40, SoundModel.WithFar(Source, 40).Far);
    }

    [Fact]
    public void AnEmptyClipIsNoClipAndTheRestStays()
    {
        var named = Source.CopyWith(clip: "a.ogg", bus: "sfx", loop: false);

        var cleared = SoundModel.WithClip(named, "");

        Assert.Null(cleared.Clip);
        Assert.Equal("sfx", cleared.Bus);
        Assert.False(cleared.Loop);
        Assert.Equal(10, cleared.Far);
        Assert.Equal("b.ogg", SoundModel.WithClip(named, "b.ogg").Clip);
    }

    [Fact]
    public void ARangeDraggedPastTheOtherEndTakesItAlong()
    {
        var longer = ParticlesModel.WithLifetimeMin(Emitter, 5);
        Assert.Equal([5, 5], [longer.LifetimeMin, longer.LifetimeMax]);
        var shorter = ParticlesModel.WithLifetimeMax(Emitter, 0.5);
        Assert.Equal([0.5, 0.5], [shorter.LifetimeMin, shorter.LifetimeMax]);
        var faster = ParticlesModel.WithSpeedMin(Emitter, 9);
        Assert.Equal([9, 9], [faster.SpeedMin, faster.SpeedMax]);
        var slower = ParticlesModel.WithSpeedMax(Emitter, 1);
        Assert.Equal([1, 1], [slower.SpeedMin, slower.SpeedMax]);
    }

    [Fact]
    public void ARangeDraggedWithinItLeavesTheOtherEndAlone()
    {
        var longer = ParticlesModel.WithLifetimeMax(Emitter, 6);
        Assert.Equal([1, 6], [longer.LifetimeMin, longer.LifetimeMax]);
        var slower = ParticlesModel.WithSpeedMin(Emitter, 2);
        Assert.Equal([2, 4], [slower.SpeedMin, slower.SpeedMax]);
    }

    [Fact]
    public void GravityIsSetAlongYAndTheSidesAreKept()
    {
        var emitter = new ParticleEmitterComponent(gravity: new Vector3(1, 2, 3));

        Assert.Equal(new Vector3(1, -9, 3), ParticlesModel.WithGravityY(emitter, -9).Gravity);
    }

    [Fact]
    public void OneBoxEdgeIsSetAndTheOthersAreKept()
    {
        var emitter = new ParticleEmitterComponent(extents: new Vector3(1, 2, 3));

        var wider = ParticlesModel.WithExtent(emitter, 1, 7);

        Assert.Equal(new Vector3(1, 7, 3), wider.Extents);
        Assert.Equal(new Vector3(1, 2, 3), emitter.Extents);
    }

    [Fact]
    public void TheShapesAreShownByTheNamesTheyAreKnownBy()
    {
        Assert.Equal(["Point", "Sphere", "Cone", "Box"], ParticlesModel.ShapeLabels);
        Assert.Equal(EmitterShape.Cone, ParticlesModel.ShapeFor("Cone"));
    }

    [Fact]
    public void AZoneDecidesAFieldOnlyWhenAskedAndStartsAtWhatTheBodyHas()
    {
        var zone = new ZoneComponent();
        Assert.False(ZoneModel.Gravity.Owns(zone));

        var own = ZoneModel.Gravity.Take(zone);

        Assert.True(ZoneModel.Gravity.Owns(own));
        Assert.Equal(new Vector3(0, -9.81, 0), own.Gravity);
        Assert.Null(own.LinearDamping);
        Assert.Null(own.AngularDamping);
        Assert.Equal(new BodyComponent().LinearDamping, ZoneModel.Drag.Take(zone).LinearDamping);
        Assert.Equal(new BodyComponent().AngularDamping, ZoneModel.SpinDrag.Take(zone).AngularDamping);
    }

    [Fact]
    public void AZoneGivesAFieldBackToTheBodyAndKeepsTheRest()
    {
        var zone = new ZoneComponent(new Vector3(0, 1, 0), 0.5, 0.25, 3);

        var left = ZoneModel.Drag.Leave(zone);

        Assert.Null(left.LinearDamping);
        Assert.Equal(new Vector3(0, 1, 0), left.Gravity);
        Assert.Equal(0.25, left.AngularDamping);
        Assert.Equal(3, left.Priority);
        Assert.Null(ZoneModel.Gravity.Leave(zone).Gravity);
        Assert.Null(ZoneModel.SpinDrag.Leave(zone).AngularDamping);
    }
}
