using System.Linq.Expressions;
using System.Reflection;
using AdCodicem.ValueObjects.Marten;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using JasperFx.Core.Reflection;
using Marten;
using Marten.Exceptions;
using Marten.Internal.CompiledQueries;
using Marten.Linq;
using Marten.Linq.Members;
using Marten.Linq.Parsing;
using Marten.Linq.SqlGeneration;
using Marten.Schema;
using Npgsql;
using NpgsqlTypes;
using Weasel.Postgresql;
using Weasel.Postgresql.SqlGeneration;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// Pins what the package relies on in Marten beyond its documented API, each apart, so that a Marten release changing
/// one fails here first and names it: the internal <c>StoreOptions.ValueTypes</c> the package reaches through
/// <c>[UnsafeAccessor]</c>, the duplicated field finding a value type by the type its member reports, Marten's refusal of a
/// compiled query over a value type its list does not hold, the select clause constructor its member closes, each step of
/// the compiled-query planning the binding of a collection rests on, Marten's own stale compiled-query parameters,
/// which the package refuses or documents, and which, the day Marten binds them, these tests report so that the refusal
/// can step back, and what the remedy of <c>VO0048</c> rests on: the member types Marten binds in a compiled query, and
/// its refusal of an identity's <c>Value</c>.
/// </summary>
public sealed class MartenInternalsTests
{
    [Fact]
    public void The_value_types_Marten_keeps_on_its_options_are_reached_and_start_empty()
        => MartenValueTypes.Of(new StoreOptions()).Should().BeEmpty();

