using AdCodicem.ValueObjects.Marten;
using Marten;
using Marten.Exceptions;
using Marten.Linq;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// Compiled queries under <c>UseValueObjects()</c>, each previewed for three calls with three values, as Marten plans the
/// first and binds each later one: a collection of value objects a member of the query type holds is bound on every
/// call, a single value object is refused whatever the query does with it, since Marten would answer every call with the
/// value of the first, and the <c>.Value</c> form over a member of the underlying type binds as any primitive does.
/// </summary>
public sealed class MartenCompiledQueryTests
{
    private const string Where = "select d.data from public.mt_doc_martenorder as d where ";

    public static TheoryData<int> Calls => [0, 1, 2];

    [Fact]
    public void An_array_of_value_objects_over_an_integer_is_bound_on_every_call()
        => Previews(static call => new MartenOrdersOnPages { Pages = [PageNumber.Create(call + 1), PageNumber.Create(call + 10)] })
            .Should().Equal(
                Where + "CAST(d.data ->> 'Page' as integer) = ANY(:p0); | [1, 10] Int32 Array|Integer",
                Where + "CAST(d.data ->> 'Page' as integer) = ANY(:p0); | [2, 11] Int32 Array|Integer",
                Where + "CAST(d.data ->> 'Page' as integer) = ANY(:p0); | [3, 12] Int32 Array|Integer");

    [Fact]
    public void A_list_of_value_objects_over_text_is_bound_on_every_call_through_IsOneOf()
        => Previews(static call => new MartenOrdersInCountries { Countries = call == 1 ? [CountryCode.Belgium] : [CountryCode.France, CountryCode.Luxembourg] })
            .Should().Equal(
                Where + "d.data ->> 'Country' = ANY(:p0); | [FR, LU] String Array|Text",
                Where + "d.data ->> 'Country' = ANY(:p0); | [BE] String Array|Text",
                Where + "d.data ->> 'Country' = ANY(:p0); | [FR, LU] String Array|Text");

    [Fact]
    public void An_array_field_of_value_objects_over_a_decimal_is_bound_on_every_call()
        => Previews(static call => new MartenOrdersOfAmounts { Amounts = [Amount.Create(1.5m + call)] })
            .Should().Equal(
                Where + "CAST(d.data ->> 'Amount' as decimal) = ANY(:p0); | [1.50] Decimal Array|Numeric",
                Where + "CAST(d.data ->> 'Amount' as decimal) = ANY(:p0); | [2.50] Decimal Array|Numeric",
                Where + "CAST(d.data ->> 'Amount' as decimal) = ANY(:p0); | [3.50] Decimal Array|Numeric");

    [Fact]
    public void A_list_of_value_objects_beside_an_integer_member_is_bound_with_it_on_every_call()
        => Previews(static call => new MartenOrdersBornOnFrom { Dates = [BirthDate.Create(new DateOnly(1990, 1, 1 + call))], MinimumIndex = call })
            .Should().Equal(
                Where + "(public.mt_immutable_date(d.data ->> 'Born') = ANY(:p0) and CAST(d.data ->> 'Index' as integer) >= :p1); | [01/01/1990] DateOnly Array|Date | 0 Int32 Integer",
                Where + "(public.mt_immutable_date(d.data ->> 'Born') = ANY(:p0) and CAST(d.data ->> 'Index' as integer) >= :p1); | [01/02/1990] DateOnly Array|Date | 1 Int32 Integer",
                Where + "(public.mt_immutable_date(d.data ->> 'Born') = ANY(:p0) and CAST(d.data ->> 'Index' as integer) >= :p1); | [01/03/1990] DateOnly Array|Date | 2 Int32 Integer");

