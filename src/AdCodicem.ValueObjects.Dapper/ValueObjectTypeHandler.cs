using System.Data;
using System.Globalization;
using AdCodicem.ValueObjects.Metadata;
using Dapper;

namespace AdCodicem.ValueObjects.Dapper;

/// <summary>
/// Maps a value object to and from its underlying column value for Dapper.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// <para>
/// Reading uses the trusted factory, on the same reasoning as the Entity Framework Core converter: the rows come
/// from a database this application wrote through the validating factory, and read paths are hot. A column holding
/// text where the underlying type is not text, or the reverse, is the exception: the value object did not write it.
/// Text read into a value object whose underlying type is not <see cref="string"/> is parsed, and so validated, the
/// way the value object parses text. Anything else read into a value object over <see cref="string"/> - a number
/// from a numeric column, a <see cref="Guid"/> from a <c>uuid</c> or <c>uniqueidentifier</c> one - is converted to
/// text, then validated through <c>TryCreate</c>.
/// </para>
/// <para>
/// A parameter declares the column a value object over <see cref="string"/> is mapped to when the value object says
/// more than its underlying type, as the Entity Framework Core conventions map it: an entity identifier as fixed-length,
/// non-Unicode text of its exact length, a value object declaring a maximum length as Unicode text of that length.
/// SQL Server then compares the parameter with the column as it is, with no implicit conversion of the column to cost
/// an index seek. The size is never smaller than the value, which both SqlClient and Npgsql would otherwise truncate.
/// </para>
/// <para>
/// A SQL <c>NULL</c> reads as <see langword="null"/> into an optional value object, <c>TSelf?</c>, and is refused
/// with a <see cref="DataException"/> for a required one in a single-column query, as Dapper refuses it for an
/// <see cref="int"/>. The handler implements <see cref="SqlMapper.ITypeHandler"/> itself for that, since
/// <see cref="SqlMapper.TypeHandler{T}.Parse(object)"/> cannot return <see langword="null"/> for a struct.
/// </para>
/// <para>
/// For a member of a mapped type, or a parameter of the constructor it is mapped through, Dapper checks for a
/// <c>NULL</c> before it calls the handler, and never calls it: a required value object is left uninitialized,
/// <c>default(TSelf)</c>, and nothing throws. A column that can be <c>NULL</c> belongs in a <c>TSelf?</c> member.
/// </para>
/// </remarks>
public sealed class ValueObjectTypeHandler<TSelf, TValue> : SqlMapper.TypeHandler<TSelf>, SqlMapper.ITypeHandler
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>
    /// Full name of the interface every entity identifier implements, in a package this one does not reference.
    /// </summary>
    private const string EntityIdInterface = "AdCodicem.ValueObjects.Identifiers.IEntityId`1";

    /// <summary>
    /// The column type a parameter declares, and its length, or <see langword="null"/> to leave both to the provider.
    /// </summary>
    private static readonly (DbType Type, int Length)? Column = DeclaredColumn();

    /// <inheritdoc />
    /// <exception cref="DataException">
    /// <paramref name="value"/> equals <c>default(TSelf)</c> and holds a value the value object rejects.
    /// </exception>
    /// <remarks>
    /// The handler refuses to write a value the value object rejects, which a read would then trust: only an instance
    /// equal to the default can hold one, since any other went through <c>Create</c>, and over a value type, where a
    /// constructed zero equals the default too, validation tells a valid zero from a refused one. Dapper calls the
    /// handler with the value object alone, whether the parameter is a <typeparamref name="TSelf"/> or a
    /// <c>TSelf?</c> holding one, so it cannot tell a column that takes a <c>NULL</c> from one that does not, and
    /// refuses in both cases: a <c>NULL</c> is written from a <c>TSelf?</c> that holds nothing.
    /// </remarks>
    public override void SetValue(IDbDataParameter parameter, TSelf value)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        if (value.IsDefault)
        {
            var current = value.Value;
            var validation = TSelf.Validate(in current);
            if (!validation.IsValid)
            {
                throw new DataException($"The value to write is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}");
            }
        }

        parameter.Value = value.Value;
        Declare(parameter, value.Value is string text ? text.Length : 0);
    }

    /// <inheritdoc />
    /// <exception cref="DataException">
    /// <paramref name="value"/> is a SQL <c>NULL</c>, which a value object cannot hold, a value that cannot be
    /// converted to <typeparamref name="TValue"/>, or text, or a value converted to text, that the value object
    /// refuses.
    /// </exception>
    public override TSelf Parse(object value)
    {
        if (value is TValue typed)
        {
            return TSelf.CreateUnchecked(typed);
        }

        if (value is null or DBNull)
        {
            throw new DataException(
                $"A NULL cannot be read as {typeof(TSelf).Name}; read the column as a nullable {typeof(TSelf).Name}? instead.");
        }

        // A legacy schema may keep a Guid or a number in a text column. Text goes through the value object's own
        // parser, which validates: unlike a value the provider returns as TValue, text was not necessarily written
        // through the value object, and a rejection says which rule refused it.
        if (value is string text)
        {
            return TSelf.TryParse(text, CultureInfo.InvariantCulture, out var parsed, out var validation)
                ? parsed
                : throw Refused(validation);
        }

        var converted = Convert(value);

        // The reverse case, a number or a Guid kept where text belongs, holds text no value object wrote either.
        if (typeof(TValue) == typeof(string))
        {
            return TSelf.TryCreate(converted, out var created, out var validation)
                ? created
                : throw Refused(validation);
        }

        return TSelf.CreateUnchecked(converted);
    }

    /// <summary>
    /// Declares the column type of a parameter, when the value object says more than its underlying type.
    /// </summary>
    /// <param name="parameter">Parameter carrying the value object.</param>
    /// <param name="length">Length of the text it carries.</param>
    /// <remarks>
    /// A value this application read without validating it may be longer than the value object allows, and the size
    /// grows to carry it whole: providers truncate a value longer than the size of its parameter, silently.
    /// </remarks>
    private static void Declare(IDbDataParameter parameter, int length)
    {
        if (Column is { } column)
        {
            parameter.DbType = column.Type;
            parameter.Size = Math.Max(column.Length, length);
        }
    }

    /// <summary>
    /// Works out the column type a parameter declares, as the Entity Framework Core conventions map the value object.
    /// </summary>
    /// <returns>The column type and its length, or <see langword="null"/> when the underlying type says it all.</returns>
    /// <remarks>
    /// <para>
    /// Only an entity identifier is known to be ASCII. Declaring any other text non-Unicode would let SQL Server
    /// replace the characters its code page lacks.
    /// </para>
    /// <para>
    /// A construction of a generic value object is never registered as such: the registry describes it the first time
    /// it is asked, which happens here, so that the column does not depend on what else asked first.
    /// </para>
    /// </remarks>
    private static (DbType Type, int Length)? DeclaredColumn()
    {
        if (typeof(TValue) != typeof(string)
            || !(typeof(TSelf).IsConstructedGenericType
                ? ValueObjectRegistry.TryResolve(typeof(TSelf), out var descriptor)
                : ValueObjectRegistry.TryGet(typeof(TSelf), out descriptor))
            || descriptor.Schema.MaxLength is not { } maxLength)
        {
            return null;
        }

        return typeof(TSelf).GetInterface(EntityIdInterface) is not null
            ? (DbType.AnsiStringFixedLength, maxLength)
            : (DbType.String, maxLength);
    }

    /// <summary>
    /// Reports a value the value object refuses, with the rule that refused it.
    /// </summary>
    /// <param name="validation">The refusal.</param>
    /// <returns>The exception to throw.</returns>
    private static DataException Refused(ValidationResult validation)
        => new($"The value read is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}");

    /// <summary>
    /// Converts what the provider returned into the underlying type, when it returned another type.
    /// </summary>
    /// <param name="value">Value the provider returned.</param>
    /// <returns>The underlying value.</returns>
    /// <exception cref="DataException">The value cannot be converted to the underlying type.</exception>
    /// <remarks>
    /// Providers return their own type for some columns of the date and time family, and
    /// <see cref="System.Convert.ChangeType(object, Type, IFormatProvider)"/> converts none of these, since none
    /// implements <see cref="IConvertible"/> on both sides: SQL Server returns a <see cref="DateTime"/> for a
    /// <c>date</c> and a <see cref="TimeSpan"/> for a <c>time</c>, Npgsql a <see cref="DateOnly"/> and a
    /// <see cref="TimeOnly"/> for them, and a UTC <see cref="DateTime"/> for a <c>timestamptz</c>. A
    /// <see cref="DateTime"/> that does not say which zone it is in has no offset to give, and is refused.
    /// Everything else goes to <see cref="System.Convert.ChangeType(object, Type, IFormatProvider)"/>, which turns
    /// the double a provider may return for a decimal into one. A <see cref="Guid"/>, which implements no
    /// <see cref="IConvertible"/>, becomes text in its <c>D</c> form, the form <see cref="Guid.ToString()"/> writes.
    /// What none of them can convert - another type, a value out of the range of the underlying type - is refused
    /// with the type read and the type wanted.
    /// </remarks>
    private static TValue Convert(object value)
    {
        try
        {
            return value switch
            {
                DateTime dateTime when typeof(TValue) == typeof(DateOnly)
                    => (TValue)(object)DateOnly.FromDateTime(dateTime),
                DateOnly date when typeof(TValue) == typeof(DateTime)
                    => (TValue)(object)date.ToDateTime(TimeOnly.MinValue),
                TimeSpan time when typeof(TValue) == typeof(TimeOnly) => (TValue)(object)TimeOnly.FromTimeSpan(time),
                TimeOnly time when typeof(TValue) == typeof(TimeSpan) => (TValue)(object)time.ToTimeSpan(),
                DateTime { Kind: DateTimeKind.Unspecified } when typeof(TValue) == typeof(DateTimeOffset)
                    => throw new DataException(
                        "The DateTime read names no zone, and so no offset: it cannot be converted to "
                        + $"{typeof(TSelf).Name}, a value object over DateTimeOffset."),
                DateTime instant when typeof(TValue) == typeof(DateTimeOffset)
                    => (TValue)(object)new DateTimeOffset(instant),
                Guid guid when typeof(TValue) == typeof(string) => (TValue)(object)guid.ToString("D"),
                _ => (TValue)System.Convert.ChangeType(value, typeof(TValue), CultureInfo.InvariantCulture),
            };
        }
        catch (InvalidCastException exception)
        {
            throw Unconvertible(value, exception);
        }
        catch (OverflowException exception)
        {
            throw Unconvertible(value, exception);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw Unconvertible(value, exception);
        }
    }

    /// <summary>
    /// Reports a value the handler cannot convert to the underlying type, naming the type the provider returned.
    /// </summary>
    /// <param name="value">Value the provider returned.</param>
    /// <param name="exception">What the conversion threw.</param>
    /// <returns>The exception to throw.</returns>
    private static DataException Unconvertible(object value, Exception exception)
        => new(
            $"The {value.GetType().Name} read cannot be converted to {typeof(TSelf).Name}, a value object over "
            + $"{typeof(TValue).Name}.",
            exception);

    /// <inheritdoc />
    void SqlMapper.ITypeHandler.SetValue(IDbDataParameter parameter, object value)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        // Dapper hands an optional value object that holds nothing over as DBNull.
        if (value is DBNull)
        {
            parameter.Value = value;
            Declare(parameter, 0);
            return;
        }

        SetValue(parameter, (TSelf)value);
    }

    /// <inheritdoc />
    object? SqlMapper.ITypeHandler.Parse(Type destinationType, object value)
    {
        ArgumentNullException.ThrowIfNull(destinationType);

        return value is DBNull && Nullable.GetUnderlyingType(destinationType) is not null
            ? null
            : Parse(value);
    }
}
