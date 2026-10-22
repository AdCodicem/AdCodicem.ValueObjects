using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// Value objects bound by MVC from the query string, the route, headers and bodies, through an application served in
/// memory, and the codes of the rules they break carried into the automatic 400 response.
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
    [InlineData("/probe/purchase-orders?reference=PO-1042-AND-MORE", "reference", ValueObjectErrorCodes.TooLong)]
    [InlineData("/probe/purchase-orders?reference=", "reference", ValueObjectErrorCodes.Required)]
    public async Task A_rejected_value_is_answered_with_the_code_of_the_rule_it_breaks(string url, string member, string code)
    {
        var problem = await GetProblemAsync(url);

        problem.GetProperty("errorCodes").GetProperty(member).GetString().Should().Be(code);
        problem.GetProperty("errors").GetProperty(member).GetArrayLength().Should().Be(1);
    }

    /// <summary>
    /// A construction of a generic value object, which the registry describes the first time it is asked for it, binds
    /// as any other value object does, normalized.
    /// </summary>
    [Fact]
    public async Task A_construction_of_a_generic_value_object_binds_from_the_query_string()
        => (await _client.GetStringAsync("/probe/purchase-orders?reference=%20po-1042", TestContext.Current.CancellationToken))
            .Should().Be("PO-1042");

    /// <summary>
    /// The binder reads text in the invariant culture, where a comma is the group separator, which a decimal does not
    /// take there: <c>12,5</c>, written with a decimal comma, is refused rather than bound as 125.
    /// </summary>
    /// <param name="text">Text holding a comma.</param>
    [Theory]
    [InlineData("12,5")]
    [InlineData("1,234.5")]
    public async Task A_decimal_holding_a_group_separator_is_refused_rather_than_bound_as_another_amount(string text)
    {
        var problem = await GetProblemAsync($"/probe/total?total={text}");

        problem.GetProperty("errorCodes").GetProperty("total").GetString().Should().Be(ValueObjectErrorCodes.NotParsable);
        (await _client.GetStringAsync("/probe/total?total=12.5", TestContext.Current.CancellationToken)).Should().Be("12.50");
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
    /// MVC binds every element of a query array under the array's name, so a member can be refused more than once: each
    /// refused element's message is listed, and the code is the first one's, the rule its first error states, as a
    /// minimal API answers the same request.
    /// </summary>
    [Fact]
    public async Task Every_refused_element_of_a_query_array_is_listed_with_the_code_of_the_first_as_a_minimal_API_lists_it()
    {
        const string Query = "?quantities=1&quantities=-1&quantities=x";
        await using var minimalApi = new ProductionProblemDetailsApplication();
        await minimalApi.InitializeAsync();

        using var response = await _client.GetAsync($"/probe/batches{Query}", TestContext.Current.CancellationToken);
        using var minimalApiResponse = await minimalApi.Client.GetAsync($"/api/batches{Query}", TestContext.Current.CancellationToken);
        var problem = await ProblemReading.ReadProblemAsync(response);

        problem.Messages.Should().BeEquivalentTo(
            new Dictionary<string, string[]>
            {
                ["quantities"] =
                [
                    ProblemReading.Refusal<Quantity, short>("-1").Message,
                    ProblemReading.Refusal<Quantity, short>("x").Message,
                ],
            },
            options => options.WithStrictOrdering());
        problem.Codes.Should().Equal(new Dictionary<string, string> { ["quantities"] = ValueObjectErrorCodes.OutOfRange });
        problem.Should().Be(await ProblemReading.ReadProblemAsync(minimalApiResponse));
        (await _client.GetStringAsync("/probe/batches?quantities=1&quantities=2", TestContext.Current.CancellationToken)).Should().Be("2");
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
    /// A value object read from a header binds through MVC's header binder, which hands the package's binder the header
    /// alone: normalized, <see langword="null"/> where an optional one is absent or blank, under the parameter's or the
    /// member's name when the attribute names no header, every comma-separated value of an array, a construction of a
    /// generic value object, one written by hand, and a member of a model bound from the query string.
    /// </summary>
    /// <param name="url">The action.</param>
    /// <param name="header">The header.</param>
    /// <param name="value">Its value, or <see langword="null"/> to send none.</param>
    /// <param name="expected">What the action answers.</param>
    [Theory]
    [InlineData("/probe/headers/country", "X-Country", " lu", "LU")]
    [InlineData("/probe/headers/country", "X-Country", null, "none")]
    [InlineData("/probe/headers/country", "X-Country", "", "none")]
    [InlineData("/probe/headers/country", "X-Country", " ", "none")]
    [InlineData("/probe/headers/customer", "customer", "0192f4a0-0000-7000-8000-000000000001", "0192f4a0-0000-7000-8000-000000000001")]
    [InlineData("/probe/headers/quantities", "X-Quantity", "1, 2", "2")]
    [InlineData("/probe/headers/reference", "X-Reference", " po-1042", "PO-1042")]
    [InlineData("/probe/headers/counter", "X-Counter", "5", "5")]
    [InlineData("/probe/headers/search?page=1", "X-Country", "be", "BE")]
    [InlineData("/probe/headers/search?page=1", "ShipTo", " lu", "LU")]
    public async Task A_value_object_binds_from_the_header_its_parameter_names(string url, string header, string? value, string expected)
    {
        var (status, body) = await SendAsync(url, header, value);

        status.Should().Be(HttpStatusCode.OK);
        body.Should().Be(expected);
    }

    /// <summary>
    /// A header the value object refuses is answered 400, with the message of the rule and its code under the key MVC
    /// gives the error, as it keys an <see cref="int"/> it refuses: the name the attribute gives the header, prefixed as
    /// MVC prefixes the members of a model; where the attribute names none, the member's name, prefixed likewise, and
    /// for a parameter the empty key, unless a route, query or form value carries the parameter's name, which is then
    /// the key. Blank text where the value object cannot be <see langword="null"/> gets MVC's own message and
    /// <c>value_object.required</c>, and every refused element of an array is listed, under the code of the first.
    /// </summary>
    /// <param name="url">The action.</param>
    /// <param name="header">The header.</param>
    /// <param name="value">Its value.</param>
    /// <param name="key">The key of the errors and of the code.</param>
    /// <param name="code">The code of the rule it breaks.</param>
    /// <param name="messages">Every message listed under the key, in order.</param>
    [Theory]
    [InlineData("/probe/headers/country", "X-Country", "ZZ", "X-Country", ValueObjectErrorCodes.NotAKnownValue, "The value is not one of the accepted values.")]
    [InlineData("/probe/headers/country", "X-Country", "lu,fr", "X-Country", ValueObjectErrorCodes.TooLong, "The value must be at most 2 characters long.")]
    [InlineData("/probe/headers/customer", "customer", "not-a-guid", "", ValueObjectErrorCodes.NotParsable, "The text is not a valid System.Guid.")]
    [InlineData("/probe/headers/customer?customer=x", "customer", "not-a-guid", "customer", ValueObjectErrorCodes.NotParsable, "The text is not a valid System.Guid.")]
    [InlineData("/probe/headers/customer", "customer", "00000000-0000-0000-0000-000000000000", "", ValueObjectErrorCodes.Required, "A customer identifier must not be empty.")]
    [InlineData("/probe/headers/customer", "customer", "", "", ValueObjectErrorCodes.Required, "The value '' is invalid.")]
    [InlineData("/probe/headers/customer", "customer", " ", "", ValueObjectErrorCodes.Required, "The value ' ' is invalid.")]
    [InlineData("/probe/headers/quantities", "X-Quantity", "1,-1,x", "X-Quantity", ValueObjectErrorCodes.OutOfRange, "The value must be greater than or equal to 0.", "The text is not a valid short.")]
    [InlineData("/probe/headers/reference", "X-Reference", "PO-1042-AND-MORE", "X-Reference", ValueObjectErrorCodes.TooLong, "The value must be at most 12 characters long.")]
    [InlineData("/probe/headers/counter", "X-Counter", "abc", "X-Counter", ValueObjectErrorCodes.NotParsable, "The value is not a valid HandWrittenCounter.")]
    [InlineData("/probe/headers/search?page=1", "X-Country", "ZZ", "X-Country", ValueObjectErrorCodes.NotAKnownValue, "The value is not one of the accepted values.")]
    [InlineData("/probe/headers/search?search.page=1", "X-Country", "ZZ", "search.X-Country", ValueObjectErrorCodes.NotAKnownValue, "The value is not one of the accepted values.")]
    [InlineData("/probe/headers/search?page=1", "ShipTo", "ZZ", "ShipTo", ValueObjectErrorCodes.NotAKnownValue, "The value is not one of the accepted values.")]
    [InlineData("/probe/headers/search?search.page=1", "ShipTo", "ZZ", "search.ShipTo", ValueObjectErrorCodes.NotAKnownValue, "The value is not one of the accepted values.")]
    public async Task A_refused_header_is_answered_with_the_code_of_the_rule_under_the_key_MVC_gives_its_error(
        string url,
        string header,
        string value,
        string key,
        string code,
        params string[] messages)
    {
        var (status, body) = await SendAsync(url, header, value);

        status.Should().Be(HttpStatusCode.BadRequest);
        var problem = JsonSerializer.Deserialize<JsonElement>(body);
        problem.GetProperty("errorCodes").Deserialize<Dictionary<string, string>>().Should()
            .Equal(new Dictionary<string, string> { [key] = code });
        var errors = problem.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
        errors.Keys.Should().Equal([key], "every error is listed under the key of the code, and none elsewhere");
        errors[key].Should().Equal(messages);
    }

    /// <summary>
    /// MVC does not narrow its value providers to a header, which a binder reads alone: a route, query or form value
    /// named as the header is never bound to it, accepted or refused.
    /// </summary>
    /// <param name="query">The query string, naming the header.</param>
    [Theory]
    [InlineData("?X-Country=lu")]
    [InlineData("?X-Country=ZZ")]
    public async Task A_header_parameter_never_binds_from_a_query_value_named_as_the_header(string query)
        => (await _client.GetStringAsync($"/probe/headers/country{query}", TestContext.Current.CancellationToken)).Should().Be("none");

    /// <summary>
    /// A value object read from the body itself is read by the input formatter, as a JSON string, never from a query
    /// value of the same name, and a refused one is answered with its code under the root of the body.
    /// </summary>
    [Fact]
    public async Task A_value_object_read_from_the_body_itself_binds_through_the_input_formatter()
    {
        using var accepted = new StringContent("\"fr\"", Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync("/probe/countries?country=be", accepted, TestContext.Current.CancellationToken);
        var problem = await PostProblemAsync(_client, "/probe/countries?country=be", "\"ZZ\"");

        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("FR");
        problem.GetProperty("errorCodes").Deserialize<Dictionary<string, string>>().Should()
            .Equal(new Dictionary<string, string> { ["$"] = ValueObjectErrorCodes.NotAKnownValue });
    }

    /// <summary>
    /// A binder the parameter names, through <c>[ModelBinder(typeof(…))]</c>, binds it, whatever the request holds.
    /// </summary>
    [Fact]
    public async Task A_binder_the_parameter_names_binds_it()
        => (await _client.GetStringAsync("/probe/countries/named-binder?country=be", TestContext.Current.CancellationToken)).Should().Be("LU");

    /// <summary>
    /// A body that fails for a reason no value object gave, here JSON cut short, carries no code: the response is the
    /// framework's own, with no empty extension added to it.
    /// </summary>
    [Fact]
    public async Task A_rejection_no_value_object_gave_leaves_the_response_without_codes()
    {
        var problem = await PostProblemAsync(_client, "/probe/orders", """{"reference":""");

        problem.GetProperty("errors").EnumerateObject().Should().NotBeEmpty();
        problem.TryGetProperty(ValueObjectProblemDetails.ExtensionName, out _).Should().BeFalse();
    }

    /// <summary>
    /// A value a JSON body holds is refused by the converter, and the code of its rule is recorded under the JSON path
    /// MVC keys the error with, as the code of a route or query value is recorded under the parameter.
    /// </summary>
    /// <param name="json">The body.</param>
    /// <param name="path">The path of the refused member.</param>
    /// <param name="code">The code of the rule it breaks.</param>
    [Theory]
    [InlineData("""{"reference":"no"}""", "$.reference", ValueObjectErrorCodes.TooShort)]
    [InlineData("""{"reference":42}""", "$.reference", ValueObjectErrorCodes.NotParsable)]
    [InlineData("""{"reference":null}""", "$.reference", ValueObjectErrorCodes.Required)]
    public async Task A_value_a_JSON_body_refuses_is_answered_with_the_code_under_its_path(string json, string path, string code)
    {
        var problem = await PostProblemAsync(_client, "/probe/orders", json);

        problem.GetProperty("errorCodes").GetProperty(path).GetString().Should().Be(code);
        problem.GetProperty("errors").GetProperty(path).GetArrayLength().Should().Be(1);
    }

    /// <summary>
    /// The model state of a body holds what it holds without the package, whatever the application says of exception
    /// messages: the message of the exception where it keeps them, and the framework's "The input was not valid."
    /// where it does not. The codes are recorded either way. The global option keeps the value the application gave it.
    /// </summary>
    /// <param name="allowMessages">The application's <c>AllowInputFormatterExceptionMessages</c>.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_messages_of_a_body_are_the_framework_s_whatever_exception_messages_are_allowed(bool allowMessages)
    {
        await using var with = await StartAsync(static _ => { }, json: options => options.AllowInputFormatterExceptionMessages = allowMessages);
        await using var without = await StartAsync(static _ => { }, withValueObjectBinder: false, json: options => options.AllowInputFormatterExceptionMessages = allowMessages);
        using var withClient = with.GetTestClient();
        using var withoutClient = without.GetTestClient();

        foreach (var json in new[] { """{"reference":"no"}""", """{"reference":42}""", """{"reference":""" })
        {
            var expected = await PostProblemAsync(withoutClient, "/probe/orders", json);
            var actual = await PostProblemAsync(withClient, "/probe/orders", json);

            actual.GetProperty("errors").Deserialize<Dictionary<string, string[]>>().Should()
                .BeEquivalentTo(expected.GetProperty("errors").Deserialize<Dictionary<string, string[]>>(), "the errors of {0}", json);
        }

        var refused = await PostProblemAsync(withClient, "/probe/orders", """{"reference":"no"}""");
        refused.GetProperty("errorCodes").GetProperty("$.reference").GetString().Should().Be(ValueObjectErrorCodes.TooShort);
        refused.GetProperty("errors").GetProperty("$.reference")[0].GetString().Should().Be(allowMessages
            ? "The value is not a valid OrderReference: The value must be at least 3 characters long."
            : "The input was not valid.");
        with.Services.GetRequiredService<IOptions<JsonOptions>>().Value.AllowInputFormatterExceptionMessages.Should().Be(allowMessages);
    }

    /// <summary>
    /// The formatter reads the application's JSON options themselves, not a copy of them: exception messages turned off
    /// once the application runs are turned off for a body, as they are for the framework's formatter, and the code is
    /// recorded all the same.
    /// </summary>
    [Fact]
    public async Task Exception_messages_turned_off_once_the_application_runs_are_turned_off_for_a_body()
    {
        await using var application = await StartAsync(static _ => { });
        using var client = application.GetTestClient();
        var before = await PostProblemAsync(client, "/probe/orders", """{"reference":"no"}""");

        application.Services.GetRequiredService<IOptions<JsonOptions>>().Value.AllowInputFormatterExceptionMessages = false;
        var after = await PostProblemAsync(client, "/probe/orders", """{"reference":"no"}""");

        before.GetProperty("errors").GetProperty("$.reference")[0].GetString().Should()
            .Be("The value is not a valid OrderReference: The value must be at least 3 characters long.");
        after.GetProperty("errors").GetProperty("$.reference")[0].GetString().Should().Be("The input was not valid.");
        after.GetProperty("errorCodes").GetProperty("$.reference").GetString().Should().Be(ValueObjectErrorCodes.TooShort);
    }

    /// <summary>
    /// A request whose query string and body are both refused answers with both codes, each under its own key.
    /// </summary>
    [Fact]
    public async Task A_refused_query_value_and_a_refused_body_keep_their_own_codes()
    {
        var problem = await PostProblemAsync(_client, "/probe/orders/by-country?country=ZZ", """{"reference":"no"}""");

        problem.GetProperty("errorCodes").Deserialize<Dictionary<string, string>>().Should().Equal(new Dictionary<string, string>
        {
            ["country"] = ValueObjectErrorCodes.NotAKnownValue,
            ["$.reference"] = ValueObjectErrorCodes.TooShort,
        });
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
    private static async Task<WebApplication> StartAsync(
        Action<ApiBehaviorOptions> configure,
        bool withValueObjectBinder = true,
        Action<JsonOptions>? json = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        var mvc = builder.Services.AddControllers()
            .ConfigureApplicationPartManager(static manager =>
            {
                manager.ApplicationParts.Clear();
                manager.ApplicationParts.Add(new AssemblyPart(typeof(ProbeController).Assembly));
            });
        if (json is not null)
        {
            mvc.AddJsonOptions(json);
        }

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

    /// <summary>
    /// Sends a request with a header through the test server itself, since <see cref="HttpClient"/> drops a header whose
    /// value is empty.
    /// </summary>
    private async Task<(HttpStatusCode Status, string Body)> SendAsync(string url, string header, string? value)
    {
        var query = url.IndexOf('?', StringComparison.Ordinal);
        var context = await _application.GetTestServer().SendAsync(
            request =>
            {
                request.Request.Method = HttpMethods.Get;
                request.Request.Path = query < 0 ? url : url[..query];
                request.Request.QueryString = query < 0 ? QueryString.Empty : new QueryString(url[query..]);
                if (value is not null)
                {
                    request.Request.Headers[header] = value;
                }
            },
            TestContext.Current.CancellationToken);
        using var reader = new StreamReader(context.Response.Body);

        return ((HttpStatusCode)context.Response.StatusCode, await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<JsonElement> PostProblemAsync(HttpClient client, string url, string json)
    {
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(url, body, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private async Task<JsonElement> GetProblemAsync(string url)
    {
        using var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }
}