    [Fact]
    public void A_collection_of_value_objects_over_each_remaining_family_is_bound_on_every_call()
    {
        Previews(static call => new MartenOrdersOfCustomers { Customers = [CustomerId.Create(Guid.Parse($"0000000{call + 1}-0000-0000-0000-000000000000"))] })
            .Should().Equal(
                Where + "CAST(d.data ->> 'Customer' as uuid) = ANY(:p0); | [00000001-0000-0000-0000-000000000000] Guid Array|Uuid",
                Where + "CAST(d.data ->> 'Customer' as uuid) = ANY(:p0); | [00000002-0000-0000-0000-000000000000] Guid Array|Uuid",
                Where + "CAST(d.data ->> 'Customer' as uuid) = ANY(:p0); | [00000003-0000-0000-0000-000000000000] Guid Array|Uuid");
        Previews(static call => new MartenOrdersOfConsents { Consents = [Consent.Create(call == 1)] })
            .Should().Equal(
                Where + "CAST(d.data ->> 'Consent' as boolean) = ANY(:p0); | [False] Boolean Array|Boolean",
                Where + "CAST(d.data ->> 'Consent' as boolean) = ANY(:p0); | [True] Boolean Array|Boolean",
                Where + "CAST(d.data ->> 'Consent' as boolean) = ANY(:p0); | [False] Boolean Array|Boolean");
        Previews(static call => new MartenOrdersAtLatitudes { Latitudes = [Latitude.Create(call + 0.5)] })
            .Should().Equal(
                Where + "CAST(d.data ->> 'Latitude' as double precision) = ANY(:p0); | [0.5] Double Array|Double",
                Where + "CAST(d.data ->> 'Latitude' as double precision) = ANY(:p0); | [1.5] Double Array|Double",
                Where + "CAST(d.data ->> 'Latitude' as double precision) = ANY(:p0); | [2.5] Double Array|Double");
        Previews(static call => new MartenOrdersAtMoments { Moments = [OccurredAt.Create(new DateTimeOffset(2026, 1, 1 + call, 0, 0, 0, TimeSpan.Zero))] })
            .Should().Equal(
                Where + "public.mt_immutable_timestamptz(d.data ->> 'At') = ANY(:p0); | [01/01/2026 00:00:00 +00:00] DateTimeOffset Array|TimestampTz",
                Where + "public.mt_immutable_timestamptz(d.data ->> 'At') = ANY(:p0); | [01/02/2026 00:00:00 +00:00] DateTimeOffset Array|TimestampTz",
                Where + "public.mt_immutable_timestamptz(d.data ->> 'At') = ANY(:p0); | [01/03/2026 00:00:00 +00:00] DateTimeOffset Array|TimestampTz");
        Previews(static call => new MartenOrdersOfReferences { References = [Reference<MartenOwner>.Create($"R{call}")] })
            .Should().Equal(
                Where + "d.data ->> 'Reference' = ANY(:p0); | [R0] String Array|Text",
                Where + "d.data ->> 'Reference' = ANY(:p0); | [R1] String Array|Text",
                Where + "d.data ->> 'Reference' = ANY(:p0); | [R2] String Array|Text");
    }

    [Fact]
    public void Two_collections_and_one_collection_read_twice_are_each_bound_on_every_call()
    {
        Previews(static call => new MartenOrdersOnPagesInCountries { Pages = [PageNumber.Create(call + 1)], Countries = call == 1 ? [CountryCode.Belgium] : [CountryCode.France] })
            .Should().Equal(
                Where + "(CAST(d.data ->> 'Page' as integer) = ANY(:p0) and d.data ->> 'Country' = ANY(:p1)); | [1] Int32 Array|Integer | [FR] String Array|Text",
                Where + "(CAST(d.data ->> 'Page' as integer) = ANY(:p0) and d.data ->> 'Country' = ANY(:p1)); | [2] Int32 Array|Integer | [BE] String Array|Text",
                Where + "(CAST(d.data ->> 'Page' as integer) = ANY(:p0) and d.data ->> 'Country' = ANY(:p1)); | [3] Int32 Array|Integer | [FR] String Array|Text");
        Previews(static call => new MartenOrdersOnPagesTwice { Pages = [PageNumber.Create(call + 1)] })
            .Should().Equal(
                Where + "(CAST(d.data ->> 'Page' as integer) = ANY(:p0) or CAST(d.data ->> 'Page' as integer) = ANY(:p1)); | [1] Int32 Array|Integer | [1] Int32 Array|Integer",
                Where + "(CAST(d.data ->> 'Page' as integer) = ANY(:p0) or CAST(d.data ->> 'Page' as integer) = ANY(:p1)); | [2] Int32 Array|Integer | [2] Int32 Array|Integer",
                Where + "(CAST(d.data ->> 'Page' as integer) = ANY(:p0) or CAST(d.data ->> 'Page' as integer) = ANY(:p1)); | [3] Int32 Array|Integer | [3] Int32 Array|Integer");
    }

