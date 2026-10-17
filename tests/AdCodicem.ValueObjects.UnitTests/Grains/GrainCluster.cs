using AdCodicem.ValueObjects.Fixtures.Grains;
using AdCodicem.ValueObjects.Orleans;
using Microsoft.Extensions.Logging;
using Orleans.Serialization;
using Orleans.TestingHost;

namespace AdCodicem.ValueObjects.UnitTests.Grains;

/// <summary>
/// An Orleans cluster in process, a silo and its client talking over localhost, each serializing through its own
/// serializer, as two processes do; both call <c>AddValueObjects()</c> unless a test builds a cluster of its own.
/// </summary>
/// <remarks>The first deployment of a process takes about a second and a half, the next ones a third of a second.</remarks>
public sealed class GrainCluster : IAsyncLifetime
{
    private InProcessTestCluster? _cluster;

    /// <summary>Gets the client of the cluster.</summary>
    public IClusterClient Client => _cluster!.Client;

    /// <summary>
    /// Gets a grain of its own.
    /// </summary>
    /// <returns>The grain.</returns>
    public IValueObjectGrain Grain() => Client.GetGrain<IValueObjectGrain>(Guid.NewGuid());

    /// <summary>
    /// Starts a cluster of its own.
    /// </summary>
    /// <param name="silos">The number of silos.</param>
    /// <param name="silo">What each silo adds to its serializer.</param>
    /// <param name="client">What the client adds to its serializer.</param>
    /// <returns>The started cluster, to be disposed of.</returns>
    public static async Task<InProcessTestCluster> StartAsync(int silos, Action<ISerializerBuilder> silo, Action<ISerializerBuilder> client)
    {
        var builder = new InProcessTestClusterBuilder((short)silos);
        builder.ConfigureSilo((_, siloBuilder) => siloBuilder.Services.AddSerializer(silo));
        builder.ConfigureClient(clientBuilder => clientBuilder.Services.AddSerializer(client));
        builder.ConfigureHost(static host => host.Logging.SetMinimumLevel(LogLevel.Critical));
        var cluster = builder.Build();
        try
        {
            await cluster.DeployAsync();
            return cluster;
        }
        catch
        {
            await cluster.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
        => _cluster = await StartAsync(1, static silo => silo.AddValueObjects(), static client => client.AddValueObjects());

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_cluster is not null)
        {
            await _cluster.DisposeAsync();
        }
    }
}
