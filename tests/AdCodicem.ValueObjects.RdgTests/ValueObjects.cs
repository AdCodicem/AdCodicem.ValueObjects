namespace AdCodicem.ValueObjects.RdgTests;

// Value objects declared in the project that maps the endpoints. The Request Delegate Generator compiles beside the
// generator and never sees what it adds, so each lists its contract on its declaration, the part the RDG reads: the
// one interface of each kind the ASP.NET Core guide names. VO0033 fails the build of this project for one that does not.

/// <summary>A stock-keeping unit, which holds a dash.</summary>
[ValueObject<string>(MaxLength = 10)]
public readonly partial struct Sku : IValueObject<Sku, string>, IValueObjectValidator<string>
{
    public static ValidationResult ValidateValue(in string value)
        => value.Contains('-', StringComparison.Ordinal)
            ? ValidationResult.Success
            : ValidationResult.InvalidFormat("A SKU holds a dash.");
}

/// <summary>A quantity, an arithmetic value object, of at most a hundred.</summary>
[ValueObject<int>(Arithmetic = true)]
public readonly partial struct Quantity : INumericValueObject<Quantity, int>, IValueObjectMinimum<int>, IValueObjectMaximum<int>
{
    public static int Minimum => 1;

    public static int Maximum => 100;
}

/// <summary>The public identifier of a customer.</summary>
[EntityId("cus")]
public readonly partial struct CustomerId : IEntityId<CustomerId>;

/// <summary>A code whose owner is part of its type.</summary>
/// <typeparam name="TOwner">What the code identifies.</typeparam>
[ValueObject<string>(MaxLength = 5)]
public readonly partial struct Code<TOwner> : IValueObject<Code<TOwner>, string>
    where TOwner : class;

/// <summary>What a <see cref="Code{TOwner}"/> identifies in the endpoints.</summary>
public sealed class Warehouse;

/// <summary>A type holding a value object nested in it.</summary>
public static partial class Catalog
{
    /// <summary>A shelf of the catalog, a letter followed by a digit.</summary>
    [ValueObject<string>(MinLength = 2, MaxLength = 2)]
    public readonly partial struct Shelf : IValueObject<Shelf, string>;
}