    [Fact]
    public void A_collection_member_holding_nothing_binds_null()
    {
        using var store = Store();
        MartenHarness.Preview(store, new MartenOrdersOnPages { Pages = [PageNumber.Create(1)] });

        MartenHarness.Preview(store, new MartenOrdersOnPages { Pages = null! }).Parameters
            .Should().ContainSingle().Which.Value.Should().Be(DBNull.Value);
    }

    [Fact]
    public void A_collection_read_through_a_member_Marten_ignores_is_bound_on_every_call_and_null_when_the_member_holds_nothing()
        => Previews(static call => new MartenOrdersOnFilteredPages { Filter = call == 1 ? null : new MartenPageFilter { Pages = [PageNumber.Create(call + 1)] } })
            .Should().Equal(
                Where + "CAST(d.data ->> 'Page' as integer) = ANY(:p0); | [1] Int32 Array|Integer",
                Where + "CAST(d.data ->> 'Page' as integer) = ANY(:p0); |  DBNull Array|Integer",
                Where + "CAST(d.data ->> 'Page' as integer) = ANY(:p0); | [3] Int32 Array|Integer");

    /// <summary>
    /// A value object held by a member Marten is told to ignore is read by neither Marten nor the package: beside it, the
    /// <c>.Value</c> form and a constant collection of the same value object are planned as without it, and a collection is
    /// bound on every call.
    /// </summary>
    [Fact]
    public void A_value_object_held_by_a_member_Marten_ignores_leaves_the_query_planned_as_without_it()
    {
        Previews(static call => new MartenOrdersByIbanTextBesideIgnored { Noted = Iban.Create("DE89370400440532013000"), Iban = $"T{call}" })
            .Should().Equal(
                Where + "d.data ->> 'Iban' = :p0; | T0 String Text",
                Where + "d.data ->> 'Iban' = :p0; | T1 String Text",
                Where + "d.data ->> 'Iban' = :p0; | T2 String Text");
        Previews(static call => new MartenOrdersInBeneluxBesideIgnored { Ignored = [CountryCode.France], Preferred = CountryCode.France, MinimumIndex = call })
            .Should().Equal(
                Where + "(d.data ->> 'Country' = ANY(:p0) and CAST(d.data ->> 'Index' as integer) >= :p1); | [BE, LU] String Array|Text | 0 Int32 Integer",
                Where + "(d.data ->> 'Country' = ANY(:p0) and CAST(d.data ->> 'Index' as integer) >= :p1); | [BE, LU] String Array|Text | 1 Int32 Integer",
                Where + "(d.data ->> 'Country' = ANY(:p0) and CAST(d.data ->> 'Index' as integer) >= :p1); | [BE, LU] String Array|Text | 2 Int32 Integer");
        Previews(static call => new MartenOrdersOnPagesBesideIgnored { Ignored = PageNumber.Create(9), Pages = [PageNumber.Create(call + 1)] })
            .Should().Equal(
                Where + "CAST(d.data ->> 'Page' as integer) = ANY(:p0); | [1] Int32 Array|Integer",
                Where + "CAST(d.data ->> 'Page' as integer) = ANY(:p0); | [2] Int32 Array|Integer",
                Where + "CAST(d.data ->> 'Page' as integer) = ANY(:p0); | [3] Int32 Array|Integer");
    }