    [Fact]
    public void A_value_object_joins_Marten_s_value_types_as_its_nullable_type_alone_read_through_its_rules()
    {
        var options = new StoreOptions();
        options.UseValueObject<PageNumber, int>();

        var nullable = MartenValueTypes.Of(options).Should().ContainSingle().Which;
        nullable.OuterType.Should().Be<PageNumber?>();
        nullable.SimpleType.Should().Be<int>();
        nullable.ValueProperty.Should().BeSameAs(typeof(IValueObject<int>).GetProperty(nameof(IValueObject<>.Value)));
        nullable.Builder.Should().BeSameAs(((Func<int, PageNumber?>)ValueObjectReads<PageNumber, int>.ReadNullable).Method);
        nullable.CreateWrapper<PageNumber?, int>()(3).Should().Be(PageNumber.Create(3));
        FluentActions.Invoking(() => nullable.CreateWrapper<PageNumber?, int>()(0))
            .Should().Throw<ValueObjectJsonException>().Which.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public void A_projected_value_is_read_through_the_rules_a_refusal_carrying_its_code()
    {
        ValueObjectReads<PageNumber, int>.Read(3).Should().Be(PageNumber.Create(3));
        var refused = FluentActions.Invoking(static () => ValueObjectReads<PageNumber, int>.Read(0))
            .Should().Throw<ValueObjectJsonException>().Which;
        refused.Message.Should().Be("The value is not a valid PageNumber: The value must be greater than or equal to 1.");
        refused.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
        refused.ValueObjectType.Should().Be<PageNumber>();
        refused.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public void A_value_a_factory_written_by_hand_refuses_without_a_reason_is_read_as_not_parsable_under_its_own_name()
    {
        var refused = FluentActions.Invoking(static () => ValueObjectReads<HandWrittenId<MartenMuteProfile>, string>.Read("mrtmute_x"))
            .Should().Throw<ValueObjectJsonException>().Which;
        refused.ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
        refused.Message.Should().Be("The value is not a valid HandWrittenId<MartenMuteProfile>: ");
    }

    /// <summary>
    /// Marten's duplicated field finds the value type of a member by the type the member reports, which the package's
    /// members report as <c>TSelf?</c>, the one value type of the value object the list holds: the day Marten looks it up
    /// otherwise, a duplicated value object fails here first.
    /// </summary>
    [Fact]
    public void Marten_s_duplicated_field_takes_the_value_type_of_the_type_the_member_reports()
    {
        var options = new StoreOptions();
        options.UseValueObject<PageNumber, int>().UseValueObject<Label, string>();
        var mapping = new DocumentMapping<MartenTypedOnlyOrder>(options);

        var page = mapping.DuplicateField(nameof(MartenTypedOnlyOrder.Page)).Should().BeOfType<DuplicatedValueTypeField>().Which;
        var label = mapping.DuplicateField(nameof(MartenTypedOnlyOrder.Label)).Should().BeOfType<DuplicatedValueTypeField>().Which;

        page.MemberType.Should().Be<PageNumber?>();
        page.ValueTypeInfo.Should().BeSameAs(MartenValueTypes.Of(options).Single(static valueType => valueType.OuterType == typeof(PageNumber?)));
        label.MemberType.Should().Be<Label?>();
        label.ValueTypeInfo.Should().BeSameAs(MartenValueTypes.Of(options).Single(static valueType => valueType.OuterType == typeof(Label?)));
    }

    /// <summary>
    /// Marten refuses a compiled query whose query type holds a value type its list does not hold, before any extension of
    /// Marten runs, and accepts one it holds: the package keeps every value object out of the list, so that Marten refuses
    /// one whatever the query does with it. The day this fails, the package must refuse such a query type some other way.
    /// </summary>
    [Fact]
    public void Marten_refuses_a_compiled_query_over_a_value_type_its_list_does_not_hold()
    {
        using var store = MartenHarness.Offline(static _ => { });

        FluentActions.Invoking(() => MartenHarness.Preview(store, new MartenWrappedQuery { Code = new MartenWrapped("abc") }))
            .Should().Throw<InvalidCompiledQueryException>().WithMessage("Members MartenWrapped Code cannot be used as parameters to a compiled query");
    }

    [Fact]
    public void The_select_clause_Marten_s_member_closes_has_the_constructor_the_package_keeps()
        => typeof(ValueTypeSelectClause<PageNumber, int>).GetConstructor([typeof(string), typeof(Func<int, PageNumber>)]).Should().NotBeNull();

    /// <summary>
    /// Marten binds no parameter of a value type in a compiled query: a type registered with its own
    /// <c>RegisterValueType</c> answers the second call with the first call's value. The day this fails, Marten binds
    /// them, and the package's refusal of a single value object can step back.
    /// </summary>
    [Fact]
    public void Marten_answers_a_compiled_query_over_its_own_value_type_with_the_value_of_the_first_call()
    {
        using var store = MartenHarness.Offline(static options => options.RegisterValueType(typeof(MartenWrapped)));

        MartenHarness.Preview(store, new MartenWrappedQuery { Code = new MartenWrapped("abc") }).Parameters.Single().Value.Should().Be("abc");
        MartenHarness.Preview(store, new MartenWrappedQuery { Code = new MartenWrapped("xyz") }).Parameters.Single().Value
            .Should().Be("abc", "Marten replays the value it planned from; the day it binds xyz, the refusal can step back");
    }

    /// <summary>
    /// Marten lists a value object it takes for a document's identity itself, once it has built that document's mapping:
    /// before, it refuses a compiled query comparing the identity with a member, as any value object; after, the identity
    /// is written by Marten's own member, which no package code reaches, and the second call is answered with the first
    /// call's identity. The guide warns of it; the day this fails, the warning goes.
    /// </summary>
    [Fact]
    public void Marten_answers_a_compiled_query_over_a_value_object_identity_with_the_identity_of_the_first_call()
    {
        using var store = MartenHarness.Offline(static options =>
        {
            options.UseValueObjects();
            options.UseValueObjectId<MartenAccount, CustomerId, Guid>();
        });
        var first = CustomerId.Create(Guid.Parse("00000001-0000-0000-0000-000000000000"));
        var second = CustomerId.Create(Guid.Parse("00000002-0000-0000-0000-000000000000"));

        FluentActions.Invoking(() => MartenHarness.Preview(store, new MartenAccountsById { Id = first }))
            .Should().Throw<InvalidCompiledQueryException>().WithMessage("Members CustomerId Id cannot be used as parameters to a compiled query");
        MartenHarness.Sql<MartenAccount>(store, static accounts => accounts.Where(x => x.Name == "Ada"));

        MartenHarness.Preview(store, new MartenAccountsById { Id = first }).Parameters.Single().Value.Should().Be(first.Value);
        MartenHarness.Preview(store, new MartenAccountsById { Id = second }).Parameters.Single().Value
            .Should().Be(first.Value, "Marten replays the identity it planned from; the day it binds the second, the guide's warning goes");
    }

    /// <summary>The types a value object may hold, by the names a test gives them.</summary>
    public static TheoryData<string> UnderlyingTypes =>
    [
        "string", "Guid", "bool", "char", "sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong", "Int128",
        "UInt128", "decimal", "double", "float", "DateOnly", "TimeOnly", "DateTime", "DateTimeOffset", "TimeSpan",
    ];

    /// <summary>
    /// Marten binds a compiled query's member of its own parameter types alone, <see langword="string"/>,
    /// <see cref="Guid"/>, <see langword="int"/>, <see langword="long"/>, <see langword="float"/>, <see langword="decimal"/>,
    /// <see cref="DateTime"/> and <see cref="DateTimeOffset"/> among the types a value object may hold, and refuses a
    /// member of any other, whether its query reads it or not. <c>VO0048</c> names a member of one of those types, an
    /// <see langword="int"/> or a <see langword="long"/> C# promotes the value to, or a collection of the value object: the
    /// day this fails, Marten binds another type, and the diagnostic's remedy follows.
    /// </summary>
    [Theory]
    [MemberData(nameof(UnderlyingTypes))]
    public void Marten_binds_a_compiled_query_member_of_its_own_parameter_types_alone(string type)
    {
        using var store = MartenHarness.Offline(static _ => { });
        var planning = type switch
        {
            "string" => Planning<string>(store),
            "Guid" => Planning<Guid>(store),
            "bool" => Planning<bool>(store),
            "char" => Planning<char>(store),
            "sbyte" => Planning<sbyte>(store),
            "byte" => Planning<byte>(store),
            "short" => Planning<short>(store),
            "ushort" => Planning<ushort>(store),
            "int" => Planning<int>(store),
            "uint" => Planning<uint>(store),
            "long" => Planning<long>(store),
            "ulong" => Planning<ulong>(store),
            "Int128" => Planning<Int128>(store),
            "UInt128" => Planning<UInt128>(store),
            "decimal" => Planning<decimal>(store),
            "double" => Planning<double>(store),
            "float" => Planning<float>(store),
            "DateOnly" => Planning<DateOnly>(store),
            "TimeOnly" => Planning<TimeOnly>(store),
            "DateTime" => Planning<DateTime>(store),
            "DateTimeOffset" => Planning<DateTimeOffset>(store),
            "TimeSpan" => Planning<TimeSpan>(store),
            _ => throw new InvalidOperationException($"No query holds a {type}."),
        };

        if (type is "string" or "Guid" or "int" or "long" or "float" or "decimal" or "DateTime" or "DateTimeOffset")
        {
            planning.Should().NotThrow();
        }
        else
        {
            planning.Should().Throw<InvalidCompiledQueryException>().WithMessage("Members * Value cannot be used as parameters to a compiled query");
        }
    }

    /// <summary>
    /// Marten reads no <c>Value</c> of a value object it takes for a document's identity: <c>x.Id.Value == Id</c> is its
    /// <c>BadLinqExpressionException</c>, so <c>VO0048</c> names a collection of the value object for a document's identity,
    /// which the package binds on every call. The day this fails, that clause can go.
    /// </summary>
    [Fact]
    public void Marten_reads_no_Value_of_a_value_object_it_takes_for_an_identity()
    {
        using var store = MartenHarness.Offline(static options =>
        {
            options.UseValueObjects();
            options.UseValueObjectId<MartenAccount, CustomerId, Guid>();
        });
        MartenHarness.Sql<MartenAccount>(store, static accounts => accounts.Where(x => x.Name == "Ada"));

        FluentActions.Invoking(() => MartenHarness.Preview(store, new MartenAccountsByIdValue { Id = Guid.Parse("00000001-0000-0000-0000-000000000000") }))
            .Should().Throw<BadLinqExpressionException>().WithMessage("Marten does not (yet) support using member chain: Id.Value");
    }

    [Fact]
    public void Planning_writes_the_command_into_the_plan_then_offers_each_parameter_to_its_members_in_order()
    {
        var log = Plan(out _);

        log.Builders.Should().ContainSingle().Which.Should().BeOfType<CompiledQueryPlan>("the fragment knows it is planned through the builder it is handed");
        log.Asked.Should().StartWith(("first", true), "the member inserted while the command is written is asked next, first in the list");
        log.Asked.Should().NotContain(static asked => asked.Name == "last" && asked.Claimed, "Marten stops at the first member that claims a parameter");
        log.Asked.Where(static asked => asked.Name == "first").Should().HaveCount(2, "the first member is offered every parameter");
    }

    [Fact]
    public void Planning_offers_a_member_the_parameter_object_AppendParameter_returned()
        => Plan(out _).Asked.Should().Contain(("first", true));

    [Fact]
    public void The_setter_of_the_filter_a_member_answers_with_writes_the_parameter_on_every_call()
    {
        var log = Plan(out var previews);

        previews.Should().Equal(
            "CAST(d.data ->> 'Index' as integer) = ANY(:p0) and CAST(d.data ->> 'Index' as integer) >= :p1 | [1001, 1002] Int32 Array|Integer | 0 Int32 Integer",
            "CAST(d.data ->> 'Index' as integer) = ANY(:p0) and CAST(d.data ->> 'Index' as integer) >= :p1 | [1003] Int32 Array|Integer | 1 Int32 Integer");
        log.Parses.Should().Be(log.ParsesOfTheFirstCall, "Marten plans a compiled query once, from the first call, and binds the later ones into that plan");
    }

    [Fact]
    public void Planning_takes_an_array_member_of_a_type_no_finder_takes_for_an_include()
        => Plan(out _).IncludedWhenApplied.Should().Equal(nameof(MartenPinnedQuery.Pins));

    [Fact]
    public void Planning_hands_the_parser_the_values_unreduced_as_a_member_of_the_query()
    {
        var values = Plan(out _).Values.Should().BeAssignableTo<MemberExpression>().Which;

        values.Member.Name.Should().Be(nameof(MartenPinnedQuery.Pins));
        values.Expression.Should().BeOfType<ConstantExpression>().Which.Value.Should().BeOfType<MartenPinnedQuery>();
    }

    [Fact]
    public void The_query_member_answers_Marten_s_other_questions_as_a_member_planned_after_the_template()
    {
        var parameter = new NpgsqlParameter("p0", NpgsqlDbType.Array | NpgsqlDbType.Integer);
        var pages = typeof(MartenOrdersOnPages).GetProperty(nameof(MartenOrdersOnPages.Pages))!;
        var member = new ValueObjectsQueryMember<PageNumber, int>(parameter, new ValuesSource(typeof(MartenOrdersOnPages), [pages]));
        var query = new MartenOrdersOnPages { Pages = [PageNumber.Create(2)] };

        member.Type.Should().Be<int>();
        member.Member.Should().BeSameAs(pages);
        member.ParameterName.Should().Be("p0");
        member.CanWrite().Should().BeFalse();
        member.TryMatchValue(2, pages).Should().BeFalse();
        member.GetValueAsObject(query).Should().BeEquivalentTo(new List<int> { 2 });
        member.GetValueAsObject(new MartenOrdersOnPages { Pages = null! }).Should().Be(DBNull.Value);
        member.TryMatch(new NpgsqlParameter(), new StoreOptions(), [], out var filter).Should().BeFalse();
        filter.Should().BeSameAs(member);
        FluentActions.Invoking(() =>
        {
            member.StoreValue(query);
            member.MarkFilterUsages(new StoreOptions(), []);
            member.TryWriteValue(new UniqueValueSource(), query);
        }).Should().NotThrow();
    }

    /// <summary>Plans <see cref="MartenPinnedQuery"/> through a parser of the test's own and previews two calls.</summary>
    private static MartenPinLog Plan(out List<string> previews)
    {
        var log = new MartenPinLog();
        using var store = MartenHarness.Offline(options => options.Linq.MethodCallParsers.Insert(0, new MartenPinParser(log)));
        var first = Preview(new MartenPinnedQuery { Pins = [new MartenPin(1), new MartenPin(2)], MinimumIndex = 0 });
        log.ParsesOfTheFirstCall = log.Parses;
        previews = [first, Preview(new MartenPinnedQuery { Pins = [new MartenPin(3)], MinimumIndex = 1 })];
        return log;

        string Preview(MartenPinnedQuery query)
            => MartenHarness.Preview(store, query).Description
                .Replace("select d.data from public.mt_doc_martenorder as d where (", string.Empty, StringComparison.Ordinal)
                .Replace(");", string.Empty, StringComparison.Ordinal);
    }

    private static Action Planning<T>(IDocumentStore store) => () => MartenHarness.Preview(store, new MartenPrimitiveQuery<T>());
}

/// <summary>A value type of the test's own, registered with Marten's own <c>RegisterValueType</c>, never through the registry.</summary>
/// <param name="Value">Its value, its one public property.</param>
public readonly record struct MartenWrapped(string Value);

/// <summary>A document holding a <see cref="MartenWrapped"/>.</summary>
public sealed class MartenWrappedHolder
{
    public Guid Id { get; set; }

    public MartenWrapped Code { get; set; }
}

/// <summary>A compiled query comparing a <see cref="MartenWrapped"/> with a member.</summary>
public sealed class MartenWrappedQuery : ICompiledListQuery<MartenWrappedHolder>
{
    public MartenWrapped Code { get; set; }

    public Expression<Func<IMartenQueryable<MartenWrappedHolder>, IEnumerable<MartenWrappedHolder>>> QueryIs()
        => holders => holders.Where(x => x.Code == Code);
}

/// <summary>A compiled query comparing a value-object identity with a member.</summary>
public sealed class MartenAccountsById : ICompiledListQuery<MartenAccount>
{
#pragma warning disable VO0048 // The shape this test pins: Marten answers a value-object identity with the first call's value.
    public CustomerId Id { get; set; }
#pragma warning restore VO0048

    public Expression<Func<IMartenQueryable<MartenAccount>, IEnumerable<MartenAccount>>> QueryIs()
        => accounts => accounts.Where(x => x.Id == Id);
}

/// <summary>A compiled query comparing a value-object identity's <c>Value</c> with a member of its underlying type.</summary>
public sealed class MartenAccountsByIdValue : ICompiledListQuery<MartenAccount>
{
    public Guid Id { get; set; }

    public Expression<Func<IMartenQueryable<MartenAccount>, IEnumerable<MartenAccount>>> QueryIs()
        => accounts => accounts.Where(x => x.Id.Value == Id);
}

/// <summary>A compiled query holding a member of one type, which its query does not read.</summary>
/// <typeparam name="T">The member's type.</typeparam>
public sealed class MartenPrimitiveQuery<T> : ICompiledListQuery<MartenOrder>
{
    public T Value { get; set; } = default!;

    public Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Index > 0);
}

/// <summary>Refuses every identifier without a reason, under a prefix no other profile claims.</summary>
public sealed class MartenMuteProfile : IHandWrittenIdProfile
{
    public static string Prefix => "mrtmute";

