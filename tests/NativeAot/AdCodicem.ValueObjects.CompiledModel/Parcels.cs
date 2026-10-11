namespace AdCodicem.ValueObjects.CompiledModel;

// Entities keyed by value objects, beside the shipment: a Guid key and an int key the model generates, and a Guid key
// nothing generates, which the convention refuses unset. None is sealed: a query Entity Framework Core 10 precompiles
// casts each entity it materializes to an interface of its own, which the compiler refuses for a sealed class (CS0030).
// The Guid value object is the domain's customer identifier, so that no value object is added to the domain the native
// AOT application serializes for an Entity Framework Core check alone.
#pragma warning disable CA1852 // Sealing them is what breaks the precompiled queries of the native AOT variant.

/// <summary>A parcel, whose key over a <see cref="Guid"/> is generated as SQL Server generates a <see cref="Guid"/> key.</summary>
public class Parcel
{
    public CustomerId Id { get; set; }
}

/// <summary>A consignment, whose key over a <see cref="Guid"/> nothing generates.</summary>
public class Consignment
{
    public CustomerId Id { get; set; }
}

/// <summary>A page, whose key over an <see cref="int"/>, counted from one, is the database's identity.</summary>
public class Page
{
    public PageNumber Id { get; set; }
}

#pragma warning restore CA1852
