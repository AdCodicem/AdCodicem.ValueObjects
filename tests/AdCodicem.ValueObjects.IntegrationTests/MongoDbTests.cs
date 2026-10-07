using AdCodicem.ValueObjects.IntegrationTests.Fixtures;
using AdCodicem.ValueObjects.MongoDB;
using AdCodicem.ValueObjects.UnitTests.Domain;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// Verifies that value objects are stored by a real MongoDB server as the bare values their underlying types write,
/// that queries over them, and over their <c>.Value</c>, return the documents the same queries over the primitives
/// would, and that a value the type rejects is refused both ways.
/// </summary>
/// <remarks>
/// The serializer's behaviour, and what each query renders, are tested in the unit suite without a server. What only a
/// server shows is that the documents stored are the ones a primitive writes, and that the filters it gets match the
/// documents they should.
/// </remarks>
public sealed class MongoDbTests(MongoDbFixture fixture) : IClassFixture<MongoDbFixture>
{
    private const string French = "FR7630006000011234567890189";

    private const string German = "DE89370400440532013000";

    private static readonly CustomerId Buyer = CustomerId.Create(Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff"));

    private IMongoDatabase Database => fixture.Database("value_objects");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_underlying_type_is_stored_as_the_primitive_it_replaces_and_found_by_its_value()
    {
        await StoredAsItsValueAsync<Consent, bool>(Consent.Create(true));
        await StoredAsItsValueAsync<Grade, char>(Grade.Create('B'));
        await StoredAsItsValueAsync<Adjustment, sbyte>(Adjustment.Create(-5));
        await StoredAsItsValueAsync<Score, byte>(Score.Create(50));
        await StoredAsItsValueAsync<LineQuantity, short>(LineQuantity.Create(42));
        await StoredAsItsValueAsync<Port, ushort>(Port.Create(8080));
        await StoredAsItsValueAsync<PageNumber, int>(PageNumber.Create(3));
        await StoredAsItsValueAsync<SequenceNumber, uint>(SequenceNumber.Create(4_000_000));
        await StoredAsItsValueAsync<FileSize, long>(FileSize.Create(9_000_000_000));
        await StoredAsItsValueAsync<ByteCount, ulong>(ByteCount.Create(9_000_000_000));
        await StoredAsItsValueAsync<Amount, decimal>(Amount.Create(12.5m));
        await StoredAsItsValueAsync<Latitude, double>(Latitude.Create(48.85));
        await StoredAsItsValueAsync<Ratio, float>(Ratio.Create(0.25f));
        await StoredAsItsValueAsync<EffectiveDate, DateOnly>(EffectiveDate.Create(new DateOnly(2024, 2, 29)));
        await StoredAsItsValueAsync<OpeningTime, TimeOnly>(OpeningTime.Create(new TimeOnly(9, 30)));
        await StoredAsItsValueAsync<RecordedAt, DateTime>(RecordedAt.Create(new DateTime(2024, 2, 29, 13, 45, 30, DateTimeKind.Utc)));
        await StoredAsItsValueAsync<OccurredAt, DateTimeOffset>(OccurredAt.Create(new DateTimeOffset(2024, 2, 29, 13, 45, 30, TimeSpan.FromHours(2))));
        await StoredAsItsValueAsync<Duration, TimeSpan>(Duration.Create(new TimeSpan(1, 30, 30)));
        await StoredAsItsValueAsync<CustomerId, Guid>(Buyer);
        await StoredAsItsValueAsync<Iban, string>(Iban.Create(French));
        await StoredAsItsValueAsync<CountryCode, string>(CountryCode.France);
        await StoredAsItsValueAsync<DocumentStatus, string>(DocumentStatus.Final);
        await StoredAsItsValueAsync<Label, string>(Label.Create("a label"));
        await StoredAsItsValueAsync<PaymentId, string>(PaymentId.New());
        await StoredAsItsValueAsync<OwnedCode<MongoOrder>, string>(OwnedCode<MongoOrder>.Create("ORD-1"));
    }

    [Fact]
    public async Task A_value_beyond_the_range_its_primitive_is_stored_in_fails_as_the_primitive_does()
    {
        var primitive = await FailedInsertAsync(new Holder<uint> { Value = uint.MaxValue });
        var valueObject = await FailedInsertAsync(new Holder<SequenceNumber> { Value = SequenceNumber.Create(uint.MaxValue) });

        valueObject.GetType().Should().Be(primitive.GetType());
        valueObject.InnerException.Should().BeOfType<OverflowException>().And.BeOfType(primitive.InnerException!.GetType());
    }

    [Fact]
    public async Task A_value_object_over_either_128_bit_integer_is_refused_rather_than_stored_as_an_empty_document()
    {
        var signed = await FailedInsertAsync(new Holder<LedgerBalance> { Value = LedgerBalance.Create(5) });
        var unsigned = await FailedInsertAsync(new Holder<Fingerprint> { Value = Fingerprint.Create(5) });

        signed.Should().BeOfType<BsonSerializationException>()
            .Which.Message.Should().Contain("LedgerBalance is a value object over Int128, which MongoDB has no representation for.");
        unsigned.Should().BeOfType<BsonSerializationException>()
            .Which.Message.Should().Contain("Fingerprint is a value object over UInt128, which MongoDB has no representation for.");
    }

    [Fact]
    public async Task Queries_over_value_objects_and_their_Value_match_the_documents_the_same_queries_over_primitives_would()
    {
        var orders = Database.GetCollection<MongoOrder>("orders");
        var french = Iban.Create(French);
        await orders.InsertManyAsync(
            [
                new MongoOrder
                {
                    Iban = french,
                    Quantity = LineQuantity.Create(3),
                    Customer = Buyer,
                    Backup = Iban.Create(German),
                    Countries = [CountryCode.France, CountryCode.Belgium],
                    Totals = new() { [CountryCode.France] = Amount.Create(12.5m), [CountryCode.Belgium] = Amount.Create(3m) },
                    Status = DocumentStatus.Create("FINAL"),
                    FirstLine = new MongoLine { Quantity = LineQuantity.Create(2), Price = Amount.Create(9.99m) },
                    Lines = [new MongoLine { Quantity = LineQuantity.Create(1) }],
                    Code = OwnedCode<MongoOrder>.Create("ORD-1"),
                },
                new MongoOrder
                {
                    Iban = Iban.Create(German),
                    Quantity = LineQuantity.Create(7),
                    Customer = Buyer,
                    Countries = [CountryCode.Germany],
                    Totals = new() { [CountryCode.France] = Amount.Create(20m) },
                    Status = DocumentStatus.Final,
                    Lines = [new MongoLine { Quantity = LineQuantity.Create(4) }],
                    Code = OwnedCode<MongoOrder>.Create("ORD-2"),
                },
            ],
            cancellationToken: Cancellation);

        var stored = await Database.GetCollection<BsonDocument>("orders").Find(new BsonDocument("Code", "ORD-1")).SingleAsync(Cancellation);
        stored["Iban"].Should().Be(new BsonString(French));
        stored["Quantity"].Should().Be(new BsonInt32(3));
        stored["Customer"].Should().Be(new BsonBinaryData(Buyer.Value, GuidRepresentation.Standard));
        stored["Countries"].Should().Be(new BsonArray { "FR", "BE" });
        stored["Totals"].Should().Be(new BsonDocument { { "FR", new BsonDecimal128(12.5m) }, { "BE", new BsonDecimal128(3m) } });
        stored["FirstLine"].Should().Be(new BsonDocument { { "Quantity", 2 }, { "Price", new BsonDecimal128(9.99m) } });

        var queryable = orders.AsQueryable();
        (await Count(x => x.Iban == french)).Should().Be(1);
        (await Count(x => x.Iban.Value == French)).Should().Be(1);
        (await Count(x => x.Quantity > LineQuantity.Create(5))).Should().Be(1);
        (await Count(x => x.Quantity.Value > 2)).Should().Be(2);
        (await Count(x => x.Iban.Value.StartsWith("DE"))).Should().Be(1);
        (await Count(x => x.Iban.Value.Length > 22)).Should().Be(1);
        (await Count(x => x.Customer == Buyer)).Should().Be(2);
        (await Count(x => x.Backup == null)).Should().Be(1);
        (await Count(x => x.Backup!.Value.Value.StartsWith("DE"))).Should().Be(1);
        (await Count(x => x.Countries.Contains(CountryCode.Belgium))).Should().Be(1);
        (await Count(x => new List<Iban> { french }.Contains(x.Iban))).Should().Be(1);
        (await Count(x => x.Totals[CountryCode.France] > Amount.Create(10m))).Should().Be(2);
        (await Count(x => x.Totals.ContainsKey(CountryCode.Belgium))).Should().Be(1);
        (await Count(x => x.FirstLine!.Quantity.Value >= 2)).Should().Be(1);
        (await Count(x => x.Lines.Any(line => line.Quantity == LineQuantity.Create(4)))).Should().Be(1);
        (await Count(x => x.Code == OwnedCode<MongoOrder>.Create("ORD-1"))).Should().Be(1);

        // The server compares as it stores: a value object whose equality ignores case still matches the spelling stored.
        (await Count(x => x.Status == DocumentStatus.Final)).Should().Be(1);

        queryable.Sum(x => x.Quantity.Value).Should().Be(10);
        queryable.Max(x => x.Quantity).Should().Be(LineQuantity.Create(7));
        queryable.OrderBy(x => x.Quantity).Select(x => x.Iban.Value.ToLowerInvariant()).ToList()
            .Should().Equal(French.ToLowerInvariant(), German.ToLowerInvariant());
        queryable.GroupBy(x => x.Customer).Select(group => new { group.Key, Total = group.Sum(x => x.Quantity.Value) }).ToList()
            .Should().ContainSingle().Which.Should().Be(new { Key = Buyer, Total = 10 });

        (await orders.CountDocumentsAsync(Builders<MongoOrder>.Filter.Eq(x => x.Iban, french), cancellationToken: Cancellation)).Should().Be(1);
        (await orders.CountDocumentsAsync(Builders<MongoOrder>.Filter.Gte(x => x.Quantity, LineQuantity.Create(3)), cancellationToken: Cancellation)).Should().Be(2);
        (await orders.CountDocumentsAsync(Builders<MongoOrder>.Filter.AnyIn(x => x.Countries, [CountryCode.Germany]), cancellationToken: Cancellation)).Should().Be(1);

        var moved = await orders.UpdateOneAsync(
            x => x.Code == OwnedCode<MongoOrder>.Create("ORD-2"),
            Builders<MongoOrder>.Update.Set(x => x.Backup, french).Inc(x => x.Quantity.Value, (short)1),
            cancellationToken: Cancellation);
        moved.ModifiedCount.Should().Be(1);
        var updated = await orders.Find(x => x.Code == OwnedCode<MongoOrder>.Create("ORD-2")).SingleAsync(Cancellation);
        (updated.Quantity, updated.Backup).Should().Be((LineQuantity.Create(8), french));

        Task<long> Count(System.Linq.Expressions.Expression<Func<MongoOrder, bool>> filter)
            => orders.CountDocumentsAsync(filter, cancellationToken: Cancellation);
    }

    [Fact]
    public async Task A_document_another_writer_stored_is_read_through_the_rules()
    {
        var raw = Database.GetCollection<BsonDocument>("foreign");
        await raw.InsertManyAsync(
            [
                new BsonDocument { { "Name", "spaced" }, { "Iban", "fr76 3000 6000 0112 3456 7890 189" } },
                new BsonDocument { { "Name", "short" }, { "Iban", "FR76" } },
                new BsonDocument { { "Name", "missing" } },
            ],
            cancellationToken: Cancellation);
        var accounts = Database.GetCollection<MongoAccount>("foreign");

        (await accounts.Find(x => x.Name == "spaced").SingleAsync(Cancellation)).Iban.Should().Be(Iban.Create(French));

        var refusal = await FluentActions.Awaiting(() => accounts.Find(x => x.Name == "short").SingleAsync(Cancellation))
            .Should().ThrowAsync<FormatException>();
        refusal.Which.Message.Should().StartWith($"An error occurred while deserializing the Iban property of class {typeof(MongoAccount).FullName}: The value read is not a valid Iban: ")
            .And.NotContain("FR76");
        ValueObjectErrors.TryGetCode(refusal.Which, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.TooShort);

        // A field the document lacks is left to the class map, which leaves the member as it is: the default instance.
#pragma warning disable VO0010 // The uninitialized instance is what the driver leaves a missing field as.
        (await accounts.Find(x => x.Name == "missing").SingleAsync(Cancellation)).Iban.Should().Be(default(Iban));
#pragma warning restore VO0010

        // A query never reads the documents it counts: .Value finds the one the type refuses.
        (await accounts.CountDocumentsAsync(x => x.Iban.Value == "FR76", cancellationToken: Cancellation)).Should().Be(1);
    }

    [Fact]
    public async Task A_member_given_a_trusted_serializer_reads_what_another_writer_stored_as_it_is()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(TrustedAccount)))
        {
            BsonClassMap.TryRegisterClassMap<TrustedAccount>(map =>
            {
                map.AutoMap();
                map.MapMember(x => x.Iban).SetSerializer(new ValueObjectBsonSerializer<Iban, string>(new StringSerializer(), trusted: true));
            });
        }

        await Database.GetCollection<BsonDocument>("trusted").InsertOneAsync(new BsonDocument("Iban", "not an iban"), cancellationToken: Cancellation);

        var read = await Database.GetCollection<TrustedAccount>("trusted").Find(FilterDefinition<TrustedAccount>.Empty).SingleAsync(Cancellation);

        read.Iban.Value.Should().Be("not an iban");
    }

