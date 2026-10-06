using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson;
using AdCodicem.ValueObjects.NewtonsoftJson;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// Request bodies MVC reads with Newtonsoft.Json, through an application served in memory, and the codes of the rules
/// their value objects break carried into the automatic 400 response.
/// </summary>
public sealed class NewtonsoftJsonModelBindingTests : IAsyncLifetime
{
    private WebApplication _application = null!;
    private HttpClient _client = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        _application = await StartAsync(Wiring.Package);
        _client = _application.GetTestClient();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _application.DisposeAsync();
    }

    /// <summary>How an application wires value objects into MVC on Newtonsoft.Json.</summary>
    public enum Wiring
    {
        /// <summary><c>AddNewtonsoftJson().AddValueObjectsNewtonsoftJson()</c>, and nothing else.</summary>
        Package,

        /// <summary>
        /// What an application did before the package: the binder, and the converter added to the settings by hand.
        /// </summary>
        WithoutPackage,

        /// <summary>The binder alone, the settings left as Newtonsoft.Json has them.</summary>
        WithoutConverter,
    }

    /// <summary>
    /// A value a body refuses is answered with the code of the rule it breaks, under the key MVC gives its error.
    /// </summary>
    /// <param name="json">The body.</param>
    /// <param name="code">The code of the rule it breaks.</param>
    [Theory]
    [InlineData("""{"reference":"no"}""", ValueObjectErrorCodes.TooShort)]
    [InlineData("""{"reference":42}""", ValueObjectErrorCodes.NotParsable)]
    [InlineData("""{"reference":null}""", ValueObjectErrorCodes.Required)]
    public async Task A_value_a_body_refuses_is_answered_with_the_code_under_the_key_of_its_error(string json, string code)
    {
        var problem = await PostProblemAsync(_client, "/probe/orders", json);

        problem.GetProperty("errorCodes").GetProperty("reference").GetString().Should().Be(code);
        problem.GetProperty("errors").GetProperty("reference").GetArrayLength().Should().Be(1);
    }

    /// <summary>
    /// Newtonsoft.Json sets the properties of an object one by one and reads on past a refused one, so every refused
    /// member of the body is answered with its own code, a construction of a generic value object and one written by
    /// hand among them, while an optional one holding null is no refusal.
    /// </summary>
    [Fact]
    public async Task Every_refused_member_of_a_body_keeps_its_own_code()
    {
        var problem = await PostProblemAsync(
            _client,
            "/newtonsoft/parcels",
            """{"reference":"no","country":null,"quantity":1001,"email":"nope","lines":[{"reference":"x"}],"purchase":"PO-1042-AND-MORE","code":"a1"}""");

        var codes = problem.GetProperty("errorCodes").Deserialize<Dictionary<string, string>>();
        codes.Should().Equal(new Dictionary<string, string>
        {
            ["reference"] = ValueObjectErrorCodes.TooShort,
            ["quantity"] = ValueObjectErrorCodes.OutOfRange,
            ["email"] = ValueObjectErrorCodes.InvalidFormat,
            ["lines[0].reference"] = ValueObjectErrorCodes.TooShort,
            ["purchase"] = ValueObjectErrorCodes.TooLong,
            ["code"] = ValueObjectErrorCodes.InvalidFormat,
        });
        problem.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!.Keys.Should().Contain(codes!.Keys);
    }

    /// <summary>
    /// The errors of a body are the framework's, with the package or without it, whatever the application says of
    /// exception messages: the message of the exception where it keeps them, and "The input was not valid." where it
    /// does not. The codes are recorded either way, and the option keeps the value the application gave it.
    /// </summary>
    /// <param name="allowMessages">The application's <c>AllowInputFormatterExceptionMessages</c>.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_errors_of_a_body_are_the_framework_s_whatever_exception_messages_are_allowed(bool allowMessages)
    {
        await using var with = await StartAsync(Wiring.Package, json: options => options.AllowInputFormatterExceptionMessages = allowMessages);
        await using var without = await StartAsync(Wiring.WithoutPackage, json: options => options.AllowInputFormatterExceptionMessages = allowMessages);
        using var withClient = with.GetTestClient();
        using var withoutClient = without.GetTestClient();

        foreach (var json in new[] { """{"reference":"no"}""", """{"reference":42}""", """{"reference":""", """{"reference":"no","quantity":-1,"lines":[{"reference":"x"}]}""" })
        {
            var url = json.Contains("quantity", StringComparison.Ordinal) ? "/newtonsoft/parcels" : "/probe/orders";
            var expected = await PostProblemAsync(withoutClient, url, json);
            var actual = await PostProblemAsync(withClient, url, json);

            actual.GetProperty("errors").Deserialize<Dictionary<string, string[]>>().Should()
                .BeEquivalentTo(expected.GetProperty("errors").Deserialize<Dictionary<string, string[]>>(), "the errors of {0}", json);
            expected.TryGetProperty(ValueObjectProblemDetails.ExtensionName, out _).Should().BeFalse("without the package a body records no code");
        }

        var refused = await PostProblemAsync(withClient, "/probe/orders", """{"reference":"no"}""");
        refused.GetProperty("errorCodes").GetProperty("reference").GetString().Should().Be(ValueObjectErrorCodes.TooShort);
        refused.GetProperty("errors").GetProperty("reference")[0].GetString().Should().Be(allowMessages
            ? "The value is not a valid OrderReference: The value must be at least 3 characters long."
            : "The input was not valid.");
        with.Services.GetRequiredService<IOptions<MvcNewtonsoftJsonOptions>>().Value.AllowInputFormatterExceptionMessages.Should().Be(allowMessages);
    }

    /// <summary>
    /// The formatter reads the application's options themselves, not a copy of them: exception messages turned off once
    /// the application runs are turned off for a body, and the code is recorded all the same.
    /// </summary>
    [Fact]
    public async Task Exception_messages_turned_off_once_the_application_runs_are_turned_off_for_a_body()
    {
        await using var application = await StartAsync(Wiring.Package);
        using var client = application.GetTestClient();
        var before = await PostProblemAsync(client, "/probe/orders", """{"reference":"no"}""");

        application.Services.GetRequiredService<IOptions<MvcNewtonsoftJsonOptions>>().Value.AllowInputFormatterExceptionMessages = false;
        var after = await PostProblemAsync(client, "/probe/orders", """{"reference":"no"}""");

        before.GetProperty("errors").GetProperty("reference")[0].GetString().Should()
            .Be("The value is not a valid OrderReference: The value must be at least 3 characters long.");
        after.GetProperty("errors").GetProperty("reference")[0].GetString().Should().Be("The input was not valid.");
        after.GetProperty("errorCodes").GetProperty("reference").GetString().Should().Be(ValueObjectErrorCodes.TooShort);
    }

    /// <summary>
    /// A request whose query string and body are both refused answers with both codes, each under its own key: the
    /// call bound value objects from the query string too.
    /// </summary>
    [Fact]
    public async Task A_refused_query_value_and_a_refused_body_keep_their_own_codes()
    {
        var problem = await PostProblemAsync(_client, "/probe/orders/by-country?country=ZZ", """{"reference":"no"}""");

        problem.GetProperty("errorCodes").Deserialize<Dictionary<string, string>>().Should().Equal(new Dictionary<string, string>
        {
            ["country"] = ValueObjectErrorCodes.NotAKnownValue,
            ["reference"] = ValueObjectErrorCodes.TooShort,
        });
    }

    /// <summary>
    /// The settings the package gives MVC read what Newtonsoft.Json's defaults lose, an offset and the digits of a
    /// decimal past a double's, with nothing but the two calls.
    /// </summary>
    [Fact]
    public async Task A_body_reads_under_the_settings_the_converter_reads_best_under()
    {
        using var body = new StringContent("""{"at":"2024-06-01T12:30:45+02:00","total":1234567890123456789.12}""", Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync("/newtonsoft/readings", body, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("2024-06-01T12:30:45.0000000+02:00|1234567890123456789.12");
    }

    /// <summary>
    /// The settings the package gives MVC are the ones it writes responses with, and they reach every member of a body:
    /// a number and a boolean are answered as such where the type converter wrote them as strings, a number sent as a
    /// string is still read, a boolean sent as a string is refused, and so is a real a member of no particular type
    /// reads as a decimal beyond its range.
    /// </summary>
    [Fact]
    public async Task The_settings_the_package_gives_MVC_write_responses_and_read_every_member()
    {
        await using var withoutConverter = await StartAsync(Wiring.WithoutConverter);
        using var former = withoutConverter.GetTestClient();

        (await PostAsync(_client, """{"quantity":"7","consent":true}""")).Should().Be("""{"quantity":7,"consent":true,"extra":null}""");
        (await PostAsync(former, """{"quantity":"7","consent":"True"}""")).Should().Be("""{"quantity":"7","consent":"True","extra":null}""");
        var boolean = await PostProblemAsync(_client, "/newtonsoft/answers", """{"quantity":7,"consent":"True"}""");
        boolean.GetProperty("errorCodes").GetProperty("consent").GetString().Should().Be(ValueObjectErrorCodes.NotParsable);
        (await PostAsync(former, """{"quantity":"7","consent":"True","extra":1e30}""")).Should().Contain("\"extra\":1E+30");
        var real = await PostProblemAsync(_client, "/newtonsoft/answers", """{"quantity":7,"consent":true,"extra":1e30}""");
        real.GetProperty("errors").GetProperty("extra").GetArrayLength().Should().Be(1);

        static async Task<string> PostAsync(HttpClient client, string json)
        {
            using var body = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("/newtonsoft/answers", body, TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Without the converter, which the package adds, Newtonsoft.Json reads a value object through its type converter:
    /// the refusal names no rule, and its message quotes the refused text.
    /// </summary>
    [Fact]
    public async Task Without_the_converter_a_refusal_names_no_rule_and_quotes_the_text()
    {
        await using var application = await StartAsync(Wiring.WithoutConverter);
        using var client = application.GetTestClient();

        var problem = await PostProblemAsync(client, "/probe/orders", """{"reference":"no"}""");

        problem.TryGetProperty(ValueObjectProblemDetails.ExtensionName, out _).Should().BeFalse();
        problem.GetProperty("errors").GetProperty("reference")[0].GetString().Should().Contain("\"no\"");
    }

    /// <summary>
    /// An exception no JSON explains, thrown by a converter of the application, propagates out of MVC as it does
    /// without the package, rather than being answered with a 400.
    /// </summary>
    /// <param name="wiring">With the package, or without it.</param>
    [Theory]
    [InlineData(Wiring.Package)]
    [InlineData(Wiring.WithoutPackage)]
    public async Task An_exception_no_JSON_explains_propagates_as_without_the_package(Wiring wiring)
    {
        await using var application = await StartAsync(wiring);
        using var client = application.GetTestClient();
        using var body = new StringContent("""{"name":"anything"}""", Encoding.UTF8, "application/json");

        await FluentActions.Awaiting(() => client.PostAsync("/newtonsoft/faulty", body, TestContext.Current.CancellationToken))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("The converter is broken.");
    }

    /// <summary>
    /// An application allowing fewer errors than a body refuses values gets one code per error it keeps, and none
    /// beyond.
    /// </summary>
    [Fact]
    public async Task A_body_refusing_more_values_than_errors_are_allowed_gets_a_code_per_error_kept()
    {
        await using var application = await StartAsync(Wiring.Package, mvc: static options => options.MaxModelValidationErrors = 3);
        using var client = application.GetTestClient();

        var problem = await PostProblemAsync(
            client,
            "/newtonsoft/parcels",
            """{"lines":[{"reference":"a"},{"reference":"b"},{"reference":"c"},{"reference":"d"}]}""");

        var codes = problem.GetProperty("errorCodes").Deserialize<Dictionary<string, string>>()!;
        codes.Keys.Should().Equal("lines[0].reference", "lines[1].reference");
        problem.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!.Keys.Should().Contain(codes.Keys);
    }

    /// <summary>
    /// Starts an MVC application holding the controllers of this assembly, reading with Newtonsoft.Json, wired as
    /// <paramref name="wiring"/> says.
    /// </summary>
    private static async Task<WebApplication> StartAsync(
        Wiring wiring,
        Action<MvcNewtonsoftJsonOptions>? json = null,
        Action<MvcOptions>? mvc = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        var controllers = builder.Services.AddControllers(options => mvc?.Invoke(options))
            .ConfigureApplicationPartManager(static manager =>
            {
                manager.ApplicationParts.Clear();
                manager.ApplicationParts.Add(new AssemblyPart(typeof(NewtonsoftProbeController).Assembly));
            });

        switch (wiring)
        {
            case Wiring.Package:
                controllers.AddNewtonsoftJson(options => json?.Invoke(options)).AddValueObjectsNewtonsoftJson();
                break;
            case Wiring.WithoutPackage:
                controllers
                    .AddNewtonsoftJson(options =>
                    {
                        options.SerializerSettings.AddValueObjects();
                        json?.Invoke(options);
                    })
                    .AddValueObjects();
                break;
            default:
                controllers.AddNewtonsoftJson().AddValueObjects();
                break;
        }

        builder.Services.Configure<ApiBehaviorOptions>(static options => options.AddValueObjectProblemDetails());

        var application = builder.Build();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);

        return application;
    }

    private static async Task<JsonElement> PostProblemAsync(HttpClient client, string url, string json)
    {
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(url, body, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }
}
