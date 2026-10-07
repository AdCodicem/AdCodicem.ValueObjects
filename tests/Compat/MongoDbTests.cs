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

/// <summary>A MongoDB server in a container, and the serializers registered once for the process.</summary>
public sealed class MongoDbFixture : IAsyncLifetime
{
    private static readonly Lazy<bool> Registration = new(static () =>
    {
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        ValueObjectBson.Register(typeof(Iban).Assembly);

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
/// queried through <c>.Value</c>, and read back through their rules.
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
}
