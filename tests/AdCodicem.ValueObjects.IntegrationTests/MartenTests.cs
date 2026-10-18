using System.Linq.Expressions;
using System.Text.Json;
using AdCodicem.ValueObjects.IntegrationTests.Fixtures;
using AdCodicem.ValueObjects.Marten;
using AdCodicem.ValueObjects.UnitTests.Domain;
using JasperFx.Events.Projections;
using Marten;
using Marten.Exceptions;
using Marten.Patching;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using AutoCreate = JasperFx.AutoCreate;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// Value objects in Marten on PostgreSQL under <c>UseValueObjects()</c>: every query answers what the same query over a
/// member of the underlying type answers, and what the documents in memory answer; duplicated fields are columns of the
/// underlying type; a stored value the rules refuse is refused with its code however it is read; and a compiled query
/// binds each call's collection of value objects, and Marten refuses one taking a single value object.
/// </summary>
/// <remarks>
/// What Marten writes is tested offline in the unit suite; what only a server shows is the rows each query finds, the
/// columns it creates and the values it hands back. Each test works in a schema of its own, cleared first.
/// </remarks>
public sealed class MartenTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private const string French = "FR7630006000011234567890189";

    private const string German = "DE89370400440532013000";

    private const string British = "GB82WEST12345698765432";

    private static readonly string[] Ibans = [French, German, British];

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_underlying_type_is_queried_as_the_primitive_it_replaces()
    {
        await QueriedAsItsValueAsync<Consent, bool>(Consent.Create(false), Consent.Create(true), Consent.Create(true));
        await QueriedAsItsValueAsync<Grade, char>(Grade.Create('A'), Grade.Create('C'), Grade.Create('F'));
        await QueriedAsItsValueAsync<Adjustment, sbyte>(Adjustment.Create(-5), Adjustment.Create(0), Adjustment.Create(7));
        await QueriedAsItsValueAsync<Score, byte>(Score.Create(10), Score.Create(50), Score.Create(100));
        await QueriedAsItsValueAsync<PageNumber, int>(PageNumber.Create(1), PageNumber.Create(3), PageNumber.Create(70));
        await QueriedAsItsValueAsync<SequenceNumber, uint>(SequenceNumber.Create(4), SequenceNumber.Create(4_000_000), SequenceNumber.Create(4_000_000_000));
        await QueriedAsItsValueAsync<FileSize, long>(FileSize.Create(0), FileSize.Create(9_000_000_000), FileSize.Create(9_000_000_001));
        await QueriedAsItsValueAsync<Amount, decimal>(Amount.Create(0.5m), Amount.Create(12.5m), Amount.Create(1250m));
        await QueriedAsItsValueAsync<Latitude, double>(Latitude.Create(-45.25), Latitude.Create(0), Latitude.Create(48.85));
        await QueriedAsItsValueAsync<Ratio, float>(Ratio.Create(0f), Ratio.Create(0.25f), Ratio.Create(0.75f));
        await QueriedAsItsValueAsync<EffectiveDate, DateOnly>(EffectiveDate.Create(new DateOnly(2024, 2, 28)), EffectiveDate.Create(new DateOnly(2024, 2, 29)), EffectiveDate.Create(new DateOnly(2025, 1, 1)));
        await QueriedAsItsValueAsync<OpeningTime, TimeOnly>(OpeningTime.Create(new TimeOnly(6, 0)), OpeningTime.Create(new TimeOnly(9, 30)), OpeningTime.Create(new TimeOnly(11, 59, 59)));
        await QueriedAsItsValueAsync<RecordedAt, DateTime>(RecordedAt.Create(new DateTime(2024, 2, 29, 13, 45, 30)), RecordedAt.Create(new DateTime(2024, 2, 29, 13, 45, 31)), RecordedAt.Create(new DateTime(2030, 1, 1)));
        await QueriedAsItsValueAsync<OccurredAt, DateTimeOffset>(
            OccurredAt.Create(new DateTimeOffset(2024, 2, 29, 13, 45, 30, TimeSpan.FromHours(2))),
            OccurredAt.Create(new DateTimeOffset(2024, 2, 29, 13, 45, 30, TimeSpan.Zero)),
            OccurredAt.Create(new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.FromHours(-5))));
        await QueriedAsItsValueAsync<Duration, TimeSpan>(Duration.Create(new TimeSpan(0, 0, 1)), Duration.Create(new TimeSpan(1, 30, 30)), Duration.Create(new TimeSpan(23, 0, 0)));
        await QueriedAsItsValueAsync<CustomerId, Guid>(
            CustomerId.Create(Guid.Parse("00000001-0000-0000-0000-000000000000")),
            CustomerId.Create(Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff")),
            CustomerId.Create(Guid.Parse("ffffffff-0000-0000-0000-000000000000")));
        await QueriedAsItsValueAsync<Iban, string>(Iban.Create(German), Iban.Create(French), Iban.Create(British));
        await QueriedAsItsValueAsync<CountryCode, string>(CountryCode.Belgium, CountryCode.France, CountryCode.Germany);
        await QueriedAsItsValueAsync<DocumentStatus, string>(DocumentStatus.Draft, DocumentStatus.Final, DocumentStatus.Final);
        await QueriedAsItsValueAsync<Label, string>(Label.Create(string.Empty), Label.Create("a label"), Label.Create("b label"));
        await QueriedAsItsValueAsync<PhoneNumber, string>(PhoneNumber.Create("+33612345678"), PhoneNumber.Create("+33698765432"), PhoneNumber.Create("+4915112345678"));
        await QueriedAsItsValueAsync<Bic, string>(Bic.Create("DEUTDEFF"), Bic.Create("BNPAFRPP"), Bic.Create("BNPAFRPPXXX"));
        await QueriedAsItsValueAsync<PaymentId, string>(PaymentId.New(), PaymentId.New(), PaymentId.New());
    }

    [Fact]
    public async Task A_value_object_over_a_type_Marten_maps_to_no_column_is_stored_and_refused_in_a_query_as_its_primitive_is()
    {
        await RefusedAsItsValueAsync<Port, ushort>(Port.Create(8080));
        await RefusedAsItsValueAsync<ByteCount, ulong>(ByteCount.Create(9_000_000_000));
        await RefusedAsItsValueAsync<LedgerBalance, Int128>(LedgerBalance.Create(5));
        await RefusedAsItsValueAsync<Fingerprint, UInt128>(Fingerprint.Create(5));
    }

    [Fact]
    public async Task A_DateTime_of_UTC_kind_is_refused_by_Npgsql_as_for_its_primitive()
    {
        using var store = await StoreAsync("marten_utc", static options => options.UseValueObjects());
        var utc = RecordedAt.Create(new DateTime(2024, 2, 29, 13, 45, 30, DateTimeKind.Utc));
        await using var session = store.QuerySession();

        var primitive = await FluentActions.Awaiting(() => session.Query<MartenTwinDocument<RecordedAt, DateTime>>().Where(x => x.Raw == utc.Value).ToListAsync(Cancellation))
            .Should().ThrowAsync<Exception>();
        var valueObject = await FluentActions.Awaiting(() => session.Query<MartenTwinDocument<RecordedAt, DateTime>>().Where(x => x.Item == utc).ToListAsync(Cancellation))
            .Should().ThrowAsync<Exception>();

        valueObject.Which.GetType().Should().Be(primitive.Which.GetType());
        valueObject.Which.InnerException?.GetType().Should().Be(primitive.Which.InnerException?.GetType());
    }

    [Fact]
    public async Task Every_shape_answers_what_the_documents_answer_in_memory()
    {
        using var store = await StoreAsync("marten_shapes", static options => options.UseValueObjects());
        var shipments = await StoreShipmentsAsync(store);
        await using var session = store.QuerySession();
        var fr = Iban.Create(French);
        var page = PageNumber.Create(2);
        var countries = new List<CountryCode> { CountryCode.Germany, CountryCode.Belgium };
        var withNull = new List<PageNumber?> { PageNumber.Create(1), null };
        var tag = Label.Create("fragile");
        var beyondAByte = 300;

        async Task Answers(Expression<Func<MartenShipment, bool>> filter)
        {
            var rows = await session.Query<MartenShipment>().Where(filter).ToListAsync(Cancellation);
            rows.Select(static x => x.Index).Order().Should().Equal(shipments.Where(filter.Compile()).Select(static x => x.Index), "{0} answers as in memory", filter);
        }

        await Answers(x => x.Iban == fr);
        await Answers(x => x.Iban != fr);
        await Answers(x => x.Iban.Value == French);
        await Answers(x => x.Iban.Value.StartsWith("FR"));
        await Answers(x => x.Iban.Value.EndsWith("32"));
        await Answers(x => x.Iban.Value.Contains("3704"));
        await Answers(x => x.Iban.Value.Equals("fr7630006000011234567890189", StringComparison.OrdinalIgnoreCase));
        await Answers(x => string.IsNullOrEmpty(x.Label.Value));
        await Answers(x => x.Page > page);
        await Answers(x => x.Page.Value <= 2);
        await Answers(x => !(x.Page > page));
        await Answers(x => x.Amount >= Amount.Create(20m));
        await Answers(x => x.Customer == CustomerId.Create(Guid.Parse("00000002-0000-0000-0000-000000000000")));
        await Answers(x => x.Consent == Consent.Create(true));
        await Answers(x => x.Effective < EffectiveDate.Create(new DateOnly(2024, 3, 1)));
        await Answers(x => x.At > OccurredAt.Create(new DateTimeOffset(2024, 1, 1, 14, 0, 0, TimeSpan.FromHours(2))));
        await Answers(x => x.Grade == Grade.Create('B'));
        await Answers(x => x.Sequence > SequenceNumber.Create(1));
        await Answers(x => x.Country == CountryCode.Germany);
        await Answers(x => countries.Contains(x.Country));
        await Answers(x => countries.ToArray().Contains(x.Country));
        await Answers(x => x.Country.IsOneOf(countries));
        await Answers(x => x.Country.In(CountryCode.France));
        await Answers(x => withNull.Contains(x.MaybePage));
        await Answers(x => x.Countries.Contains(CountryCode.Luxembourg));
        await Answers(x => x.Parcels.Any(p => p.Tag == tag));
        await Answers(x => x.Parcels.Any(p => p.Tag.Value == "fragile"));
        await Answers(x => x.Parcels.Any(p => p.Page > page));
        await Answers(x => x.Parcels.Any(p => p.Score.Value == 20));
        await Answers(x => x.Parcels.Any(p => p.Grade.Value == 'C'));
        await Answers(x => x.Parcels.Any(p => p.Adjustment.Value == -1));
        await Answers(x => x.Parcels.Any(p => p.Quantity.Value == 11));
        await Answers(x => x.Parcels.Any(p => p.Score.Value == beyondAByte));
        await Answers(x => x.Parcels.Any(p => p.Adjustment.Value == 1 && p.Score == Score.Create(30)));
        await Answers(x => x.Backup == null);
        await Answers(x => x.Backup == fr);
        await Answers(x => x.MaybePage > page);
        await Answers(x => x.MaybePage.HasValue);

        (await session.Query<MartenShipment>().OrderByDescending(static x => x.Page).ThenBy(static x => x.Iban).Select(static x => x.Index).ToListAsync(Cancellation))
            .Should().Equal(shipments.OrderByDescending(static x => x.Page).ThenBy(static x => x.Iban).Select(static x => x.Index));
        (await session.Query<MartenShipment>().OrderBy(static x => x.Index).Select(static x => x.Page).ToListAsync(Cancellation))
            .Should().Equal(shipments.Select(static x => x.Page));
#pragma warning disable CS8714 // Marten's ToListAsync declares notnull; a nullable member projected is what this reads.
        (await session.Query<MartenShipment>().OrderBy(static x => x.Index).Select(static x => x.Backup).ToListAsync(Cancellation))
            .Should().Equal(shipments.Select(static x => x.Backup));
#pragma warning restore CS8714
        (await session.Query<MartenShipment>().MaxAsync(static x => x.Page, Cancellation)).Should().Be(shipments.Max(static x => x.Page));
    }

    [Fact]
    public async Task Duplicated_fields_are_columns_of_the_underlying_type_written_by_a_bulk_insert_too()
    {
        using var store = await StoreAsync("marten_duplicated", static options =>
        {
            options.UseValueObjects();
            options.Schema.For<MartenShipment>()
                .Duplicate(x => x.Iban).Duplicate(x => x.Backup).Duplicate(x => x.Page).Duplicate(x => x.Amount)
                .Duplicate(x => x.Customer).Duplicate(x => x.Consent).Duplicate(x => x.Effective).Duplicate(x => x.Grade)
                .Duplicate(x => x.Sequence).Duplicate(x => x.Label);
        });
        var shipments = Shipments();
        await store.BulkInsertAsync(shipments, cancellation: Cancellation);

        (await RowsAsync("""
            select column_name || ' ' || data_type || coalesce('(' || character_maximum_length || ')', '')
            from information_schema.columns
            where table_schema = 'marten_duplicated' and table_name = 'mt_doc_martenshipment'
              and column_name in ('iban', 'backup', 'page', 'amount', 'customer', 'consent', 'effective', 'grade', 'sequence', 'label')
            order by column_name
            """)).Should().Equal(
            "amount numeric",
            "backup character varying(34)",
            "consent boolean",
            "customer uuid",
            "effective date",
            "grade text",
            "iban character varying(34)",
            "label character varying(200)",
            "page integer",
            "sequence oid");
        (await RowsAsync("select coalesce(iban, '-') || ' ' || coalesce(backup, 'null') || ' ' || page || ' ' || sequence from marten_duplicated.mt_doc_martenshipment order by page"))
            .Should().Equal(shipments.OrderBy(static x => x.Page).Select(static x => $"{x.Iban.Value} {(x.Backup is { } backup ? backup.Value : "null")} {x.Page.Value} {x.Sequence.Value}"));

        await using var session = store.QuerySession();
        var asked = new[] { Iban.Create(German) };
        (await session.Query<MartenShipment>().Where(x => asked.Contains(x.Iban)).Select(static x => x.Index).ToListAsync(Cancellation))
            .Should().BeEquivalentTo(shipments.Where(x => asked.Contains(x.Iban)).Select(static x => x.Index));
        (await session.Query<MartenShipment>().Where(static x => x.Iban.Value.StartsWith("GB")).Select(static x => x.Index).ToListAsync(Cancellation))
            .Should().BeEquivalentTo(shipments.Where(static x => x.Iban.Value.StartsWith("GB", StringComparison.Ordinal)).Select(static x => x.Index));

        // Marten's own limit: its duplicated field is not one of its value-type members, and builds the selector of a
        // projection from the field's type, PageNumber?, which the query's result type is not.
        (await FluentActions.Awaiting(() => session.Query<MartenShipment>().Select(static x => x.Page).ToListAsync(Cancellation)).Should().ThrowAsync<NotSupportedException>())
            .Which.Message.Should().StartWith("Marten does not know how to use result type");
    }

    [Fact]
    public async Task A_computed_index_over_a_value_object_over_a_date_is_created_and_serves_the_query()
    {
        using var store = await StoreAsync("marten_indexed", static options =>
        {
            options.UseValueObjects();
            options.Schema.For<MartenShipment>().Index(x => x.Effective).Index(x => x.At);
        });
        var shipments = await StoreShipmentsAsync(store);
        var before = EffectiveDate.Create(new DateOnly(2024, 3, 1));

        await using var session = store.QuerySession();
        (await session.Query<MartenShipment>().Where(x => x.Effective < before).Select(static x => x.Index).ToListAsync(Cancellation))
            .Should().BeEquivalentTo(shipments.Where(x => x.Effective < before).Select(static x => x.Index));
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Cancellation);
        await using (var disable = new NpgsqlCommand("set enable_seqscan = off", connection))
        {
            await disable.ExecuteNonQueryAsync(Cancellation);
        }

        await using var explain = session.Query<MartenShipment>().Where(x => x.Effective < before).ToCommand();
        explain.CommandText = "explain " + explain.CommandText;
        explain.Connection = connection;
        List<string> plan = [];
        await using (var reader = await explain.ExecuteReaderAsync(Cancellation))
        {
            while (await reader.ReadAsync(Cancellation))
            {
                plan.Add(reader.GetString(0));
            }
        }

        plan.Should().Contain(static line => line.Contains("mt_doc_martenshipment_idx_effective", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_stored_value_the_rules_refuse_is_refused_with_its_code_however_it_is_read()
    {
        using var store = await StoreAsync("marten_refused", static options => options.UseValueObjects());
        var shipment = Shipments()[0];
        await using (var write = store.LightweightSession())
        {
            write.Store(shipment);
            await write.SaveChangesAsync(Cancellation);
        }

        await ExecuteAsync("update marten_refused.mt_doc_martenshipment set data = jsonb_set(data, '{Page}', '0')");
        await using var session = store.QuerySession();

        foreach (var read in new Func<Task>[]
        {
            () => session.LoadAsync<MartenShipment>(shipment.Id, Cancellation),
            () => session.Query<MartenShipment>().Where(static x => x.Index >= 0).ToListAsync(Cancellation),
            () => session.Query<MartenShipment>().Select(static x => x.Page).ToListAsync(Cancellation),
            () => session.Query<MartenShipment>().MaxAsync(static x => x.Page, Cancellation),
        })
        {
            var refusal = (await FluentActions.Awaiting(read).Should().ThrowAsync<ValueObjectJsonException>()).Which;
            refusal.Message.Should().Be("The value is not a valid PageNumber: The value must be greater than or equal to 1.");
            refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
            ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue();
            code.Should().Be(ValueObjectErrorCodes.OutOfRange);
        }
    }

    [Fact]
    public async Task Events_are_queried_projected_and_aggregated_through_the_rules()
    {
        using var store = await StoreAsync("marten_events", static options =>
        {
            options.UseValueObjects();
            options.Projections.Snapshot<MartenShipmentView>(SnapshotLifecycle.Inline);
        });
        var id = Guid.CreateVersion7();
        await using (var write = store.LightweightSession())
        {
            write.Events.StartStream<MartenShipmentView>(id, new MartenShipped(id, Iban.Create(French), PageNumber.Create(3), CountryCode.France), new MartenRepaged(PageNumber.Create(9)));
            await write.SaveChangesAsync(Cancellation);
        }

        await using var session = store.QuerySession();
        (await session.Events.QueryRawEventDataOnly<MartenShipped>().Where(static x => x.Page > PageNumber.Create(2)).ToListAsync(Cancellation))
            .Should().ContainSingle().Which.Iban.Should().Be(Iban.Create(French));
        (await session.Query<MartenShipmentView>().Where(static x => x.Page > PageNumber.Create(5)).ToListAsync(Cancellation))
            .Should().ContainSingle().Which.Page.Should().Be(PageNumber.Create(9));
        (await session.Events.AggregateStreamAsync<MartenShipmentView>(id, token: Cancellation))!.Page.Should().Be(PageNumber.Create(9));

        await ExecuteAsync("update marten_events.mt_events set data = jsonb_set(data, '{Page}', '0') where type = 'marten_shipped'");
        (await FluentActions.Awaiting(() => session.Events.QueryRawEventDataOnly<MartenShipped>().ToListAsync(Cancellation))
            .Should().ThrowAsync<ValueObjectJsonException>()).Which.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
        var refusal = (await FluentActions.Awaiting(() => session.Events.FetchStreamAsync(id, token: Cancellation))
            .Should().ThrowAsync<Exception>()).Which;
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue("{0} carries the rule's code", refusal);
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);
        refusal.InnerException.Should().BeOfType<ValueObjectJsonException>();
    }

    [Fact]
    public async Task A_value_object_identity_is_loaded_compared_and_listed_through_its_value()
    {
        using var store = await StoreAsync("marten_identity", static options =>
        {
            options.UseValueObjects();
            options.UseValueObjectId<MartenCustomer, CustomerId, Guid>();
            options.UseValueObjectId<MartenAccount, Label, string>();
        });
        var first = CustomerId.Create(Guid.Parse("00000001-0000-0000-0000-000000000000"));
        var second = CustomerId.Create(Guid.Parse("00000002-0000-0000-0000-000000000000"));
        await using (var write = store.LightweightSession())
        {
            write.Store(new MartenCustomer { Id = first, Name = "Ada" }, new MartenCustomer { Id = second, Name = "Grace" });
            write.Store(new MartenAccount { Id = Label.Create("ACC-42"), Holder = "Ada" });
            await write.SaveChangesAsync(Cancellation);
        }

        await using var session = store.QuerySession();
        (await session.LoadAsync<MartenCustomer>(second, Cancellation))!.Name.Should().Be("Grace");
        (await session.LoadAsync<MartenAccount>(Label.Create("ACC-42"), Cancellation))!.Holder.Should().Be("Ada");
        (await session.Query<MartenCustomer>().Where(x => x.Id == first).Select(static x => x.Name).ToListAsync(Cancellation)).Should().Equal("Ada");
        (await session.Query<MartenCustomer>().Where(x => x.Id.IsOneOf(new[] { first, second })).CountAsync(Cancellation)).Should().Be(2);

        await ExecuteAsync("update marten_identity.mt_doc_martencustomer set id = '00000000-0000-0000-0000-000000000000', data = jsonb_set(data, '{Id}', '\"00000000-0000-0000-0000-000000000000\"') where id = '00000001-0000-0000-0000-000000000000'");
        await using var refused = store.QuerySession();
        (await FluentActions.Awaiting(() => refused.Query<MartenCustomer>().ToListAsync(Cancellation)).Should().ThrowAsync<ValueObjectJsonException>())
            .Which.ErrorCode.Should().Be(ValueObjectErrorCodes.Required);
        (await FluentActions.Awaiting(() => refused.Query<MartenCustomer>().Select(static x => x.Id).ToListAsync(Cancellation)).Should().ThrowAsync<ValueObjectException>())
            .Which.ErrorCode.Should().Be(ValueObjectErrorCodes.Required, "Marten builds an identity it projects through the type's own Create");
    }

    /// <summary>
    /// Marten takes a struct for an identity when exactly one of its public properties, static ones counted, has a type an
    /// identity may have: an entity identifier has its static <c>Length</c> and <c>Prefix</c> beside its value, and the
    /// sample's IBAN its own <c>CountryCode</c>.
    /// </summary>
    [Fact]
    public async Task A_value_object_with_another_public_property_of_an_identity_type_is_no_identity_Marten_finds()
    {
        using var store = await StoreAsync("marten_no_identity", static options => options.UseValueObjects());
        await using var session = store.LightweightSession();

        FluentActions.Invoking(() => session.Store(new MartenPayment { Id = PaymentId.New() }))
            .Should().Throw<InvalidDocumentException>().WithMessage("*MartenPayment*");
        FluentActions.Invoking(() => session.Store(new MartenIbanAccount { Id = Iban.Create(French) }))
            .Should().Throw<InvalidDocumentException>().WithMessage("*MartenIbanAccount*");
    }

    [Fact]
    public async Task A_compiled_query_binds_each_call_s_collection_of_value_objects_and_the_Value_of_one()
    {
        using var store = await StoreAsync("marten_compiled", static options => options.UseValueObjects());
        var shipments = await StoreShipmentsAsync(store);

        await AnswersEachCallAsync(store, shipments, call => new MartenShipmentsByIbanText { Iban = Ibans[call] });
        await AnswersEachCallAsync(store, shipments, call => new MartenShipmentsAbovePageNumber { Minimum = call + 1 });
        await AnswersEachCallAsync(store, shipments, call => new MartenShipmentsOnPages { Pages = [PageNumber.Create(call + 1), PageNumber.Create(4 - call)] });
        await AnswersEachCallAsync(store, shipments, call => new MartenShipmentsInCountries { Countries = call == 1 ? [CountryCode.Germany] : [CountryCode.France, CountryCode.Belgium] });
        await AnswersEachCallAsync(store, shipments, call => new MartenShipmentsOfAmounts { Amounts = [Amount.Create(10m * (call + 1))] });
        await AnswersEachCallAsync(store, shipments, call => new MartenShipmentsOfCustomers { Customers = [CustomerId.Create(Guid.Parse($"0000000{call + 1}-0000-0000-0000-000000000000"))] });
        await AnswersEachCallAsync(store, shipments, call => new MartenShipmentsEffectiveOn { Dates = [Shipments()[call].Effective, Shipments()[3].Effective], MinimumIndex = call });
        await AnswersEachCallAsync(store, shipments, call => new MartenShipmentsAt { Moments = [Shipments()[call].At] });
        await AnswersEachCallAsync(store, shipments, call => new MartenShipmentsOfConsents { Consents = [Consent.Create(call == 1)] });
    }

    /// <summary>
    /// A single value object is refused by Marten whatever the query does with it, on every call, before any row is read:
    /// compared with a member, with a member of a child collection, sought in a collection the document holds, its
    /// <c>Value</c> compared with a member of another type or handed to a string method.
    /// </summary>
    [Fact]
    public async Task Every_use_of_a_single_value_object_in_a_compiled_query_is_refused_by_Marten_on_every_call()
    {
        using var store = await StoreAsync("marten_compiled_single", static options => options.UseValueObjects());
        await StoreShipmentsAsync(store);

        await RefusedByMartenAsync(store, call => new MartenShipmentsByIban { Iban = Iban.Create(Ibans[call]) }, "Iban Iban");
        await RefusedByMartenAsync(store, call => new MartenShipmentsByParcelTag { Tag = Label.Create(call == 1 ? "sturdy" : "fragile") }, "Label Tag");
        await RefusedByMartenAsync(store, call => new MartenShipmentsWithParcelsAbove { Page = PageNumber.Create(call + 1) }, "PageNumber Page");
        await RefusedByMartenAsync(store, call => new MartenShipmentsHoldingCountry { Country = call == 1 ? CountryCode.France : CountryCode.Luxembourg }, "CountryCode Country");
        await RefusedByMartenAsync(store, call => new MartenShipmentsNotedWithIban { Iban = Iban.Create(Ibans[call]) }, "Iban Iban");
        await RefusedByMartenAsync(store, call => new MartenShipmentsWithIbanPrefix { Prefix = Iban.Create(Ibans[call]) }, "Iban Prefix");
        await RefusedByMartenAsync(store, call => new MartenShipmentsAtPageIndex { Page = PageNumber.Create(call + 1) }, "PageNumber Page");
    }

    /// <summary>
    /// A value object held by a member Marten is told to ignore is read by neither Marten nor the package: beside it, the
    /// <c>.Value</c> form, a constant collection of the same value object and a collection answer each call's own values.
    /// </summary>
    [Fact]
    public async Task A_compiled_query_beside_a_value_object_Marten_ignores_answers_each_call_s_own_values()
    {
        using var store = await StoreAsync("marten_compiled_ignored", static options => options.UseValueObjects());
        var shipments = await StoreShipmentsAsync(store);

        await AnswersEachCallAsync(store, shipments, call => new MartenShipmentsByIbanTextBesideIgnored { Noted = Iban.Create(Ibans[2 - call]), Iban = Ibans[call] });
        await AnswersEachCallAsync(store, shipments, call => new MartenShipmentsInBeneluxBesideIgnored { Ignored = [CountryCode.France], Preferred = CountryCode.Germany, MinimumIndex = call + 1 });
        await AnswersEachCallAsync(store, shipments, call => new MartenShipmentsOnPagesBesideIgnored { Ignored = PageNumber.Create(9), Pages = [PageNumber.Create(call + 1), PageNumber.Create(4 - call)] });
    }

    /// <summary>
    /// C# compares the <c>.Value</c> of a value object over a <c>byte</c> or a <c>char</c> as an <c>int</c>; in a child
    /// collection's filter, that constant must reach the containment document, or the filter matches every document, and
    /// a deletion or a patch through it reaches them all.
    /// </summary>
    [Fact]
    public async Task A_child_collection_filter_over_a_promoted_Value_deletes_and_patches_only_the_documents_it_matches()
    {
        using var store = await StoreAsync("marten_promoted", static options => options.UseValueObjects());
        var shipments = await StoreShipmentsAsync(store);

        await using (var write = store.LightweightSession())
        {
            write.DeleteWhere<MartenShipment>(static x => x.Parcels.Any(p => p.Score.Value == 20));
            write.Patch<MartenShipment>(static x => x.Parcels.Any(p => p.Grade.Value == 'D')).Set(static x => x.Note, "patched");
            await write.SaveChangesAsync(Cancellation);
        }

        (await RowsAsync("select (data ->> 'Index') || ' ' || coalesce(data ->> 'Note', '') from marten_promoted.mt_doc_martenshipment order by data ->> 'Index'"))
            .Should().Equal(shipments
                .Where(static x => !x.Parcels.Any(p => p.Score.Value == 20))
                .Select(static x => $"{x.Index} {(x.Parcels.Any(p => p.Grade.Value == 'D') ? "patched" : x.Note)}"));
    }

    [Fact]
    public async Task A_batch_planned_from_one_collection_answers_its_own_while_another_session_asks_another()
    {
        using var store = await StoreAsync("marten_batch", static options => options.UseValueObjects());
        var shipments = await StoreShipmentsAsync(store);
        await using var batchSession = store.QuerySession();
        var batch = batchSession.CreateBatchQuery();

        var planned = batch.Query(new MartenShipmentsOnPages { Pages = [PageNumber.Create(1)] });
        await using (var other = store.QuerySession())
        {
            (await other.QueryAsync(new MartenShipmentsOnPages { Pages = [PageNumber.Create(2)] }, Cancellation)).Select(static x => x.Index)
                .Should().BeEquivalentTo(shipments.Where(static x => x.Page == PageNumber.Create(2)).Select(static x => x.Index));
        }

        await batch.Execute(Cancellation);
        (await planned).Select(static x => x.Index).Should().BeEquivalentTo(shipments.Where(static x => x.Page == PageNumber.Create(1)).Select(static x => x.Index));
    }

    /// <summary>
    /// A value-object identity compared with a member of a compiled query is written by Marten's own member, which no code
    /// of the package reaches: every call is answered with the first call's identity, which the guide warns of. The day
    /// this fails, Marten binds it, and the warning goes.
    /// </summary>
    [Fact]
    public async Task A_compiled_query_over_a_value_object_identity_answers_every_call_with_the_first_call_s_identity()
    {
        using var store = await StoreAsync("marten_identity_compiled", static options =>
        {
            options.UseValueObjects();
            options.UseValueObjectId<MartenCustomer, CustomerId, Guid>();
        });
        var first = CustomerId.Create(Guid.Parse("00000001-0000-0000-0000-000000000000"));
        var second = CustomerId.Create(Guid.Parse("00000002-0000-0000-0000-000000000000"));
        await using (var write = store.LightweightSession())
        {
            write.Store(new MartenCustomer { Id = first, Name = "Ada" }, new MartenCustomer { Id = second, Name = "Grace" });
            await write.SaveChangesAsync(Cancellation);
        }

        await using var session = store.QuerySession();
        (await session.QueryAsync(new MartenCustomersById { Id = first }, Cancellation)).Select(static x => x.Name).Should().Equal("Ada");
        (await session.QueryAsync(new MartenCustomersById { Id = second }, Cancellation)).Select(static x => x.Name)
            .Should().Equal(["Ada"], "Marten replays the identity it planned from");
    }

    [Fact]
    public async Task A_patch_writes_the_bare_value_and_a_default_its_type_refuses_is_refused_with_its_code()
    {
        using var store = await StoreAsync("marten_patch", static options => options.UseValueObjects());
        var shipments = await StoreShipmentsAsync(store);

        await using (var write = store.LightweightSession())
        {
            write.Patch<MartenShipment>(shipments[0].Id).Set(static x => x.Page, PageNumber.Create(7));
            write.Patch<MartenShipment>(static x => x.Page > PageNumber.Create(3)).Set(static x => x.Note, "patched");
            await write.SaveChangesAsync(Cancellation);
        }

        (await RowsAsync($"select (data -> 'Page')::text || ' ' || (data ->> 'Note') from marten_patch.mt_doc_martenshipment where id = '{shipments[0].Id}'"))
            .Should().Equal("7 patched");

        await using var refused = store.LightweightSession();
#pragma warning disable VO0010 // The default instance its type refuses, on purpose.
        refused.Patch<MartenShipment>(shipments[1].Id).Set(static x => x.Page, default(PageNumber));
#pragma warning restore VO0010
        var refusal = (await FluentActions.Awaiting(() => refused.SaveChangesAsync(Cancellation)).Should().ThrowAsync<Exception>()).Which;
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue("{0} carries the rule's code", refusal);
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public async Task A_source_generated_context_s_options_write_the_values_and_a_resolver_alone_writes_none()
    {
        using var withTheFactory = await StoreAsync("marten_context", static options =>
        {
            options.UseSystemTextJsonForSerialization(MartenJsonContext.Default.Options);
            options.UseValueObjects();
        });
        await StoreShipmentsAsync(withTheFactory);
        (await RowsAsync("select (data -> 'Page')::text || ' ' || (data -> 'Iban')::text from marten_context.mt_doc_martenshipment order by data ->> 'Index' limit 1"))
            .Should().Equal($"1 \"{French}\"");

        using var resolverOnly = DocumentStore.For(options =>
        {
            options.Connection(fixture.ConnectionString);
            options.DatabaseSchemaName = "marten_resolver";
            options.AutoCreateSchemaObjects = AutoCreate.All;
            ((SystemTextJsonSerializer)options.Serializer()).UseTypeInfoResolver(MartenJsonContext.Default);
        });
        await resolverOnly.Advanced.Clean.CompletelyRemoveAllAsync(Cancellation);
        await using (var write = resolverOnly.LightweightSession())
        {
            write.Store(Shipments()[0]);
            await write.SaveChangesAsync(Cancellation);
        }

        (await RowsAsync("select (data -> 'Page')::text from marten_resolver.mt_doc_martenshipment")).Should().Equal("{}");
    }

    [Fact]
    public async Task AddMarten_with_UseValueObjects_teaches_the_store_a_host_builds()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddMarten(options =>
        {
            options.Connection(fixture.ConnectionString);
            options.DatabaseSchemaName = "marten_host";
            options.AutoCreateSchemaObjects = AutoCreate.All;
            options.Schema.For<MartenShipment>().Duplicate(x => x.Page);
        }).UseValueObjects();
        using var host = builder.Build();
        var store = host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.CompletelyRemoveAllAsync(Cancellation);
        var shipments = await StoreShipmentsAsync(store);

        await using var session = store.QuerySession();
        (await session.Query<MartenShipment>().Where(static x => x.Page > PageNumber.Create(2)).Select(static x => x.Index).ToListAsync(Cancellation))
            .Should().BeEquivalentTo(shipments.Where(static x => x.Page > PageNumber.Create(2)).Select(static x => x.Index));
        (await RowsAsync("select data_type from information_schema.columns where table_schema = 'marten_host' and table_name = 'mt_doc_martenshipment' and column_name = 'page'"))
            .Should().Equal("integer");
    }

    private static List<MartenShipment> Shipments()
        => [.. Enumerable.Range(0, 5).Select(static index => new MartenShipment
        {
            Id = Guid.Parse($"0000000{index + 1}-1111-0000-0000-000000000000"),
            Index = index,
            Iban = Iban.Create(Ibans[index % 3]),
            Backup = index % 2 == 0 ? Iban.Create(Ibans[(index + 1) % 3]) : null,
            Page = PageNumber.Create(index + 1),
            MaybePage = index == 4 ? null : PageNumber.Create(index % 2 + 1),
            Amount = Amount.Create(10m * (index + 1)),
            Customer = CustomerId.Create(Guid.Parse($"0000000{index + 1}-0000-0000-0000-000000000000")),
            Consent = Consent.Create(index % 2 == 1),
            Effective = EffectiveDate.Create(new DateOnly(2024, 2, 27).AddDays(index)),
            At = OccurredAt.Create(new DateTimeOffset(2024, 1, 1, 12 + index, 0, 0, TimeSpan.FromHours(index - 2))),
            Grade = Grade.Create((char)('A' + index)),
            Sequence = SequenceNumber.Create((uint)index),
            Country = index % 3 == 0 ? CountryCode.France : index % 3 == 1 ? CountryCode.Germany : CountryCode.Belgium,
            Label = index == 3 ? Label.Create(string.Empty) : Label.Create($"label {index}"),
            Countries = index % 2 == 0 ? [CountryCode.Luxembourg] : [],
            Parcels =
            [
                new MartenParcel
                {
                    Tag = Label.Create(index == 2 ? "fragile" : "sturdy"),
                    Page = PageNumber.Create(index + 1),
                    Score = Score.Create((byte)(index * 10)),
                    Grade = Grade.Create((char)('A' + index)),
                    Adjustment = Adjustment.Create((sbyte)(index - 2)),
                    Quantity = LineQuantity.Create((short)(index * 5 + 1)),
                },
            ],
        })];

    private static async Task<List<MartenShipment>> StoreShipmentsAsync(IDocumentStore store)
    {
        var shipments = Shipments();
        await using var write = store.LightweightSession();
        write.Store<MartenShipment>(shipments);
        await write.SaveChangesAsync(Cancellation);
        return shipments;
    }

    private static async Task AnswersEachCallAsync<TQuery>(DocumentStore store, List<MartenShipment> shipments, Func<int, TQuery> query)
        where TQuery : MartenShipmentQuery
    {
        for (var call = 0; call < 3; call++)
        {
            var asked = query(call);
            await using var session = store.QuerySession();
            (await session.QueryAsync(asked, Cancellation)).Select(static x => x.Index).Order()
                .Should().Equal(shipments.Where(asked.Answers).Select(static x => x.Index), "call {0} of {1} answers its own values", call, typeof(TQuery).Name);
        }
    }

    private static async Task RefusedByMartenAsync<TQuery>(DocumentStore store, Func<int, TQuery> query, string members)
        where TQuery : MartenShipmentQuery
    {
        for (var call = 0; call < 3; call++)
        {
            await using var session = store.QuerySession();
            (await FluentActions.Awaiting(() => session.QueryAsync(query(call), Cancellation)).Should().ThrowAsync<InvalidCompiledQueryException>())
                .Which.Message.Should().Be($"Members {members} cannot be used as parameters to a compiled query", "call {0} of {1} is refused", call, typeof(TQuery).Name);
        }
    }

    private async Task QueriedAsItsValueAsync<TSelf, TValue>(TSelf first, TSelf second, TSelf third)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var name = typeof(TSelf).Name;
        TSelf[] values = [first, second, third];
        using var store = await StoreAsync($"marten_parity_{name.ToLowerInvariant()}", options =>
        {
            options.UseValueObjects();
            options.Schema.For<MartenTwinDocument<TSelf, TValue>>().DocumentAlias("twin");
        });
        var twins = values.Select((value, index) => new MartenTwinDocument<TSelf, TValue>
        {
            Id = Guid.CreateVersion7(),
            Index = index,
            Item = value,
            Optional = index == 1 ? null : value,
            Raw = value.Value,
        }).ToList();
        await using (var write = store.LightweightSession())
        {
            write.Store<MartenTwinDocument<TSelf, TValue>>(twins);
            await write.SaveChangesAsync(Cancellation);
        }

        await using var session = store.QuerySession();
        var twin = Expression.Parameter(typeof(MartenTwinDocument<TSelf, TValue>), "twin");
        foreach (var value in values)
        {
            foreach (var comparison in (ExpressionType[])[ExpressionType.Equal, ExpressionType.NotEqual, ExpressionType.GreaterThan, ExpressionType.LessThanOrEqual])
            {
                if (Filter<TSelf, TValue>(twin, nameof(MartenTwinDocument<,>.Raw), comparison, value.Value) is not { } raw)
                {
                    continue;
                }

                var oracle = await IndexesAsync(session, raw);
                oracle.Should().Equal(twins.AsQueryable().Where(raw).Select(static x => x.Index), "{0} {1} over the primitive answers as in memory", name, comparison);
                (await IndexesAsync(session, Filter<TSelf, TValue>(twin, nameof(MartenTwinDocument<,>.Item), comparison, value)!))
                    .Should().Equal(oracle, "{0} {1} answers as its primitive", name, comparison);
            }

            (await IndexesAsync(session, Filter<TSelf, TValue>(twin, nameof(MartenTwinDocument<,>.Optional), ExpressionType.Equal, (TSelf?)value)!))
                .Should().Equal(twins.Where(x => Nullable.Equals(x.Optional, value)).Select(static x => x.Index), "{0}? compares as in memory", name);
        }

        // Marten's own Contains hands Npgsql a DateTimeOffset as it is, which Npgsql refuses for a timestamp with time zone at
        // any offset but UTC; the package's hands it in UTC, as Marten's own comparison of one does.
        List<TSelf> some = [first, third];
        List<TValue> someRaws = [.. some.Select(static item => item.Value is DateTimeOffset moment ? (TValue)(object)moment.ToUniversalTime() : item.Value)];
        (await session.Query<MartenTwinDocument<TSelf, TValue>>().Where(x => some.Contains(x.Item)).Select(static x => x.Index).ToListAsync(Cancellation)).Order()
            .Should().Equal(twins.Where(x => some.Contains(x.Item)).Select(static x => x.Index), "a Contains over {0} answers as in memory", name);
        (await session.Query<MartenTwinDocument<TSelf, TValue>>().Where(x => someRaws.Contains(x.Raw)).Select(static x => x.Index).ToListAsync(Cancellation)).Order()
            .Should().Equal(twins.Where(x => someRaws.Contains(x.Raw)).Select(static x => x.Index), "a Contains over the primitive of {0} answers as in memory", name);
        (await session.Query<MartenTwinDocument<TSelf, TValue>>().OrderBy(static x => x.Item).ThenBy(static x => x.Index).Select(static x => x.Index).ToListAsync(Cancellation))
            .Should().Equal(await session.Query<MartenTwinDocument<TSelf, TValue>>().OrderBy(static x => x.Raw).ThenBy(static x => x.Index).Select(static x => x.Index).ToListAsync(Cancellation), "{0} orders as its primitive", name);
        (await session.Query<MartenTwinDocument<TSelf, TValue>>().OrderBy(static x => x.Index).Select(static x => x.Item).ToListAsync(Cancellation))
            .Should().Equal(values, "{0} projects back to itself", name);
#pragma warning disable CS8714 // Marten's ToListAsync declares notnull; a nullable member projected is what this reads.
        (await session.Query<MartenTwinDocument<TSelf, TValue>>().OrderBy(static x => x.Index).Select(static x => x.Optional).ToListAsync(Cancellation))
            .Should().Equal(twins.Select(static x => x.Optional), "{0}? projects back to itself, null included", name);
#pragma warning restore CS8714
    }

    private async Task RefusedAsItsValueAsync<TSelf, TValue>(TSelf value)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var name = typeof(TSelf).Name;
        using var store = await StoreAsync($"marten_unmapped_{name.ToLowerInvariant()}", options =>
        {
            options.UseValueObjects();
            options.Schema.For<MartenTwinDocument<TSelf, TValue>>().DocumentAlias("twin");
        });
        var id = Guid.CreateVersion7();
        await using (var write = store.LightweightSession())
        {
            write.Store(new MartenTwinDocument<TSelf, TValue> { Id = id, Item = value, Raw = value.Value });
            await write.SaveChangesAsync(Cancellation);
        }

        await using var session = store.QuerySession();
        (await session.LoadAsync<MartenTwinDocument<TSelf, TValue>>(id, Cancellation))!.Item.Should().Be(value, "{0} is stored as its value", name);
        var twin = Expression.Parameter(typeof(MartenTwinDocument<TSelf, TValue>), "twin");
        var primitive = await FluentActions.Awaiting(() => IndexesAsync(session, Filter<TSelf, TValue>(twin, nameof(MartenTwinDocument<,>.Raw), ExpressionType.Equal, value.Value)!))
            .Should().ThrowAsync<BadLinqExpressionException>();
        (await FluentActions.Awaiting(() => IndexesAsync(session, Filter<TSelf, TValue>(twin, nameof(MartenTwinDocument<,>.Item), ExpressionType.Equal, value)!))
            .Should().ThrowAsync<BadLinqExpressionException>()).Which.Message.Should().Be(primitive.Which.Message, "{0} is refused as its primitive is", name);
    }

    private static Expression<Func<MartenTwinDocument<TSelf, TValue>, bool>>? Filter<TSelf, TValue>(ParameterExpression twin, string member, ExpressionType comparison, object? value)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var property = Expression.Property(twin, member);
        try
        {
            return Expression.Lambda<Func<MartenTwinDocument<TSelf, TValue>, bool>>(Expression.MakeBinary(comparison, property, Expression.Constant(value, property.Type)), twin);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task<List<int>> IndexesAsync<TSelf, TValue>(IQuerySession session, Expression<Func<MartenTwinDocument<TSelf, TValue>, bool>> filter)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => [.. (await session.Query<MartenTwinDocument<TSelf, TValue>>().Where(filter).Select(static x => x.Index).ToListAsync(Cancellation)).Order()];

    private async Task<DocumentStore> StoreAsync(string schema, Action<StoreOptions> configure)
    {
        var store = DocumentStore.For(options =>
        {
            options.Connection(fixture.ConnectionString);
            options.DatabaseSchemaName = schema;
            options.AutoCreateSchemaObjects = AutoCreate.All;
            configure(options);
        });
        await store.Advanced.Clean.CompletelyRemoveAllAsync(Cancellation);
        return store;
    }

    private async Task<List<string>> RowsAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Cancellation);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(Cancellation);
        List<string> rows = [];
        while (await reader.ReadAsync(Cancellation))
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Cancellation);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Cancellation);
    }
}
