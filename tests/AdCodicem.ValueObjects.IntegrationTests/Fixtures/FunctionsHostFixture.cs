using System.Reflection;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.Azurite;

namespace AdCodicem.ValueObjects.IntegrationTests.Fixtures;

/// <summary>
/// The Azure Functions host, in Microsoft's <c>dotnet-isolated</c> image, running the integration suite's Functions
/// application (<c>tests/Fixtures/AdCodicem.ValueObjects.Fixtures.Functions</c>) on the isolated worker, with Azurite
/// for its queue triggers.
/// </summary>
/// <remarks>
/// <para>
/// The worker's protocol with the host is internal to it, so no host runs in process: the build's output is copied into
/// each container, which starts the worker as Azure does. Three hosts run the same application, configured apart by
/// <c>FIXTURE_ORDER</c>: <see cref="After"/>, on the current template's builder, the problem details registered after
/// the ASP.NET Core integration and the request localization middleware in its pipeline; <see cref="HostBuilder"/>, on
/// the <c>HostBuilder</c> style, the culture chosen by the request localization options alone; and
/// <see cref="Before"/>, the problem details registered before the integration. Only the first runs the queue triggers,
/// which the two others disable, so that each message is read once.
/// </para>
/// <para>
/// HTTP triggers need no storage once the host keeps its keys in files; the queue triggers' storage is Azurite, reached
/// through the network the containers share. The image is a moving tag, as <c>mongo:7.0</c> is, and exists for amd64
/// alone.
/// </para>
/// </remarks>
public sealed class FunctionsHostFixture : IAsyncLifetime
{
    /// <summary>The Functions host for .NET 10 isolated workers.</summary>
    public const string HostImage = "mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0";

    /// <summary>The queues the application reads and writes.</summary>
    public static readonly string[] Queues = ["orders", "rates", "codes", "rawcodes", "results"];

    /// <summary>Azurite's well-known development account, as the containers reach it on their network.</summary>
    private const string Storage =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;"
        + "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;"
        + "BlobEndpoint=http://azurite:10000/devstoreaccount1;QueueEndpoint=http://azurite:10001/devstoreaccount1;"
        + "TableEndpoint=http://azurite:10002/devstoreaccount1;";

    private readonly INetwork _network = new NetworkBuilder().Build();

    private readonly AzuriteContainer _azurite;

    private readonly IContainer _after;

    private readonly IContainer _hostBuilder;

    private readonly IContainer _before;

    /// <summary>Creates the containers, which start in <see cref="InitializeAsync"/>.</summary>
    public FunctionsHostFixture()
    {
        _azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.37.0")
            .WithNetwork(_network)
            .WithNetworkAliases("azurite")
            .WithCommand("--skipApiVersionCheck", "--loose")
            .Build();
        _after = Host("after", queues: true);
        _hostBuilder = Host("hostbuilder", queues: false);
        _before = Host("before", queues: false);
    }

    /// <summary>Gets the client of the host on the current template's builder, the problem details registered after the integration.</summary>
    public HttpClient After { get; private set; } = null!;

    /// <summary>Gets the client of the host on the <c>HostBuilder</c> style.</summary>
    public HttpClient HostBuilder { get; private set; } = null!;

    /// <summary>Gets the client of the host whose problem details are registered before the integration.</summary>
    public HttpClient Before { get; private set; } = null!;