    [Fact]
    public void Constants_of_value_objects_stay_constants_of_the_plan()
    {
        Previews(static call => new MartenOrdersInBeneluxFrom { MinimumIndex = call })
            .Should().Equal(
                Where + "(d.data ->> 'Country' = ANY(:p0) and CAST(d.data ->> 'Index' as integer) >= :p1); | [BE, LU] String Array|Text | 0 Int32 Integer",
                Where + "(d.data ->> 'Country' = ANY(:p0) and CAST(d.data ->> 'Index' as integer) >= :p1); | [BE, LU] String Array|Text | 1 Int32 Integer",
                Where + "(d.data ->> 'Country' = ANY(:p0) and CAST(d.data ->> 'Index' as integer) >= :p1); | [BE, LU] String Array|Text | 2 Int32 Integer");
        Previews(static call => new MartenOrdersInFranceFrom { MinimumIndex = call })
            .Should().Equal(
                Where + "(d.data ->> 'Country' = :p0 and CAST(d.data ->> 'Index' as integer) >= :p1); | FR String Text | 0 Int32 Integer",
                Where + "(d.data ->> 'Country' = :p0 and CAST(d.data ->> 'Index' as integer) >= :p1); | FR String Text | 1 Int32 Integer",
                Where + "(d.data ->> 'Country' = :p0 and CAST(d.data ->> 'Index' as integer) >= :p1); | FR String Text | 2 Int32 Integer");
    }

