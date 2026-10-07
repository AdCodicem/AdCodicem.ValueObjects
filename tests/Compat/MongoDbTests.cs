using AdCodicem.ValueObjects.Identifiers.MongoDB;
using AdCodicem.ValueObjects.MongoDB;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>A purchase as a MongoDB document.</summary>
public sealed class Purchase
{
    /// <summary>Gets or sets the document's identifier.</summary>
    public ObjectId Id { get; set; }

    /// <summary>Gets or sets the account paid from.</summary>
    public Iban Account { get; set; }

    /// <summary>Gets or sets the customer.</summary>
    public CustomerId Customer { get; set; }

    /// <summary>Gets or sets the quantity bought.</summary>
    public Quantity Quantity { get; set; }

    /// <summary>Gets or sets the account to fall back on.</summary>
    public Iban? Backup { get; set; }

    /// <summary>Gets or sets the purchase order.</summary>
    public Reference<PurchaseOrder> Order { get; set; }
}

/// <summary>A MongoDB server in a container, and the serializers and id generators registered once for the process.</summary>
public sealed class MongoDbFixture : IAsyncLifetime
{
    private static readonly Lazy<bool> Registration = new(static () =>
    {
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        ValueObjectBson.Register(typeof(Iban).Assembly);
        EntityIdBson.Register(typeof(Iban).Assembly);

        return true;
    });

    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:8.0").Build();

    /// <summary>Gets the client connected to the running server.</summary>
    public MongoClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _ = Registration.Value;
        await _container.StartAsync();
        Client = new MongoClient(_container.GetConnectionString());
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _container.DisposeAsync();
    }
}

/// <summary>
/// MongoDB.Driver, at the floor the package declares, on the next major: value objects stored as their bare values,
/// queried through <c>.Value</c>, and read back through their rules; a collection validator built from the rules; and an
/// entity identifier minted for a document inserted without one.
/// </summary>
[Trait("Requires", "Docker")]
public sealed class MongoDbTests(MongoDbFixture fixture) : IClassFixture<MongoDbFixture>
{
    [Fact]
    public async Task A_value_object_is_stored_as_its_value_queried_through_it_and_read_back_through_its_rules()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var database = fixture.Client.GetDatabase("compat");
        var purchases = database.GetCollection<Purchase>("purchases");
        var customer = CustomerId.Create(Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff"));

        await purchases.InsertOneAsync(
            new Purchase
            {
                Account = Iban.Create("FR7630006000011234567890189"),
                Customer = customer,
                Quantity = Quantity.Create(3),
                Order = Reference<PurchaseOrder>.Create("po-1"),
            },
            cancellationToken: cancellation);

        var stored = await database.GetCollection<BsonDocument>("purchases").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync(cancellation);
        stored["Account"].Should().Be(new BsonString("FR7630006000011234567890189"));
        stored["Customer"].Should().Be(new BsonBinaryData(customer.Value, GuidRepresentation.Standard));
        stored["Quantity"].Should().Be(new BsonInt32(3));
        stored["Backup"].Should().Be(BsonNull.Value);
        stored["Order"].Should().Be(new BsonString("PO-1"));

        (await purchases.CountDocumentsAsync(x => x.Account.Value.StartsWith("FR"), cancellationToken: cancellation)).Should().Be(1);
        (await purchases.CountDocumentsAsync(x => x.Quantity > Quantity.Create(2) && x.Customer == customer, cancellationToken: cancellation)).Should().Be(1);
        (await purchases.Find(x => x.Order == Reference<PurchaseOrder>.Create("PO-1")).SingleAsync(cancellation)).Quantity
            .Should().Be(Quantity.Create(3));

        await database.GetCollection<BsonDocument>("foreign").InsertOneAsync(new BsonDocument("Quantity", 500), cancellationToken: cancellation);
        var refusal = await FluentActions.Awaiting(() => database.GetCollection<Purchase>("foreign").Find(FilterDefinition<Purchase>.Empty).SingleAsync(cancellation))
            .Should().ThrowAsync<FormatException>();
        ValueObjectErrors.TryGetCode(refusal.Which, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public async Task A_validator_built_from_the_rules_refuses_what_the_type_refuses()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var database = fixture.Client.GetDatabase("compat");
        await database.CreateCollectionAsync(
            "validated",
            new CreateCollectionOptions<BsonDocument> { Validator = new BsonDocumentFilterDefinition<BsonDocument>(ValueObjectBsonSchema.For<Purchase>()) },
            cancellation);

        await database.GetCollection<Purchase>("validated").InsertOneAsync(
            new Purchase
            {
                Account = Iban.Create("FR7630006000011234567890189"),
                Customer = CustomerId.Create(Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff")),
                Quantity = Quantity.Create(3),
                Order = Reference<PurchaseOrder>.Create("po-2"),
            },
            cancellationToken: cancellation);

        var stored = await database.GetCollection<BsonDocument>("validated").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync(cancellation);
        stored.Remove("_id");
        stored["Quantity"] = 500;
        var refusal = await FluentActions.Awaiting(() => database.GetCollection<BsonDocument>("validated").InsertOneAsync(stored, cancellationToken: cancellation))
            .Should().ThrowAsync<MongoWriteException>();
        refusal.Which.WriteError.Code.Should().Be(121);
    }

    [Fact]
    public async Task A_document_inserted_without_its_entity_identifier_is_given_one()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var payments = fixture.Client.GetDatabase("compat").GetCollection<Payment>("payments");
        var payment = new Payment { Account = Iban.Create("FR7630006000011234567890189"), Amount = Amount.Create(12.5m) };

        await payments.InsertOneAsync(payment, cancellationToken: cancellation);

        payment.Id.Value.Should().StartWith("pay_");
        (await payments.Find(x => x.Id == payment.Id).SingleAsync(cancellation)).Amount.Should().Be(Amount.Create(12.5m));
    }
}
