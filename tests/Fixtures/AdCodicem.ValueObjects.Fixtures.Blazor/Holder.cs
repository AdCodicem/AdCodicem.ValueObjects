namespace AdCodicem.ValueObjects.Fixtures.Blazor;

/// <summary>The model of a form of one field, the value object or its nullable type.</summary>
/// <typeparam name="TValue">The type of the field.</typeparam>
public sealed class Holder<TValue>
{
    /// <summary>Gets or sets the value of the field.</summary>
    public TValue? Value { get; set; }
}
