using AdCodicem.ValueObjects.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace AdCodicem.ValueObjects.RdgTests;

/// <summary>
/// An application mapping <see cref="ProblemEndpoints"/>, whose binding the Request Delegate Generator writes, with
/// <c>RouteHandlerOptions.ThrowOnBadRequest</c> as one of the subclasses sets it.
/// </summary>
public abstract class ProblemApplication(bool throwOnBadRequest) : IAsyncLifetime
{
    private WebApplication _application = null!;

    /// <summary>Gets a client of the in-memory server.</summary>
    public HttpClient Client { get; private set; } = null!;

    /// <summary>Gets the in-memory server, which takes a request built on its own context, an empty header included.</summary>
    public TestServer Server => _application.GetTestServer();

    /// <summary>Gets the endpoints the application maps.</summary>
    public IReadOnlyList<Endpoint> Endpoints => _application.Services.GetRequiredService<EndpointDataSource>().Endpoints;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = throwOnBadRequest);
        builder.Services.AddProblemDetails(static options =>
            options.CustomizeProblemDetails = static context => context.ProblemDetails.Extensions.Remove("traceId"));
        builder.Services.AddValueObjectHttpProblemDetails();

        _application = builder.Build();
        _application.UseExceptionHandler();
        ProblemEndpoints.Map(_application);
        await _application.StartAsync(TestContext.Current.CancellationToken);
        Client = _application.GetTestClient();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _application.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

/// <summary>The application in Production, where the binder answers a refusal itself.</summary>
public sealed class ProductionProblemApplication() : ProblemApplication(throwOnBadRequest: false);

/// <summary>The application in Development, where the binder throws for a refusal.</summary>
public sealed class DevelopmentProblemApplication() : ProblemApplication(throwOnBadRequest: true);
