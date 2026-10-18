using System.Collections;
using System.Reflection;
using Marten;
using Marten.Internal.CompiledQueries;
using Npgsql;
using NpgsqlTypes;

namespace AdCodicem.ValueObjects.Marten;

/// <summary>
/// The query member that binds the parameter of a <c>Contains</c> or an <c>IsOneOf</c> in a compiled query: it claims the
/// parameter it was written for, and writes on each call the underlying values of the member they are read from.
/// </summary>
/// <typeparam name="TSelf">The value object.</typeparam>
/// <typeparam name="TValue">Its underlying type.</typeparam>
internal sealed class ValueObjectsQueryMember<TSelf, TValue> : IQueryMember, ICompiledQueryAwareFilter
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    private readonly NpgsqlParameter _parameter;
    private readonly NpgsqlDbType _dbType;

    /// <summary>Initializes the member for a parameter Marten's command holds.</summary>
    /// <param name="parameter">The parameter.</param>
    /// <param name="source">Where the values are read from.</param>
    public ValueObjectsQueryMember(NpgsqlParameter parameter, ValuesSource source)
    {
        _parameter = parameter;
        _dbType = parameter.NpgsqlDbType;
        Source = source;
    }

    /// <summary>Gets where the values are read from.</summary>
    public ValuesSource Source { get; }

    /// <inheritdoc />
    /// <remarks>
    /// The underlying type, which Marten's runtime descriptor maps to a parameter type for a binder it never calls: the
    /// setter writes the parameter.
    /// </remarks>
    public Type Type => typeof(TValue);

    /// <inheritdoc />
    /// <remarks>
    /// The member of the query type the values are read from: Marten's runtime descriptor files a binder under its name,
    /// which it never calls, the setter writing the parameter.
    /// </remarks>
    public MemberInfo Member => Source.Chain[0];

    /// <inheritdoc />
    public string ParameterName => _parameter.ParameterName;

    /// <inheritdoc />
    public bool CanWrite() => false;

    /// <inheritdoc />
    public void StoreValue(object query)
    {
    }

    /// <inheritdoc />
    public bool TryMatch(NpgsqlParameter parameter, StoreOptions options, ICompiledQueryAwareFilter[] filters, out ICompiledQueryAwareFilter filter)
    {
        // Marten builds the parameter's setter from the filter a member answers with.
        filter = this;
        return ReferenceEquals(parameter, _parameter);
    }

    /// <inheritdoc />
    public void MarkFilterUsages(StoreOptions options, ICompiledQueryAwareFilter[] filters)
    {
    }

    /// <inheritdoc />
    public void TryWriteValue(UniqueValueSource valueSource, object query)
    {
    }

    /// <inheritdoc />
    public object GetValueAsObject(object query) => (object?)Read(query) ?? DBNull.Value;

    /// <inheritdoc />
    public bool TryMatchValue(object value, MemberInfo member) => false;

    /// <inheritdoc />
    public Action<NpgsqlParameter, object> BuildSetter()
        => (parameter, query) =>
        {
            parameter.NpgsqlDbType = _dbType;
            parameter.Value = (object?)Read(query) ?? DBNull.Value;
        };

    /// <summary>Reads the underlying values off a call's query.</summary>
    /// <param name="query">The compiled query called.</param>
    /// <returns>The list of underlying values, or <see langword="null"/> where the member holds none.</returns>
    private List<TValue>? Read(object query)
        => Source.Read(query) is IEnumerable values ? MartenValueObject<TSelf, TValue>.Unwrap(values) : null;
}
