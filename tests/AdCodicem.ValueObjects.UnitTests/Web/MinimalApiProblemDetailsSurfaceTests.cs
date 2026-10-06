using AdCodicem.ValueObjects.AspNetCore.Http;
using AdCodicem.ValueObjects.UnitTests.GeneratedSurface;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// Every value object of the domain, one per underlying type and per hook, refused by a minimal API and answered with
/// the rule it broke: the check of each is closed over its type through the descriptor's visitor.
/// </summary>
public sealed class MinimalApiProblemDetailsSurfaceTests(MinimalApiProblemDetailsSurfaceTests.SamplesApplication application)
    : IClassFixture<MinimalApiProblemDetailsSurfaceTests.SamplesApplication>
{
    public static TheoryData<string> Every => Samples.Names;

    [Theory]
    [MemberData(nameof(Every))]
    public Task Every_value_object_a_minimal_API_refuses_is_answered_with_the_rule_it_broke(string type)
        => Samples.All[type].AnswersARefusedMinimalApiValueWithItsRuleAsync(application.Client);

    /// <summary>An application mapping one endpoint per sample, under <c>/samples</c>.</summary>
    public sealed class SamplesApplication : IAsyncLifetime
    {
        private WebApplication _application = null!;

        /// <summary>Gets a client of the in-memory server.</summary>
        public HttpClient Client { get; private set; } = null!;

        /// <inheritdoc />
        public async ValueTask InitializeAsync()
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();

            _application = builder.Build();
            var samples = _application.MapGroup("/samples").WithValueObjectProblemDetails();
            foreach (var sample in Samples.All.Values)
            {
                sample.MapMinimalApiEndpoint(samples);
            }

            await _application.StartAsync(TestContext.Current.CancellationToken);
            Client = _application.GetTestClient();
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _application.DisposeAsync();
        }
    }
}