    public static bool RefusesSilently => true;
}

/// <summary>A struct of the test's own, which none of Marten's parameter finders takes.</summary>
/// <param name="Index">The index it stands for.</param>
public readonly record struct MartenPin(int Index);

/// <summary>The marker method the test's own parser reads.</summary>
public static class MartenPins
{
    public static bool Among(this int index, MartenPin[] pins) => pins.Any(pin => pin.Index == index);
}

/// <summary>A compiled query calling the marker method over an array member, beside an integer member.</summary>
public sealed class MartenPinnedQuery : ICompiledListQuery<MartenOrder>
{
#pragma warning disable CA1819 // An array, which Marten's planning takes for an include.
    public MartenPin[] Pins { get; set; } = [];
#pragma warning restore CA1819

    public int MinimumIndex { get; set; }

    public Expression<Func<IMartenQueryable<MartenOrder>, IEnumerable<MartenOrder>>> QueryIs()
        => orders => orders.Where(x => x.Index.Among(Pins) && x.Index >= MinimumIndex);
}

/// <summary>What the test's own parser, fragment and members saw.</summary>
internal sealed class MartenPinLog
{
    public int Parses { get; set; }

    public int ParsesOfTheFirstCall { get; set; }

    public Expression? Values { get; set; }

    public List<ICommandBuilder> Builders { get; } = [];

