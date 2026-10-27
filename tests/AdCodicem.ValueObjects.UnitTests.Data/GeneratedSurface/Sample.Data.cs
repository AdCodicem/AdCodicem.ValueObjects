using AdCodicem.ValueObjects.UnitTests.Persistence;

namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

// The data family's part of Sample (GeneratedSurface/Sample.cs): the check that holds every sample to MongoDB.

public abstract partial class Sample
{
    /// <summary>
    /// Stores the value object through MongoDB.Driver, with the serializer <c>ValueObjectBson.Register</c> hands it, and
    /// checks that it writes and reads back what the bare value it carries writes and reads.
    /// </summary>
    public abstract void RoundTripsThroughBsonAsItsUnderlyingValue();
}

public partial class Sample<TSelf, TValue>
{
    public override void RoundTripsThroughBsonAsItsUnderlyingValue()
    {
        MongoDbParity.Check<TSelf, TValue>(Small);
        MongoDbParity.Check<TSelf, TValue>(Large);
    }
}