#pragma warning disable VO0010 // The uninitialized instance is what every write refuses.
    [Fact]
    public async Task A_default_instance_the_type_refuses_is_refused_by_every_write()
    {
        var pages = Database.GetCollection<MongoPage>("pages");
        await pages.InsertOneAsync(new MongoPage { Name = "first", Page = PageNumber.Create(1) }, cancellationToken: Cancellation);

        await RefusedAsync(() => pages.InsertOneAsync(new MongoPage { Name = "none" }, cancellationToken: Cancellation));
        await RefusedAsync(() => pages.InsertManyAsync([new MongoPage { Name = "none" }], cancellationToken: Cancellation));
        await RefusedAsync(() => pages.ReplaceOneAsync(x => x.Name == "first", new MongoPage { Name = "first" }, cancellationToken: Cancellation));
        await RefusedAsync(() => pages.UpdateOneAsync(x => x.Name == "first", Builders<MongoPage>.Update.Set(x => x.Page, default(PageNumber)), cancellationToken: Cancellation));

        (await pages.Find(FilterDefinition<MongoPage>.Empty).ToListAsync(Cancellation))
            .Should().ContainSingle().Which.Page.Should().Be(PageNumber.Create(1));

        static async Task RefusedAsync(Func<Task> write)
        {
            var refusal = await FluentActions.Awaiting(write).Should().ThrowAsync<BsonSerializationException>();
            ValueObjectErrors.TryGetCode(refusal.Which, out var code).Should().BeTrue();
            code.Should().Be(ValueObjectErrorCodes.OutOfRange);
        }
    }