    /// <summary>Gets a queue of Azurite, encoded as the host reads it.</summary>
    /// <param name="name">The queue's name.</param>
    /// <returns>Its client.</returns>
    public QueueClient Queue(string name)
        => new(_azurite.GetConnectionString(), name, new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });

    /// <summary>Gets what the host running the queue triggers has logged so far.</summary>
    /// <returns>Its standard output and error.</returns>
    public async Task<string> AfterLogsAsync()
    {
        var (stdout, stderr) = await _after.GetLogsAsync(ct: TestContext.Current.CancellationToken);

        return stdout + stderr;
    }

    /// <summary>Gets what the host whose problem details are registered before the integration has logged so far.</summary>
    /// <returns>Its standard output and error.</returns>
    public async Task<string> BeforeLogsAsync()
    {
        var (stdout, stderr) = await _before.GetLogsAsync(ct: TestContext.Current.CancellationToken);

        return stdout + stderr;
    }

    /// <summary>Waits until a queue holds at least as many messages as asked, and takes them.</summary>
    /// <param name="name">The queue's name.</param>
    /// <param name="count">How many messages to wait for.</param>
    /// <returns>The messages' text, in no particular order.</returns>
    public async Task<IReadOnlyList<string>> TakeAsync(string name, int count)
    {
        var queue = Queue(name);
        var taken = new List<string>();
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (taken.Count < count && DateTime.UtcNow < deadline)
        {
            if (await queue.ExistsAsync(TestContext.Current.CancellationToken))
            {
                QueueMessage[] messages = await queue.ReceiveMessagesAsync(32, cancellationToken: TestContext.Current.CancellationToken);
                foreach (var message in messages)
                {
                    taken.Add(message.Body.ToString());
                    await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, TestContext.Current.CancellationToken);
                }
            }

            if (taken.Count < count)
            {
                await Task.Delay(500, TestContext.Current.CancellationToken);
            }
        }

        return taken;
    }

    /// <summary>Starts Azurite, creates the queues, then starts the three hosts.</summary>
    /// <returns>A task that completes once every host answers.</returns>
    public async ValueTask InitializeAsync()
    {
        await _network.CreateAsync();
        await _azurite.StartAsync();
        foreach (var name in Queues)
        {
            await Queue(name).CreateIfNotExistsAsync();
        }

        await Task.WhenAll(_after.StartAsync(), _hostBuilder.StartAsync(), _before.StartAsync());
        After = Client(_after);
        HostBuilder = Client(_hostBuilder);
        Before = Client(_before);
    }

    /// <summary>Stops the containers and removes their network.</summary>
    /// <returns>A task that completes once they are gone.</returns>
    public async ValueTask DisposeAsync()
    {
        After?.Dispose();
        HostBuilder?.Dispose();
        Before?.Dispose();
        await Task.WhenAll(_after.DisposeAsync().AsTask(), _hostBuilder.DisposeAsync().AsTask(), _before.DisposeAsync().AsTask());
        await _azurite.DisposeAsync();
        await _network.DisposeAsync();
    }

    /// <summary>Finds the Functions application's build, which the integration suite's build writes beside its own.</summary>
    private static string Application()
    {
        var path = typeof(FunctionsHostFixture).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(static metadata => metadata.Key == "FunctionsApplication").Value!;

        return File.Exists(Path.Combine(path, "functions.metadata"))
            ? path
            : throw new InvalidOperationException(
                $"The Functions application is not built at '{path}': build tests/Fixtures/AdCodicem.ValueObjects.Fixtures.Functions, "
                + "which building this project does.");
    }

    /// <summary>Creates a host running the application in one configuration, with its queue triggers or without.</summary>
    private IContainer Host(string order, bool queues)
    {
        var builder = new ContainerBuilder(HostImage)
            .WithNetwork(_network)
            .WithPortBinding(80, assignRandomHostPort: true)
            .WithResourceMapping(new DirectoryInfo(Application()), "/home/site/wwwroot")
            .WithEnvironment("FIXTURE_ORDER", order)
            .WithEnvironment("AzureWebJobsStorage", Storage)
            .WithEnvironment("AzureWebJobsSecretStorageType", "files")
            .WithEnvironment("AzureFunctionsJobHost__Logging__Console__IsEnabled", "true")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
                static request => request.ForPort(80).ForPath("/api/ready"),
                static wait => wait.WithTimeout(TimeSpan.FromMinutes(2))));
        if (!queues)
        {
            builder = builder
                .WithEnvironment("AzureWebJobs.QueuePoco.Disabled", "true")
                .WithEnvironment("AzureWebJobs.QueueRate.Disabled", "true")
                .WithEnvironment("AzureWebJobs.QueueJson.Disabled", "true")
                .WithEnvironment("AzureWebJobs.QueueRaw.Disabled", "true");
        }

        return builder.Build();
    }

    private static HttpClient Client(IContainer container)
        => new() { BaseAddress = new Uri($"http://{container.Hostname}:{container.GetMappedPublicPort(80)}/api/") };
}

/// <summary>The test classes that share the Functions hosts, which start once for them.</summary>
[CollectionDefinition(nameof(FunctionsHosts))]
public sealed class FunctionsHosts : ICollectionFixture<FunctionsHostFixture>;
