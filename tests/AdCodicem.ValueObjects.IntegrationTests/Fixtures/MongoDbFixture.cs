using AdCodicem.ValueObjects.Identifiers.MongoDB;
using AdCodicem.ValueObjects.MongoDB;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace AdCodicem.ValueObjects.IntegrationTests.Fixtures;

/// <summary>
/// A real MongoDB server, started in a container, which value objects are stored in and queried through MongoDB.Driver.
/// </summary>
/// <remarks>
/// The driver keeps its serializers for the whole process, so the registration runs once, before any test touches the
/// driver: the <see cref="Guid"/> representation first, then the provider, strict, for the sample's value objects and
/// this suite's, then the id generators of their entity identifiers.
/// </remarks>
public sealed class MongoDbFixture : IAsyncLifetime
{
    private static readonly Lazy<bool> Registration = new(static () =>
    {
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        ValueObjectBson.Register(typeof(Iban).Assembly, typeof(MongoDbFixture).Assembly);
        EntityIdBson.Register(typeof(Iban).Assembly, typeof(MongoDbFixture).Assembly);

        return true;
    });

    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:8.0").Build();

    private MongoClient? _client;

    /// <summary>Gets a database of its own for each name, on the running server.</summary>
    /// <param name="name">The name of the database, one per test class.</param>
    /// <returns>The database.</returns>
    public IMongoDatabase Database(string name)
        => (_client ?? throw new InvalidOperationException("The server is not started.")).GetDatabase(name);

    /// <summary>Registers the serializers, then starts the server.</summary>
    /// <returns>A task that completes once the server accepts connections.</returns>
    public async ValueTask InitializeAsync()
    {
        _ = Registration.Value;
        await _container.StartAsync();
        _client = new MongoClient(_container.GetConnectionString());
    }

    /// <summary>Stops the server.</summary>
    /// <returns>A task that completes once the container is gone.</returns>
    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _container.DisposeAsync();
    }
}