#pragma warning restore VO0010

    private async Task StoredAsItsValueAsync<TSelf, TValue>(TSelf value)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var name = typeof(TSelf).Name.Split('`')[0];
        var id = ObjectId.GenerateNewId();
        var objects = Database.GetCollection<Holder<TSelf>>($"{name}.objects");
        var primitives = Database.GetCollection<Holder<TValue>>($"{name}.primitives");

        await objects.InsertOneAsync(new Holder<TSelf> { Id = id, Value = value }, cancellationToken: Cancellation);
        await primitives.InsertOneAsync(new Holder<TValue> { Id = id, Value = value.Value }, cancellationToken: Cancellation);

        var byId = new BsonDocument("_id", id);
        var stored = await Database.GetCollection<BsonDocument>($"{name}.objects").Find(byId).SingleAsync(Cancellation);
        var storedPrimitive = await Database.GetCollection<BsonDocument>($"{name}.primitives").Find(byId).SingleAsync(Cancellation);
        stored.ToJson().Should().Be(storedPrimitive.ToJson(), "{0} is stored as its {1} is", name, typeof(TValue).Name);

        var read = await objects.Find(x => x.Id == id).SingleAsync(Cancellation);
        var readPrimitive = await primitives.Find(x => x.Id == id).SingleAsync(Cancellation);
        read.Value.Value.Should().Be(readPrimitive.Value, "{0} reads back what its {1} reads", name, typeof(TValue).Name);

        (await objects.CountDocumentsAsync(Builders<Holder<TSelf>>.Filter.Eq(x => x.Value, value), cancellationToken: Cancellation))
            .Should().Be(1, "{0} is found by its value", name);
    }

    private async Task<Exception> FailedInsertAsync<T>(Holder<T> holder)
    {
        var collection = Database.GetCollection<Holder<T>>($"{typeof(T).Name}.failed");

        return (await FluentActions.Awaiting(() => collection.InsertOneAsync(holder, cancellationToken: Cancellation))
            .Should().ThrowAsync<Exception>()).Which;
    }
}

