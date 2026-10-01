using System.Data;
using System.Globalization;
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
/// from a database this application wrote through the validating factory, and read paths are hot.
/// </para>
/// <para>
/// A SQL <c>NULL</c> reads as <see langword="null"/> into an optional value object, <c>TSelf?</c>, and is refused
/// with a <see cref="DataException"/> for a required one, as Dapper refuses it for an <see cref="int"/>. The handler
/// implements <see cref="SqlMapper.ITypeHandler"/> itself for that, since
/// <see cref="SqlMapper.TypeHandler{T}.Parse(object)"/> cannot return <see langword="null"/> for a struct.
/// </para>
/// </remarks>
public sealed class ValueObjectTypeHandler<TSelf, TValue> : SqlMapper.TypeHandler<TSelf>, SqlMapper.ITypeHandler
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <inheritdoc />
    public override void SetValue(IDbDataParameter parameter, TSelf value)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        parameter.Value = value.Value;
    }

    /// <inheritdoc />
    /// <exception cref="DataException"><paramref name="value"/> is a SQL <c>NULL</c>, which a value object cannot hold.</exception>
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

        // Providers are not always faithful: a Guid may come back as a string, a decimal as a double.
        // Going through the value object's own parser keeps that conversion in one place.
        if (value is string text && TSelf.TryParse(text, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return TSelf.CreateUnchecked((TValue)Convert.ChangeType(value, typeof(TValue), CultureInfo.InvariantCulture));
    }

    /// <inheritdoc />
    void SqlMapper.ITypeHandler.SetValue(IDbDataParameter parameter, object value)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        // Dapper hands an optional value object that holds nothing over as DBNull.
        if (value is DBNull)
        {
            parameter.Value = value;
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