    [Fact]
    public void The_Value_of_a_value_object_compared_with_a_member_of_its_underlying_type_is_bound_on_every_call()
    {
        Previews(static call => new MartenOrdersByIbanText { Iban = $"T{call}" })
            .Should().Equal(
                Where + "d.data ->> 'Iban' = :p0; | T0 String Text",
                Where + "d.data ->> 'Iban' = :p0; | T1 String Text",
                Where + "d.data ->> 'Iban' = :p0; | T2 String Text");
        Previews(static call => new MartenOrdersAbovePageNumber { Minimum = call })
            .Should().Equal(
                Where + "CAST(d.data ->> 'Page' as integer) > :p0; | 0 Int32 Integer",
                Where + "CAST(d.data ->> 'Page' as integer) > :p0; | 1 Int32 Integer",
                Where + "CAST(d.data ->> 'Page' as integer) > :p0; | 2 Int32 Integer");
        Previews(static call => new MartenOrdersAboveQuantityNumber { Minimum = call + 5 })
            .Should().Equal(
                Where + "CAST(d.data ->> 'Quantity' as smallint) > :p0; | 5 Int32 Integer",
                Where + "CAST(d.data ->> 'Quantity' as smallint) > :p0; | 6 Int32 Integer",
                Where + "CAST(d.data ->> 'Quantity' as smallint) > :p0; | 7 Int32 Integer");
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public void A_single_value_object_a_member_of_the_query_type_holds_is_refused_by_Marten_on_every_call(int call)
    {
        using var store = Store();

        // Marten publishes no plan for a query type it refuses: every call refuses, the first and the later ones alike.
        for (var previous = 0; previous < call; previous++)
        {
            RefusedByMarten(store, new MartenOrdersByIban { Iban = Iban.Create("FR7630006000011234567890189") }, "Iban Iban");
        }

        RefusedByMarten(store, new MartenOrdersByIban { Iban = Iban.Create("DE89370400440532013000") }, "Iban Iban");
        RefusedByMarten(store, new MartenOrdersAbovePage { Minimum = PageNumber.Create(call + 1) }, "PageNumber Minimum");
        RefusedByMarten(store, new MartenOrdersByCustomerFrom { Customer = CustomerId.Create(Guid.CreateVersion7()), MinimumIndex = call }, "CustomerId Customer");
        RefusedByMarten(store, new MartenOrdersByIbanPlanned { Iban = Iban.Create("FR7630006000011234567890189") }, "Iban Iban");
        RefusedByMarten(store, new MartenOrdersOnEitherPage { First = PageNumber.Create(1), Second = PageNumber.Create(call + 2) }, "PageNumber First, PageNumber Second");
    }

    /// <summary>
    /// What the query does with a single value object does not matter: compared with a member of a child collection, sought
    /// in a collection the document holds, its <c>Value</c> compared with a member of another type or handed to a string
    /// method, none of which reaches code of the package, Marten refuses the query type as it does without the package.
    /// </summary>
    /// <param name="call">How many calls ran before.</param>
    [Theory]
    [MemberData(nameof(Calls))]
    public void Every_use_of_a_single_value_object_is_refused_by_Marten_on_every_call(int call)
    {
        using var store = Store();
        var iban = Iban.Create(call == 1 ? "DE89370400440532013000" : "FR7630006000011234567890189");

        RefusedByMarten(store, new MartenOrdersByLineTag { Tag = Label.Create($"tag {call}") }, "Label Tag");
        RefusedByMarten(store, new MartenOrdersWithLinesAbove { Minimum = Quantity.Create((short)(call + 1)) }, "Quantity Minimum");
        RefusedByMarten(store, new MartenOrdersHoldingCountry { Country = call == 1 ? CountryCode.Belgium : CountryCode.France }, "CountryCode Country");
        RefusedByMarten(store, new MartenOrdersNotedWithIban { Iban = iban }, "Iban Iban");
        RefusedByMarten(store, new MartenOrdersWithIbanPrefix { Prefix = iban }, "Iban Prefix");
        RefusedByMarten(store, new MartenOrdersAtPageIndex { Page = PageNumber.Create(call + 1) }, "PageNumber Page");
    }

    [Fact]
    public void A_generic_construction_is_refused_by_Marten_whether_or_not_the_store_was_taught_it()
    {
        using (var taught = MartenHarness.Offline(static options => options.UseValueObjects().UseValueObject<Reference<MartenCompiledOwner>, string>()))
        {
            FluentActions.Invoking(() => MartenHarness.Preview(taught, new MartenOrdersByReference { Reference = Reference<MartenCompiledOwner>.Create("R1") }))
                .Should().Throw<InvalidCompiledQueryException>().WithMessage("Members Reference<*MartenCompiledOwner> Reference cannot be used as parameters to a compiled query*");
        }

        using var store = Store();
        FluentActions.Invoking(() => MartenHarness.Preview(store, new MartenOrdersByUnknownReference { Reference = Reference<MartenUnknownOwner>.Create("R1") }))
            .Should().Throw<InvalidCompiledQueryException>().WithMessage("Members Reference<*MartenUnknownOwner> Reference cannot be used as parameters to a compiled query*");
    }

    [Fact]
    public void A_nullable_value_object_member_is_refused_by_Marten_as_a_nullable_primitive_is()
    {
        using var store = Store();

        FluentActions.Invoking(() => MartenHarness.Preview(store, new MartenOrdersByMaybeIban { Iban = Iban.Create("FR7630006000011234567890189") }))
            .Should().Throw<InvalidCompiledQueryException>().WithMessage("Members Nullable<*Iban> Iban cannot be used as parameters to a compiled query*");
    }

    /// <summary>
    /// Marten lists a value object it takes for a document's identity itself, once it has built that document's mapping,
    /// and then accepts a query type holding one: the package refuses its own comparisons, and <c>IsOneOf</c> over single
    /// members, which Marten would freeze.
    /// </summary>
    [Fact]
    public void A_value_object_Marten_lists_as_an_identity_is_refused_by_the_package_once_listed()
    {
        using var store = MartenHarness.Offline(static options =>
        {
            options.UseValueObjects();
            options.UseValueObjectId<MartenAccount, CustomerId, Guid>();
        });
        var first = CustomerId.Create(Guid.Parse("00000001-0000-0000-0000-000000000000"));
        var second = CustomerId.Create(Guid.Parse("00000002-0000-0000-0000-000000000000"));

        RefusedByMarten(store, new MartenOrdersByCustomerFrom { Customer = first, MinimumIndex = 1 }, "CustomerId Customer");
        MartenHarness.Sql<MartenAccount>(store, static accounts => accounts.Where(x => x.Name == "Ada"));

        Refuses(store, new MartenOrdersByCustomerFrom { Customer = first, MinimumIndex = 1 }, "MartenOrdersByCustomerFrom", "CustomerId", "Customer");
        Refuses(store, new MartenOrdersOfEitherCustomer { First = first, Second = second }, "MartenOrdersOfEitherCustomer", "CustomerId", "First");
    }

    [Fact]
    public void Values_the_package_cannot_read_off_a_member_of_the_query_type_are_refused()
    {
        using var store = Store();

        ReadsOtherwise(store, new MartenOrdersOnPagesThroughALocal { Pages = [PageNumber.Create(1)] }, "MartenOrdersOnPagesThroughALocal", "PageNumber", "Pages");
        ReadsOtherwise(store, new MartenOrdersInMaybeCountriesThroughALocal { Countries = [CountryCode.France] }, "MartenOrdersInMaybeCountriesThroughALocal", "CountryCode", "Countries");
        FluentActions.Invoking(() => MartenHarness.Preview(store, new MartenOrdersInMaybeCountries { Countries = [CountryCode.France] }))
            .Should().Throw<NotSupportedException>().WithMessage(
                "The compiled query MartenOrdersInMaybeCountries takes nullable CountryCode values as a parameter, through its member "
                + "Countries, whose null test Marten would plan from the first call. Declare the member as a collection of CountryCode.");
    }

    /// <summary>
    /// A constant collection stays a constant of the plan, unless the query type also holds a collection of its value
    /// object: the package cannot tell it from values read through a local, and refuses it. A single constant beside such a
    /// collection stays a constant, and the collection is bound.
    /// </summary>
    [Fact]
    public void A_constant_collection_beside_a_collection_member_of_its_value_object_is_refused_and_a_single_constant_kept()
    {
        using var store = Store();

        ReadsOtherwise(store, new MartenOrdersInBeneluxAndCountries { Countries = [CountryCode.France] }, "MartenOrdersInBeneluxAndCountries", "CountryCode", "Countries");
        Previews(static call => new MartenOrdersInFranceAmongCountries { Countries = call == 1 ? [CountryCode.Belgium] : [CountryCode.France] })
            .Should().Equal(
                Where + "(d.data ->> 'Origin' = :p0 and d.data ->> 'Country' = ANY(:p1)); | FR String Text | [FR] String Array|Text",
                Where + "(d.data ->> 'Origin' = :p0 and d.data ->> 'Country' = ANY(:p1)); | FR String Text | [BE] String Array|Text",
                Where + "(d.data ->> 'Origin' = :p0 and d.data ->> 'Country' = ANY(:p1)); | FR String Text | [FR] String Array|Text");
    }

    [Fact]
    public void A_compiled_query_that_takes_no_value_object_is_planned_as_Marten_plans_it()
        => Previews(static call => new MartenOrdersFrom { MinimumIndex = call })
            .Should().Equal(
                Where + "CAST(d.data ->> 'Index' as integer) >= :p0; | 0 Int32 Integer",
                Where + "CAST(d.data ->> 'Index' as integer) >= :p0; | 1 Int32 Integer",
                Where + "CAST(d.data ->> 'Index' as integer) >= :p0; | 2 Int32 Integer");

    private static DocumentStore Store() => MartenHarness.Offline(static options => options.UseValueObjects());

    private static List<string> Previews<TQuery>(Func<int, TQuery> query)
        where TQuery : ICompiledListQuery<MartenOrder>
    {
        using var store = Store();
        return [.. Calls.Select(call => MartenHarness.Preview(store, query(call)).Description)];
    }

    private static void Refuses<TQuery>(IDocumentStore store, TQuery query, string queryName, string valueObject, string member)
        where TQuery : ICompiledListQuery<MartenOrder>
        => FluentActions.Invoking(() => MartenHarness.Preview(store, query))
            .Should().Throw<NotSupportedException>().WithMessage(
                $"The compiled query {queryName} takes {valueObject} as a parameter, through its member {member}, which Marten cannot "
                + "bind: it binds no parameter of a value type, and would answer every later call with the value of the first. "
                + "Declare the member as the underlying type and compare the value object's Value with it: "
                + "x.Member.Value == Parameter.");

    private static void ReadsOtherwise<TQuery>(IDocumentStore store, TQuery query, string queryName, string valueObject, string member)
        where TQuery : ICompiledListQuery<MartenOrder>
        => FluentActions.Invoking(() => MartenHarness.Preview(store, query))
            .Should().Throw<NotSupportedException>().WithMessage(
                $"The compiled query {queryName} reads values of {valueObject} other than straight off its member {member}, which "
                + $"Marten would plan once and answer every later call with: read them in QueryIs as {member}.Contains(x.Member) or "
                + $"x.Member.IsOneOf({member}), which the package binds on every call.");

    private static void RefusedByMarten<TQuery>(IDocumentStore store, TQuery query, string members)
        where TQuery : ICompiledListQuery<MartenOrder>
        => FluentActions.Invoking(() => MartenHarness.Preview(store, query))
            .Should().Throw<InvalidCompiledQueryException>().WithMessage($"Members {members} cannot be used as parameters to a compiled query");
}