/// <summary>
/// One value, the value object or the primitive it replaces, as a document.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
public sealed class Holder<T>
{
    public ObjectId Id { get; set; }

    public T Value { get; set; } = default!;
}

/// <summary>
/// An order, holding value objects in every shape a query reaches.
/// </summary>
public sealed class MongoOrder
{
    public ObjectId Id { get; set; }

    public Iban Iban { get; set; }

    public LineQuantity Quantity { get; set; }

    public CustomerId Customer { get; set; }

    public Iban? Backup { get; set; }

    public List<CountryCode> Countries { get; set; } = [];

    public Dictionary<CountryCode, Amount> Totals { get; set; } = [];

    public DocumentStatus Status { get; set; }

    public MongoLine? FirstLine { get; set; }

    public List<MongoLine> Lines { get; set; } = [];

    public OwnedCode<MongoOrder> Code { get; set; }
}

/// <summary>
/// A line of an order, as a sub-document.
/// </summary>
public sealed class MongoLine
{
    public LineQuantity Quantity { get; set; }

    public Amount? Price { get; set; }
}

/// <summary>
/// An account as another writer may have stored it.
/// </summary>
public sealed class MongoAccount
{
    public ObjectId Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Iban Iban { get; set; }
}

/// <summary>
/// An account whose IBAN is read on trust, through the serializer its class map sets.
/// </summary>
public sealed class TrustedAccount
{
    public ObjectId Id { get; set; }

    public Iban Iban { get; set; }
}

/// <summary>
/// A page of a book, whose number starts at one.
/// </summary>
public sealed class MongoPage
{
    public ObjectId Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public PageNumber Page { get; set; }
}
