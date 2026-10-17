using AdCodicem.ValueObjects.Metadata;
using Orleans.Serialization;

namespace AdCodicem.ValueObjects.Orleans;

/// <summary>
/// Allows every value object through Orleans' type filter, and has no opinion on any other type.
/// </summary>
/// <remarks>
/// Orleans names a value object on the wire whenever its type is not the member's, as for a grain method's result, which
/// travels inside a response whose type names it, a <c>TSelf?</c> or a member typed <see cref="object"/>, and refuses a
/// type nothing allows. A construction of a generic value object is allowed whatever its type argument, which only names
/// it: Orleans then allows the construction unless another filter refuses that argument.
/// </remarks>
internal sealed class ValueObjectTypeFilter : ITypeFilter
{
    /// <inheritdoc />
    public bool? IsTypeAllowed(Type type) => ValueObjectRegistry.IsValueObject(type) ? true : null;
}
