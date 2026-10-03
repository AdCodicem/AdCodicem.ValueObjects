using Microsoft.EntityFrameworkCore;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Decides what a converter writes for a value object: its value, or nothing, when the value object rejects it.
/// </summary>
/// <remarks>
/// <para>
/// Only an instance equal to <c>default(TSelf)</c> can hold a value its type rejects: any other went through
/// <c>Create</c>, whose value is normalized and valid by construction. So only that one is validated, as its value
/// stands, and any other costs the comparison <c>IsDefault</c> makes. Over a value type, a constructed zero equals the
/// default too, and validation tells a valid zero, which is written, from a refused one.
/// </para>
/// <para>
/// A refused value is never stored: a read trusts what it finds, and would hand back an instance holding it.
/// </para>
/// <para>
/// A design-time tool, such as <c>dotnet ef dbcontext optimize</c>, converts the default of every property, which
/// Entity Framework Core takes for its sentinel, to write it into a compiled model as a provider value. That writes
/// nothing to a column, so under <see cref="EF.IsDesignTime"/> the value is handed over as it stands.
/// </para>
/// </remarks>
internal static class ProviderValue
{
    /// <summary>
    /// Gives the value a column that takes no <c>NULL</c> stores.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="valueObject">Value object being written.</param>
    /// <returns>Its value.</returns>
    /// <exception cref="ValueObjectException">The value object rejects the value it holds.</exception>
    public static TValue Required<TSelf, TValue>(TSelf valueObject)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var validation = Validate<TSelf, TValue>(valueObject);
        if (!validation.IsValid && !EF.IsDesignTime)
        {
            throw new ValueObjectException(
                $"The value to write is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}",
                typeof(TSelf),
                validation.ErrorCode,
                attemptedValue: null);
        }

        return valueObject.Value;
    }

    /// <summary>
    /// Tells whether a column that takes a <c>NULL</c> stores one in place of the value a value object holds.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="valueObject">Value object being written.</param>
    /// <returns><see langword="true"/> when the value object rejects the value it holds.</returns>
    public static bool IsRefused<TSelf, TValue>(TSelf valueObject)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => !Validate<TSelf, TValue>(valueObject).IsValid;

    private static ValidationResult Validate<TSelf, TValue>(TSelf valueObject)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        if (!valueObject.IsDefault)
        {
            return ValidationResult.Success;
        }

        var value = valueObject.Value;

        return TSelf.Validate(in value);
    }
}
