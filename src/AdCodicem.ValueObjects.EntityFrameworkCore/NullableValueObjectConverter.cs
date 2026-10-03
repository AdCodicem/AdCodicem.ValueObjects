using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Stores an optional value object over a value type as its bare underlying value, and a value it rejects as
/// <c>NULL</c>.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// <para>
/// The convention maps a <c>TSelf?</c> property through it. Entity Framework Core answers a <c>null</c> itself, in both
/// directions; the converter sees a value object, which it stores as <see cref="ValueObjectConverter{TSelf, TValue}"/>
/// does, but for a value the value object rejects, which only an instance equal to <c>default(TSelf)</c> can hold: the
/// column takes a <c>NULL</c>, so that is what it stores, rather than a value every later read would trust. Over a
/// value type, a constructed zero equals the default too, and validation tells a valid zero, which is stored, from a
/// refused one.
/// </para>
/// <para>
/// Reading uses the trusted factory, as <see cref="ValueObjectConverter{TSelf, TValue}"/> does.
/// <see cref="StrictNullableValueObjectConverter{TSelf, TValue}"/> validates what it reads.
/// </para>
/// </remarks>
public sealed class NullableValueObjectConverter<TSelf, TValue> : ValueConverter<TSelf?, TValue?>
    where TSelf : struct, IValueObject<TSelf, TValue>
    where TValue : struct
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NullableValueObjectConverter{TSelf, TValue}"/> class.
    /// </summary>
    public NullableValueObjectConverter()
        : base(valueObject => ToProvider(valueObject), value => FromProvider(value))
    {
    }

    private static TValue? ToProvider(TSelf? valueObject)
        => valueObject is { } present && !ProviderValue.IsRefused<TSelf, TValue>(present) ? present.Value : null;

    private static TSelf? FromProvider(TValue? value) => value is { } present ? TSelf.CreateUnchecked(present) : null;
}

/// <summary>
/// Stores an optional value object over a value type as its bare underlying value, and a value it rejects as
/// <c>NULL</c>, and re-validates whatever comes back from the database.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// Writes as <see cref="NullableValueObjectConverter{TSelf, TValue}"/> does, and reads as
/// <see cref="StrictValueObjectConverter{TSelf, TValue}"/> does.
/// </remarks>
public sealed class StrictNullableValueObjectConverter<TSelf, TValue> : ValueConverter<TSelf?, TValue?>
    where TSelf : struct, IValueObject<TSelf, TValue>
    where TValue : struct
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StrictNullableValueObjectConverter{TSelf, TValue}"/> class.
    /// </summary>
    public StrictNullableValueObjectConverter()
        : base(valueObject => ToProvider(valueObject), value => FromProvider(value))
    {
    }

    private static TValue? ToProvider(TSelf? valueObject)
        => valueObject is { } present && !ProviderValue.IsRefused<TSelf, TValue>(present) ? present.Value : null;

    private static TSelf? FromProvider(TValue? value) => value is { } present ? TSelf.Create(present) : null;
}

/// <summary>
/// Stores an optional value object over <see cref="string"/> as its bare text, and a value it rejects as <c>NULL</c>.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <remarks>
/// <para>
/// The convention maps a <c>TSelf?</c> property through it, as it maps one over a value type through
/// <see cref="NullableValueObjectConverter{TSelf, TValue}"/>, which it writes and reads as. The value object it
/// rejects is an instance equal to <c>default(TSelf)</c>, whose text is empty.
/// </para>
/// <para>
/// <see cref="StrictNullableValueObjectConverter{TSelf}"/> validates what it reads.
/// </para>
/// </remarks>
public sealed class NullableValueObjectConverter<TSelf> : ValueConverter<TSelf?, string?>
    where TSelf : struct, IValueObject<TSelf, string>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NullableValueObjectConverter{TSelf}"/> class.
    /// </summary>
    public NullableValueObjectConverter()
        : base(valueObject => ToProvider(valueObject), value => FromProvider(value))
    {
    }

    private static string? ToProvider(TSelf? valueObject)
        => valueObject is { } present && !ProviderValue.IsRefused<TSelf, string>(present) ? present.Value : null;

    private static TSelf? FromProvider(string? value) => value is null ? null : TSelf.CreateUnchecked(value);
}

/// <summary>
/// Stores an optional value object over <see cref="string"/> as its bare text, and a value it rejects as <c>NULL</c>,
/// and re-validates whatever comes back from the database.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <remarks>
/// Writes as <see cref="NullableValueObjectConverter{TSelf}"/> does, and reads as
/// <see cref="StrictValueObjectConverter{TSelf, TValue}"/> does.
/// </remarks>
public sealed class StrictNullableValueObjectConverter<TSelf> : ValueConverter<TSelf?, string?>
    where TSelf : struct, IValueObject<TSelf, string>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StrictNullableValueObjectConverter{TSelf}"/> class.
    /// </summary>
    public StrictNullableValueObjectConverter()
        : base(valueObject => ToProvider(valueObject), value => FromProvider(value))
    {
    }

    private static string? ToProvider(TSelf? valueObject)
        => valueObject is { } present && !ProviderValue.IsRefused<TSelf, string>(present) ? present.Value : null;

    private static TSelf? FromProvider(string? value) => value is null ? null : TSelf.Create(value);
}