    public List<string> IncludedWhenApplied { get; } = [];

    public List<(string Name, bool Claimed)> Asked { get; } = [];
}

/// <summary>Reads <c>x.Index.Among(Pins)</c> into a fragment of the test's own.</summary>
internal sealed class MartenPinParser(MartenPinLog log) : IMethodCallParser
{
    public bool Matches(MethodCallExpression expression) => expression.Method.DeclaringType == typeof(MartenPins);

    public ISqlFragment Parse(IQueryableMemberCollection memberCollection, IReadOnlyStoreOptions options, MethodCallExpression expression)
    {
        log.Parses++;
        log.Values = expression.Arguments[1];
        return new MartenPinFragment(log, memberCollection.MemberFor(expression.Arguments[0]), expression.Arguments[1]);
    }
}

/// <summary>
/// <c>locator = ANY(:p0)</c>, which, planned, inserts a member claiming its parameter first in the plan's members, and
/// another last, which never claims one.
/// </summary>
internal sealed class MartenPinFragment(MartenPinLog log, IQueryableMember member, Expression values) : ISqlFragment
{
    public void Apply(ICommandBuilder builder)
    {
        log.Builders.Add(builder);
        var plan = builder as CompiledQueryPlan;
        var pins = (MartenPin[])values.ReduceToConstant().Value!;

        builder.Append(member.TypedLocator);
        builder.Append(" = ANY(");
        var parameter = builder.AppendParameter(pins.Select(static pin => pin.Index).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Integer);
        builder.Append(")");

        if (plan is not null)
        {
            log.IncludedWhenApplied.AddRange(plan.IncludeMembers.Select(static included => included.Name));
            var pinsMember = typeof(MartenPinnedQuery).GetProperty(nameof(MartenPinnedQuery.Pins))!;
            plan.IncludeMembers.Remove(pinsMember);
            plan.QueryMembers.Insert(0, new MartenPinMember(log, "first", parameter, claims: true, pinsMember));
            plan.QueryMembers.Add(new MartenPinMember(log, "last", parameter, claims: false, pinsMember));
        }
    }
}

/// <summary>A query member of the test's own, which records what Marten asks it and writes each call's pins plus 1000.</summary>
internal sealed class MartenPinMember(MartenPinLog log, string name, NpgsqlParameter parameter, bool claims, MemberInfo member)
    : IQueryMember, ICompiledQueryAwareFilter
{
    public Type Type => typeof(int);

    public MemberInfo Member => member;

    public string ParameterName => parameter.ParameterName;

    public bool CanWrite() => false;

    public void StoreValue(object query)
    {
    }

    public bool TryMatch(NpgsqlParameter offered, StoreOptions options, ICompiledQueryAwareFilter[] filters, out ICompiledQueryAwareFilter filter)
    {
        var claimed = claims && ReferenceEquals(offered, parameter);
        log.Asked.Add((name, claimed));
        filter = this;
        return claimed;
    }

    public void MarkFilterUsages(StoreOptions options, ICompiledQueryAwareFilter[] filters)
    {
    }

    public void TryWriteValue(UniqueValueSource valueSource, object query)
    {
    }

    public object GetValueAsObject(object query) => ((MartenPinnedQuery)query).Pins;

    public bool TryMatchValue(object value, MemberInfo member) => false;

    public Action<NpgsqlParameter, object> BuildSetter()
        => static (written, query) =>
        {
            written.NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Integer;
            written.Value = ((MartenPinnedQuery)query).Pins.Select(static pin => pin.Index + 1000).ToArray();
        };
}
