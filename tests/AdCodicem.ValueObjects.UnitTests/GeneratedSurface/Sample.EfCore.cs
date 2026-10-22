using AdCodicem.ValueObjects.UnitTests.Persistence;

namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

// The efcore family's part of Sample (GeneratedSurface/Sample.cs): the checks that hold every sample to Entity
// Framework Core.

public abstract partial class Sample
{
    /// <summary>
    /// Stores the value object in a list, an array and a list of its optional type, which the Entity Framework Core
    /// convention maps as primitive collections, on SQLite in memory, and checks that each element gets the converter,
    /// the comparer and the length of the value object, and that the collections round-trip, a null element included.
    /// </summary>
    public abstract void RoundTripsThroughAPrimitiveCollection();

    /// <summary>
    /// Queries the value object's <c>.Value</c> through Entity Framework Core under <c>UseValueObjectQueries()</c>, on SQLite
    /// in memory and in Npgsql's query text, and checks that it translates as the same query over a property of the
    /// underlying type does.
    /// </summary>
    public abstract void TranslatesItsValueInAQueryAsItsUnderlyingValue();
}

public partial class Sample<TSelf, TValue>
{
    public override void RoundTripsThroughAPrimitiveCollection() => PrimitiveCollectionParity.Check<TSelf, TValue>(Small, Large);

    public override void TranslatesItsValueInAQueryAsItsUnderlyingValue() => ValueQueryParity.Check<TSelf, TValue>(Small, Large);
}
