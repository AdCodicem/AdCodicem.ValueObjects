using System.Collections;
using System.Reflection;
using JasperFx.Core.Reflection;
using Marten;
using Marten.Exceptions;
using Marten.Linq.Members;
using Marten.Linq.SqlGeneration;
using Marten.Linq.SqlGeneration.Filters;
using Weasel.Core;
using Weasel.Postgresql;
using Weasel.Postgresql.SqlGeneration;

namespace AdCodicem.ValueObjects.Marten;

/// <summary>
/// What Marten needs of one value object: its value types, its member, and its values unwrapped.
/// </summary>
internal abstract class MartenValueObject
{
    /// <summary>Gets the value object.</summary>
    public abstract Type ValueObjectType { get; }

    /// <summary>
    /// Gets whether Marten queries the underlying type: it maps no <c>ushort</c>, <c>ulong</c>, <c>Int128</c> or
    /// <c>UInt128</c> to a column type, and refuses a query over one, as over the value object then.
    /// </summary>
    public abstract bool IsQueryable { get; }

    /// <summary>
    /// Adds the value object's nullable type to the value types of a store, and maps the value object to its underlying
    /// type's database type, once.
    /// </summary>
    /// <param name="options">The store's options.</param>
    public abstract void AddTo(StoreOptions options);

    /// <summary>Tells whether a type is a collection of the value object, nullable or not.</summary>
    /// <param name="type">The type of a member.</param>
    /// <returns><see langword="true"/> for an enumerable of the value object.</returns>
    public abstract bool IsHeldBy(Type type);

    /// <summary>
    /// Writes a member holding the value object, the package's, a duplicated field or an identity, tested against values of
    /// it: <c>locator = ANY(:p0)</c> over the underlying values.
    /// </summary>
    /// <param name="member">The member tested.</param>
    /// <param name="values">The value objects, or nullable ones.</param>
    /// <param name="source">Where the values were read from, when it is a chain of members.</param>
    /// <returns>The fragment.</returns>
    public abstract ISqlFragment In(IQueryableMember member, IEnumerable values, ValuesSource? source);

    /// <summary>
    /// Builds Marten's refusal of a member its LINQ does not read (<c>x.Iban.CountryCode</c>), naming the type Marten's own
    /// member reports, where the package's members report <c>TSelf?</c> to Marten's duplicated field.
    /// </summary>
    /// <param name="declared">The type Marten's member reports.</param>
    /// <param name="member">The member not read.</param>
    /// <returns>Marten's exception, with Marten's wording.</returns>
    public static BadLinqExpressionException NotRead(Type declared, MemberInfo member)
        => new($"Marten does not (yet) support member {declared.ShortNameInCode()}.{member.Name}");

    /// <summary>Creates Marten's member for a member of the value object's type, or of its nullable type.</summary>
    /// <param name="parent">The member holding it.</param>
    /// <param name="options">The store's options, whose serializer's casing and schema the member reads.</param>
    /// <param name="member">The member.</param>
    /// <returns>The member Marten queries through.</returns>
    public abstract IQueryableMember CreateMember(IQueryableMember parent, StoreOptions options, MemberInfo member);
}

