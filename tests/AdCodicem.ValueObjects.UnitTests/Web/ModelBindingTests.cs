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
    [InlineData("/probe/country?country=%20", "none")]
    [InlineData("/probe/country?country=%20%09", "none")]
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
    [InlineData("/probe/customers/%20", "id", ValueObjectErrorCodes.Required)]
    [InlineData("/probe/counter?counter=", "counter", ValueObjectErrorCodes.Required)]
    [InlineData("/probe/counter?counter=%20", "counter", ValueObjectErrorCodes.Required)]
    public async Task A_rejected_value_is_answered_with_the_code_of_the_rule_it_breaks(string url, string member, string code)
    {
        var problem = await GetProblemAsync(url);

        problem.GetProperty("errorCodes").GetProperty(member).GetString().Should().Be(code);
        problem.GetProperty("errors").GetProperty(member).GetArrayLength().Should().Be(1);
    }

    /// <summary>
    /// Blank text binds an optional value object to null, but a value object that cannot be null would bind to its
    /// default instance, which no rule has checked: it is refused as MVC refuses blank text for an int.
    /// </summary>
    [Fact]
    public async Task Blank_text_for_a_value_object_that_cannot_be_null_is_refused_as_MVC_refuses_it()
    {
        var problem = await GetProblemAsync("/probe/customers/%20");

        problem.GetProperty("errors").GetProperty("id")[0].GetString().Should().Be("The value ' ' is invalid.");
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
    /// An application that does not register the package's binder still binds a value object, through the type
    /// converter it carries, as MVC binds any simple type. MVC turns a <see cref="FormatException"/> into the message
    /// of its binding message provider, quoting the text as it does for an <c>int</c>, and records any other exception
    /// with no message, which problem details render as "The input was not valid.". No rule code is recorded, which
    /// only the binder does.
    /// </summary>
    [Fact]
    public async Task Without_the_binder_a_rejection_through_the_type_converter_is_reported_as_MVC_reports_bad_input()
    {
        await using var application = await StartAsync(static _ => { }, withValueObjectBinder: false);
        using var client = application.GetTestClient();

        using var response = await client.GetAsync("/probe/country?country=ZZ", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errors").GetProperty("country")[0].GetString().Should().Be("The value 'ZZ' is not valid.");
        problem.TryGetProperty(ValueObjectProblemDetails.ExtensionName, out _).Should().BeFalse();
        (await client.GetStringAsync("/probe/country?country=lu", TestContext.Current.CancellationToken)).Should().Be("LU");
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
    /// says, after the application's own API behaviour, or without the value object binder.
    /// </summary>
    private static async Task<WebApplication> StartAsync(Action<ApiBehaviorOptions> configure, bool withValueObjectBinder = true)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        var mvc = builder.Services.AddControllers()
            .ConfigureApplicationPartManager(static manager =>
            {
                manager.ApplicationParts.Clear();
                manager.ApplicationParts.Add(new AssemblyPart(typeof(ProbeController).Assembly));
            });
        if (withValueObjectBinder)
        {
            mvc.AddValueObjects();
        }

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
