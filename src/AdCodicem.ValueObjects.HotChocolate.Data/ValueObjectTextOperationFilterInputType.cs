using HotChocolate.Data.Filters;
using HotChocolate.Types;

namespace AdCodicem.ValueObjects.HotChocolate.Data;

/// <summary>
/// The comparable operations of a value object over <see cref="string"/>, and the string operations over its underlying
/// text.
/// </summary>
/// <typeparam name="TSelf">The value object, over <see cref="string"/>.</typeparam>
/// <remarks>
/// <c>contains</c>, <c>startsWith</c> and <c>endsWith</c>, and their negations, take a <c>String</c>, compared as typed
/// with the underlying text, never normalized; the comparable operations stay the scalar's. Their handlers come with
/// <see cref="ValueObjectFilteringOptions.IncludeTextOperations"/>, in the default filter convention.
/// </remarks>
public class ValueObjectTextOperationFilterInputType<TSelf> : ValueObjectOperationFilterInputType<TSelf>, IValueObjectTextOperationFilterInputType
    where TSelf : struct, IValueObject
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TSelf"/> is not a value object over <see cref="string"/>.
    /// </exception>
    protected override void Configure(IFilterInputTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!typeof(IValueObject<string>).IsAssignableFrom(typeof(TSelf)))
        {
            throw new InvalidOperationException(
                $"'{typeof(TSelf)}' is not a value object over string: its value has no string operation. Bind it to "
                + "ValueObjectOperationFilterInputType<TSelf> instead.");
        }

        base.Configure(descriptor);
        descriptor.Operation(DefaultFilterOperations.Contains).Type<StringType>();
        descriptor.Operation(DefaultFilterOperations.NotContains).Type<StringType>();
        descriptor.Operation(DefaultFilterOperations.StartsWith).Type<StringType>();
        descriptor.Operation(DefaultFilterOperations.NotStartsWith).Type<StringType>();
        descriptor.Operation(DefaultFilterOperations.EndsWith).Type<StringType>();
        descriptor.Operation(DefaultFilterOperations.NotEndsWith).Type<StringType>();
    }
}
