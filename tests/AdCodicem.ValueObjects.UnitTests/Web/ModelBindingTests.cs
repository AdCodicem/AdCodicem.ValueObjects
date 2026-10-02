using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// Value objects bound by MVC from the query string and the route, through an application served in memory, and
/// the codes of the rules they break carried into the automatic 400 response.
/// </summary>
public sealed class ModelBindingTests : IAsyncLifetime
{
    private WebApplication _application = null!;
    private HttpClient _client = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        _application = await StartAsync(static _ => { });
        _client = _application.GetTestClient();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _application.DisposeAsync();
    }

    [Theory]
    [InlineData("/probe/country?country=lu", "LU")]
    [InlineData("/probe/country", "none")]
    [InlineData("/probe/country?country=", "none")]
    public async Task An_optional_value_object_binds_from_the_query_string_or_to_null(string url, string expected)
        => (await _client.GetStringAsync(url, TestContext.Current.CancellationToken)).Should().Be(expected);

    [Fact]
    public async Task A_value_object_binds_from_a_route_segment()
    {
        var id = "0192f4a0-0000-7000-8000-000000000001";

        (await _client.GetStringAsync($"/probe/customers/{id}", TestContext.Current.CancellationToken)).Should().Be(id);
    }

    [Theory]
    [InlineData("/probe/country?country=ZZ", "country", ValueObjectErrorCodes.NotAKnownValue)]
    [InlineData("/probe/customers/not-a-guid", "id", ValueObjectErrorCodes.NotParsable)]
    [InlineData("/probe/customers/00000000-0000-0000-0000-000000000000", "id", ValueObjectErrorCodes.Required)]
    public async Task A_rejected_value_is_answered_with_the_code_of_the_rule_it_breaks(string url, string member, string code)
    {
        var problem = await GetProblemAsync(url);

        problem.GetProperty("errorCodes").GetProperty(member).GetString().Should().Be(code);
        problem.GetProperty("errors").GetProperty(member).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Every_rejected_member_keeps_its_own_code()
    {
        var problem = await GetProblemAsync("/probe/search?country=ZZ&email=nope");

        problem.GetProperty("errorCodes").Deserialize<Dictionary<string, string>>().Should().Equal(new Dictionary<string, string>
        {
            ["country"] = ValueObjectErrorCodes.NotAKnownValue,
            ["email"] = ValueObjectErrorCodes.InvalidFormat,
        });
    }

    /// <summary>
    /// A parser written by hand may refuse text without giving a reason, which a generated one never does. The
    /// binder supplies one, so the response still names a rule.
    /// </summary>
    [Fact]
    public async Task A_rejection_without_a_reason_is_reported_as_not_parsable()
    {
        var problem = await GetProblemAsync("/probe/counter?counter=abc");

        problem.GetProperty("errorCodes").GetProperty("counter").GetString().Should().Be(ValueObjectErrorCodes.NotParsable);
        problem.GetProperty("errors").GetProperty("counter")[0].GetString()
            .Should().Be("The value is not a valid HandWrittenCounter.");
        (await _client.GetStringAsync("/probe/counter?counter=5", TestContext.Current.CancellationToken)).Should().Be("5");
    }

    /// <summary>
    /// A value inside a JSON body is refused by the serializer, which records no code: the response is the
    /// framework's own, with no empty extension added to it.
    /// </summary>
    [Fact]
    public async Task A_rejection_no_binder_recorded_leaves_the_response_without_codes()
    {
        using var body = new StringContent("""{"reference":"no"}""", Encoding.UTF8, "application/json");

        using var response = await _client.PostAsync("/probe/orders", body, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errors").EnumerateObject().Should().NotBeEmpty();
        problem.TryGetProperty(ValueObjectProblemDetails.ExtensionName, out _).Should().BeFalse();
    }

    /// <summary>
    /// An application may answer an invalid model with a response of its own. The codes go into problem details,
    /// and a response that holds none is left as the application made it - a bare status, which MVC itself turns
    /// into problem details afterwards, or a body of another kind.
    /// </summary>
    [Fact]
    public async Task A_response_that_is_no_problem_details_is_left_as_the_application_made_it()
    {
        await using var plain = await StartAsync(static options => options.InvalidModelStateResponseFactory = static _ => new BadRequestResult());
        await using var text = await StartAsync(static options => options.InvalidModelStateResponseFactory = static _ => new BadRequestObjectResult("refused"));

        using var plainResponse = await plain.GetTestClient().GetAsync("/probe/country?country=ZZ", TestContext.Current.CancellationToken);
        using var textResponse = await text.GetTestClient().GetAsync("/probe/country?country=ZZ", TestContext.Current.CancellationToken);

        plainResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await plainResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.TryGetProperty(ValueObjectProblemDetails.ExtensionName, out _).Should().BeFalse();
        textResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await textResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("refused");
    }

    /// <summary>
    /// Starts an MVC application holding <see cref="ProbeController"/> alone, configured as the ASP.NET Core how-to
    /// says, after the application's own API behaviour.
    /// </summary>
    private static async Task<WebApplication> StartAsync(Action<ApiBehaviorOptions> configure)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers()
            .ConfigureApplicationPartManager(static manager =>
            {
                manager.ApplicationParts.Clear();
                manager.ApplicationParts.Add(new AssemblyPart(typeof(ProbeController).Assembly));
            })
            .AddValueObjects();
        builder.Services.Configure<ApiBehaviorOptions>(options =>
        {
            configure(options);
            options.AddValueObjectProblemDetails();
        });

        var application = builder.Build();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);

        return application;
    }

    private async Task<JsonElement> GetProblemAsync(string url)
    {
        using var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }
}
