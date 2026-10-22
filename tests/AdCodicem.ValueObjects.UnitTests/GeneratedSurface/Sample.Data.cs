using AdCodicem.ValueObjects.UnitTests.Persistence;

namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

// The data family's part of Sample (GeneratedSurface/Sample.cs): the checks that hold every sample to MongoDB and
// Marten.

public abstract partial class Sample
{
    /// <summary>
    /// Stores the value object through MongoDB.Driver, with the serializer <c>ValueObjectBson.Register</c> hands it, and
    /// checks that it writes and reads back what the bare value it carries writes and reads.
    /// </summary>
    public abstract void RoundTripsThroughBsonAsItsUnderlyingValue();

    /// <summary>
    /// Queries the value object through Marten under <c>UseValueObjects()</c>, offline, and checks that a comparison of it
    /// and of its nullable type, and a <c>Contains</c> over values of it, translate as the same queries over a member of
    /// its underlying type do, the same parameters included; or, over an underlying type Marten maps to no column, that
    /// both are refused the same way.
    /// </summary>
    public abstract void QueriesInADocumentStoreAsItsUnderlyingValue();
}

public partial class Sample<TSelf, TValue>
{
    public override void RoundTripsThroughBsonAsItsUnderlyingValue()
    {
        MongoDbParity.Check<TSelf, TValue>(Small);
        MongoDbParity.Check<TSelf, TValue>(Large);
    }

    public override void QueriesInADocumentStoreAsItsUnderlyingValue() => MartenParity.Check<TSelf, TValue>(Small, Large);
}
