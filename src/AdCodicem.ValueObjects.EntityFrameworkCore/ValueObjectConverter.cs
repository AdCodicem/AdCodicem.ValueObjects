using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Stores a value object as its bare underlying value.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// <para>
/// Reading uses the trusted factory: no normalization, no validation, no allocation beyond the value itself.
/// Materializing an entity is the hottest path in most applications, and the rows being read were written by
/// this same application through the validating factory. Use <see cref="StrictValueObjectConverter{TSelf, TValue}"/>
/// when the table is also written to by something else.
/// </para>
/// <para>
/// Writing refuses a value the value object rejects, with a <see cref="ValueObjectException"/>, rather than store one
/// that every later read would trust. Only an instance equal to <c>default(TSelf)</c> can hold one, since any other
/// went through <c>Create</c>, so only that one is validated: over a value type, a constructed zero equals the default
/// too, and validation tells a valid zero, which is stored, from a refused one. A property holding a <c>TSelf?</c> is
/// mapped through <see cref="NullableValueObjectConverter{TSelf, TValue}"/> or
/// <see cref="NullableValueObjectConverter{TSelf}"/> instead, which stores such a value as <c>NULL</c>.
/// </para>
/// <para>
/// Both directions go through static helpers rather than inline lambdas, because an expression tree cannot
/// contain a call to a static abstract interface member.
/// </para>
/// </remarks>
public sealed class ValueObjectConverter<TSelf, TValue> : ValueConverter<TSelf, TValue>
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectConverter{TSelf, TValue}"/> class.
    /// </summary>
    public ValueObjectConverter()
        : base(valueObject => ToProvider(valueObject), value => FromProvider(value))
    {
    }

    private static TValue ToProvider(TSelf valueObject) => ProviderValue.Required<TSelf, TValue>(valueObject);

    private static TSelf FromProvider(TValue value) => TSelf.CreateUnchecked(value);
}

/// <summary>
/// Stores a value object as its bare underlying value, and re-validates whatever comes back from the database.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// <para>
/// Pays normalization and validation on every materialized row. Worth it when the table is shared with another
/// writer — a legacy application, an ETL job, a migration script — and a row may therefore hold a value the
/// domain would refuse.
/// </para>
/// <para>
/// Writing refuses a value the value object rejects, as <see cref="ValueObjectConverter{TSelf, TValue}"/> does.
/// </para>
/// </remarks>
public sealed class StrictValueObjectConverter<TSelf, TValue> : ValueConverter<TSelf, TValue>
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StrictValueObjectConverter{TSelf, TValue}"/> class.
    /// </summary>
    public StrictValueObjectConverter()
        : base(valueObject => ToProvider(valueObject), value => FromProvider(value))
    {
    }

    private static TValue ToProvider(TSelf valueObject) => ProviderValue.Required<TSelf, TValue>(valueObject);

    private static TSelf FromProvider(TValue value) => TSelf.Create(value);
}