/// <summary>
/// What Marten needs of the value object <typeparamref name="TSelf"/>, closed over its type arguments.
/// </summary>
/// <typeparam name="TSelf">The value object.</typeparam>
/// <typeparam name="TValue">Its underlying type.</typeparam>
internal sealed class MartenValueObject<TSelf, TValue> : MartenValueObject
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>
    /// The value type the package's members read: the value through <see cref="IValueObject{TValue}.Value"/>, which an
    /// explicit implementation answers too, and a value read back through the rules, a refusal as the converter refuses a
    /// document. It never joins Marten's list of value types (<see cref="AddTo"/>).
    /// </summary>
    private readonly ValueTypeInfo _valueType = new(
        typeof(TSelf),
        typeof(TValue),
        typeof(IValueObject<TValue>).GetProperty(nameof(IValueObject<TValue>.Value))!,
        ((Func<TValue, TSelf>)ValueObjectReads<TSelf, TValue>.Read).Method);

    /// <summary>
    /// The same value type for <c>TSelf?</c>, the one entry of Marten's list of value types: Marten's duplicated field looks
    /// it up by the member's type, which the package's members report as <c>TSelf?</c>.
    /// </summary>
    private readonly ValueTypeInfo _nullableValueType = new(
        typeof(TSelf?),
        typeof(TValue),
        typeof(IValueObject<TValue>).GetProperty(nameof(IValueObject<TValue>.Value))!,
        ((Func<TValue, TSelf?>)ValueObjectReads<TSelf, TValue>.ReadNullable).Method);

    /// <summary>Builds the value object over a value with no rule applied: the text a <c>.Value</c> is compared with.</summary>
    private static readonly Func<TValue, TSelf> Unchecked = static value => TSelf.CreateUnchecked(value);

    /// <inheritdoc />
    public override Type ValueObjectType => typeof(TSelf);

    /// <inheritdoc />
    public override bool IsQueryable => PostgresqlProvider.Instance.HasTypeMapping(typeof(TValue));

    /// <inheritdoc />
    /// <remarks>
    /// Marten accepts a compiled query whose query type holds a value type its list holds, and binds no parameter of one,
    /// so it would answer every call with the first call's value, whatever the query does with it: the value object stays
    /// out of the list, and Marten refuses such a query type itself, as it does without the package. Its nullable type is
    /// listed, which Marten never accepts in a compiled query, for the duplicated field, which finds a value type by the
    /// member's type: the package's members report <c>TSelf?</c>, and claim a collection of the value object themselves.
    /// </remarks>
    public override void AddTo(StoreOptions options)
    {
        if (IsQueryable && MartenValueTypes.TryAdd(options, _nullableValueType))
        {
            MapToTheUnderlyingType();
        }
    }

    /// <inheritdoc />
    public override bool IsHeldBy(Type type) => CompiledQueryBinding.HoldsValuesOf<TSelf>(type);

    /// <inheritdoc />
    public override IQueryableMember CreateMember(IQueryableMember parent, StoreOptions options, MemberInfo member)
    {
        KeepTheSelectClause();

        // TValue is string on the first branch, which C# cannot prove: the delegate goes through object.
        var casing = options.Serializer().Casing;
        return typeof(TValue) == typeof(string)
            ? new ValueObjectStringMember<TSelf>(parent, casing, member, _valueType, (Func<string, TSelf>)(object)Unchecked, Guard)
            : new ValueObjectMember<TSelf, TValue>(parent, casing, member, _valueType, options.DatabaseSchemaName);
    }

    /// <inheritdoc />
    public override ISqlFragment In(IQueryableMember member, IEnumerable values, ValuesSource? source)
        => new ValueObjectInFilter<TSelf, TValue>(member, Unwrap(values), values.Cast<object?>().Contains(null), source);

    /// <summary>
    /// Puts the package's parameter in place of the one Marten's member wrote into a comparison, which refuses a compiled
    /// query taking the value object as a parameter.
    /// </summary>
    /// <param name="member">The member compared.</param>
    /// <param name="fragment">The comparison.</param>
    /// <returns>The comparison, through the package's parameter.</returns>
    public static ISqlFragment Guard(IQueryableMember member, ISqlFragment fragment)
        => fragment is MemberComparisonFilter { Right: CommandParameter parameter } comparison
            ? new MemberComparisonFilter(member, new ValueObjectParameter<TSelf, TValue>(parameter), comparison.Op)
            : fragment;

    /// <summary>
    /// Unwraps the values of an enumerable to the underlying values a parameter holds, skipping a <see langword="null"/>.
    /// </summary>
    /// <param name="values">The values, value objects or nullable ones.</param>
    /// <returns>A list of the underlying values.</returns>
    public static List<TValue> Unwrap(IEnumerable values) => values.OfType<TSelf>().Select(static value => Parameter(value.Value)).ToList();

    /// <summary>
    /// Gets the value a parameter holds for an underlying value: the value itself, but a <see cref="DateTimeOffset"/> in UTC,
    /// as Marten's own member of that type writes it, since Npgsql writes no other offset to a
    /// <c>timestamp with time zone</c>.
    /// </summary>
    /// <param name="value">The underlying value.</param>
    /// <returns>The value of the parameter.</returns>
    public static TValue Parameter(TValue value)
        => value is DateTimeOffset moment ? (TValue)(object)moment.ToUniversalTime() : value;

    /// <summary>
    /// Maps the value object, in Weasel's process-wide provider, to the database type of its underlying type, as
    /// <c>RegisterValueType</c> does, so that a duplicated column and a parameter take it; text with a maximum length
    /// becomes <c>varchar(n)</c>, which counts characters, never more than the UTF-16 code units .NET counts, so the
    /// column refuses no value the type accepts.
    /// </summary>
    private static void MapToTheUnderlyingType()
    {
        var provider = PostgresqlProvider.Instance;
        var databaseType = typeof(TValue) == typeof(string) && TSelf.Schema.MaxLength is { } maxLength
            ? $"varchar({maxLength})"
            : provider.GetDatabaseType(typeof(TValue), EnumStorage.AsInteger);

        provider.RegisterMapping(typeof(TSelf), databaseType, provider.TryGetDbType(typeof(TValue)));
    }

    /// <summary>
    /// Keeps the constructor of the select clause Marten's member closes over the value object with
    /// <c>Activator.CreateInstance</c>, which a native image holds only for an instantiation the compiler saw.
    /// </summary>
    private static void KeepTheSelectClause()
        => _ = typeof(ValueTypeSelectClause<TSelf, TValue>).GetConstructor([typeof(string), typeof(Func<TValue, TSelf>)]);
}
