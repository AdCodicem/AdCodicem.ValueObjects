using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.Fixtures.GraphQL;

/// <summary>A stock keeping unit, declared beside the source-generated query type.</summary>
[ValueObject<string>(MaxLength = 10)]
public readonly partial struct Sku : IValueObjectValidator<string>
{
    /// <inheritdoc />
    public static ValidationResult ValidateValue(in string value)
        => value.Contains('-', StringComparison.Ordinal) ? ValidationResult.Success : ValidationResult.InvalidFormat("A SKU holds a dash.");
}

/// <summary>A count of items, from one.</summary>
[ValueObject<int>]
public readonly partial struct ItemCount : IValueObjectMinimum<int>
{
    /// <inheritdoc />
    public static int Minimum => 1;
}
