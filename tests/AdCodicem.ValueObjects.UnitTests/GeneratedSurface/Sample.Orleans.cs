using AdCodicem.ValueObjects.Fixtures.Grains;
using AdCodicem.ValueObjects.UnitTests.Grains;

namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

// The orleans family's part of Sample (GeneratedSurface/Sample.cs): the checks that hold every sample to Orleans.

public abstract partial class Sample
{
    /// <summary>
    /// Writes the value object through Orleans, on a serializer registering it through the registry and on one registering
    /// it by its type arguments, trusted, and checks that it writes and reads back what the bare value it carries writes
    /// and reads, in a member typed <see cref="object"/> too, and that each value its rules refuse is refused with its code.
    /// </summary>
    public abstract void RoundTripsThroughOrleansAsItsUnderlyingValue();

    /// <summary>
    /// Sends the value object to a grain and back, as itself, nullable and typed <see cref="object"/>.
    /// </summary>
    /// <param name="grain">A grain of a cluster whose silo and client call <c>AddValueObjects()</c>.</param>
    /// <returns>The check.</returns>
    public abstract Task CrossesAGrainCallAsItselfAsync(IValueObjectGrain grain);
}

public partial class Sample<TSelf, TValue>
{
    public override void RoundTripsThroughOrleansAsItsUnderlyingValue()
    {
        OrleansParity.Check<TSelf, TValue>(Small);
        OrleansParity.Check<TSelf, TValue>(Large);
    }

    public override async Task CrossesAGrainCallAsItselfAsync(IValueObjectGrain grain)
    {
        await OrleansParity.CheckAsync(grain, Small);
        await OrleansParity.CheckAsync(grain, Large);
    }
}
