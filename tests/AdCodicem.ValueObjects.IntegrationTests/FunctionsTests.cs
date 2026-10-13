using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AdCodicem.ValueObjects.IntegrationTests.Fixtures;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// HTTP triggers of the Azure Functions isolated worker on its ASP.NET Core integration, in the real host: route
/// values and query values bound to value objects through their rules by <c>UseValueObjects()</c>, which the worker's own
/// conversion reads as JSON and fails with a 500, and a refused one answered by <c>UseValueObjectProblemDetails()</c>
/// with the validation problem a minimal API writes, carrying its code, in the request's language.
/// </summary>
[Collection(nameof(FunctionsHosts))]
public sealed class FunctionsTests(FunctionsHostFixture host)
{
    private const string Guid = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";

    /// <summary>The two styles that register the problem details inside the integration: the current template's builder and <c>HostBuilder</c>.</summary>
    public static TheoryData<string> Styles => ["after", "hostbuilder"];

    /// <summary>
    /// Text the worker's own JSON conversion refuses, a <see cref="System.Guid"/>, text, a date, an integer with leading
    /// zeros, binds through the value object's rules, a generic construction and a value object written by hand, which
    /// nothing registered, included, and a decimal in exponent form, which the worker's JSON conversion reads and a
    /// decimal's <c>TryParse</c> does not, binds as it did.
    /// </summary>
    [Theory]
    [MemberData(nameof(Styles))]
    public async Task Route_and_query_values_bind_through_the_value_objects_rules(string style)
    {
        var client = Client(style);

        (await GetTextAsync(client, $"orders/{Guid}")).Should().Be($"OrderId({Guid})");
        (await GetTextAsync(client, "lines/007")).Should().Be("Quantity(7)");
        (await GetTextAsync(client, "amounts/12.5")).Should().Be("Amount(12.5)");
        (await GetTextAsync(client, "amounts/1e2")).Should().Be("Amount(100)");
        (await GetTextAsync(client, "due/2026-10-13")).Should().Be("DueDate(2026-10-13)");
        (await GetTextAsync(client, "query?customer=ABC-12345")).Should().Be("CustomerCode('ABC-12345')");
        (await GetTextAsync(client, "query-optional")).Should().Be("CustomerCode? = null");
        (await GetTextAsync(client, "query-optional?customer=ABC-123")).Should().Be("CustomerCode? = ABC-123");
        (await GetTextAsync(client, "refs/%20po-1042%20")).Should().Be("Reference<PurchaseOrder>('PO-1042')");
        (await GetTextAsync(client, "hand/abc")).Should().Be("HandWrittenCode('ABC')");
    }

    /// <summary>
    /// The host keeps a query parameter named <c>code</c> for function keys, and never hands it a function, even an
    /// anonymous one: a value object bound under that name is absent, as the guide warns.
    /// </summary>
    [Fact]
    public async Task A_query_parameter_named_code_never_reaches_a_function()
    {
        (await GetTextAsync(host.After, "keyed?code=ABC-12345")).Should().Be("CustomerCode? = null");
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public async Task A_refused_route_value_is_answered_with_the_validation_problem_minimal_APIs_write(string style)
    {
        using var response = await Client(style).GetAsync("orders/not-a-guid", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        JsonSerializer.Serialize(JsonDocument.Parse(body).RootElement).Should().Be(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            ["title"] = "One or more validation errors occurred.",
            ["status"] = 400,
            ["errors"] = new Dictionary<string, string[]> { ["id"] = ["The text is not a valid System.Guid."] },
            ["errorCodes"] = new Dictionary<string, string> { ["id"] = "value_object.not_parsable" },
        }));
        body.Should().NotContain("not-a-guid", "an answer never carries the refused text");
    }

    /// <summary>
    /// Each refused value object of a request is listed with the rule that refused it: a bound, broken by a decimal in
    /// exponent form too, a pattern, a decimal written with a comma, a length, empty text, which the type's own rule
    /// refuses, nullable or not, a value absent where the worker requires one, and a value object written by hand.
    /// </summary>
    [Theory]
    [MemberData(nameof(Styles))]
    public async Task Every_refused_value_object_is_answered_with_the_rule_it_broke(string style)
    {
        var client = Client(style);

        (await GetProblemAsync(client, "lines/500")).Should().Equal(Refusal("qty", "The value must be less than or equal to 100.", "value_object.out_of_range"));
        (await GetProblemAsync(client, "amounts/12,5")).Should().Equal(Refusal("amount", "The text is not a valid decimal.", "value_object.not_parsable"));
        (await GetProblemAsync(client, "amounts/2000")).Should().Equal(Refusal("amount", "The value must be less than or equal to 1000.25.", "value_object.out_of_range"));
        (await GetProblemAsync(client, "amounts/2e3")).Should().Equal(Refusal("amount", "The value must be less than or equal to 1000.25.", "value_object.out_of_range"));
        (await GetProblemAsync(client, "query?customer=abc")).Should().Equal(Refusal("customer", "The value does not match the expected format.", "value_object.invalid_format"));
        (await GetProblemAsync(client, "query?customer=")).Should().Equal(Refusal("customer", "The value must not be empty.", "value_object.required"));
        (await GetProblemAsync(client, "query-optional?customer=")).Should().Equal(Refusal("customer", "The value must not be empty.", "value_object.required"));
        (await GetProblemAsync(client, "query")).Should().Equal(Refusal("customer", "A value is required.", "value_object.required"));
        (await GetProblemAsync(client, "refs/ABCDEFGHIJKLMNOP")).Should().Equal(Refusal("reference", "The value must be at most 12 characters long.", "value_object.too_long"));
        (await GetProblemAsync(client, "hand/a1")).Should().Equal(Refusal("letters", "A code holds ASCII letters only.", "value_object.invalid_format"));
        (await GetProblemAsync(client, "two/not-a-guid/500")).Should().Equal(
            Refusal("id", "The text is not a valid System.Guid.", "value_object.not_parsable")
                .Concat(Refusal("qty", "The value must be less than or equal to 100.", "value_object.out_of_range")));
    }

    /// <summary>
    /// A failure no value object explains keeps the worker's 500: a plain <see cref="int"/> the worker's JSON conversion
    /// refuses, alone, while beside a refused value object only the value object is listed.
    /// </summary>
    [Theory]
    [MemberData(nameof(Styles))]
    public async Task A_failure_no_value_object_explains_keeps_the_workers_500(string style)
    {
        var client = Client(style);

        using (var plain = await client.GetAsync("ints/007", TestContext.Current.CancellationToken))
        {
            plain.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }

        using (var accepted = await client.GetAsync("mixed/7/007", TestContext.Current.CancellationToken))
        {
            accepted.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }

        (await GetProblemAsync(client, "mixed/500/007")).Should().Equal(Refusal("qty", "The value must be less than or equal to 100.", "value_object.out_of_range"));
    }

    /// <summary>
    /// A <c>[FromBody]</c> value object is read by MVC, through the MVC package's <c>AddValueObjects()</c>, which records
    /// the code beside its message: answered under its JSON path, beside a refused route value.
    /// </summary>
    [Theory]
    [MemberData(nameof(Styles))]
    public async Task A_refused_request_body_is_answered_under_its_JSON_path(string style)
    {
        var client = Client(style);

        using (var accepted = await client.PostAsync("body", Order(7), TestContext.Current.CancellationToken))
        {
            accepted.StatusCode.Should().Be(HttpStatusCode.OK);
            (await accepted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be($"OrderSubmitted({Guid}, 'ABC-12345', 7)");
        }

        (await PostProblemAsync(client, "body", Order(500))).Should().Equal(
            Refusal("$.quantity", "The value is not a valid Quantity: The value must be less than or equal to 100.", "value_object.out_of_range"));
        (await PostProblemAsync(client, "body/500", Order(500))).Should().Equal(
            Refusal("$.quantity", "The value is not a valid Quantity: The value must be less than or equal to 100.", "value_object.out_of_range")
                .Concat(Refusal("qty", "The value must be less than or equal to 100.", "value_object.out_of_range")));
        (await PostProblemAsync(client, "body/500", Order(7))).Should().Equal(Refusal("qty", "The value must be less than or equal to 100.", "value_object.out_of_range"));
    }

    /// <summary>
    /// The messages of the application's resource, in the language the request asks for: through the request localization
    /// middleware a startup filter adds to the integration's pipeline on one host, through the request localization
    /// options alone on the other, from <c>?culture=</c> or <c>Accept-Language</c>. A culture the resource does not hold
    /// keeps the English messages.
    /// </summary>
    [Theory]
    [MemberData(nameof(Styles))]
    public async Task A_refusal_is_answered_in_the_language_of_the_request(string style)
    {
        var client = Client(style);

        (await GetProblemAsync(client, "lines/500?culture=fr-FR")).Should().Equal(Refusal("qty", "« qty » doit être compris entre 1 et 100.", "value_object.out_of_range"));
        (await GetProblemAsync(client, "amounts/2000?culture=fr-FR")).Should().Equal(Refusal("amount", "« amount » doit être compris entre 0,5 et 1000,25.", "value_object.out_of_range"));
        (await GetProblemAsync(client, "query", "fr-FR")).Should().Equal(Refusal("customer", "« customer » est obligatoire.", "value_object.required"));
        (await GetProblemAsync(client, "lines/500?culture=en-US")).Should().Equal(Refusal("qty", "The value must be less than or equal to 100.", "value_object.out_of_range"));
    }

    /// <summary>
    /// Registered before <c>ConfigureFunctionsWebApplication()</c>, the middleware runs outside the integration's, which
    /// writes the answer: a refusal keeps the worker's 500 rather than an empty 200, an accepted value binds, and a
    /// warning says why, once.
    /// </summary>
    [Fact]
    public async Task Registered_before_the_integration_a_refusal_keeps_the_workers_500_and_a_warning_says_why_once()
    {
        foreach (var path in new[] { "lines/500", "orders/not-a-guid", "query?customer=" })
        {
            using var refused = await host.Before.GetAsync(path, TestContext.Current.CancellationToken);
            refused.StatusCode.Should().Be(HttpStatusCode.InternalServerError, path);
            (await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        }

        (await GetTextAsync(host.Before, "lines/7")).Should().Be("Quantity(7)");

        var logs = await host.BeforeLogsAsync();
        logs.Split("UseValueObjectProblemDetails() found no HttpContext").Length.Should().Be(2, "the warning is logged once");
        logs.Should().Contain("Call it after ConfigureFunctionsWebApplication()");
    }

    /// <summary>
    /// The host counts an answered invocation as succeeded, as ASP.NET Core counts a 400: the middleware logs each answer
    /// once, with the parameters and their codes, never the values.
    /// </summary>
    [Fact]
    public async Task Each_answer_is_logged_with_the_parameters_and_their_codes_and_never_the_values()
    {
        (await GetProblemAsync(host.After, "two/zzunguidzz/qqmanyqq")).Should().HaveCount(2);

        var logs = await host.AfterLogsAsync();
        logs.Should().Contain("Function 'Two' answered 400 for refused value objects: id (value_object.not_parsable), qty (value_object.not_parsable).");
        logs.Should().NotContain("zzunguidzz").And.NotContain("qqmanyqq");
    }

    private static StringContent Order(int quantity)
        => new($$"""{"orderId":"{{Guid}}","customer":"ABC-12345","quantity":{{quantity}}}""", Encoding.UTF8, "application/json");

    private static KeyValuePair<string, (string Message, string Code)>[] Refusal(string member, string message, string code)
        => [new(member, (message, code))];

    private static async Task<string> GetTextAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        return body;
    }

    private static async Task<IEnumerable<KeyValuePair<string, (string Message, string Code)>>> GetProblemAsync(HttpClient client, string path, string? language = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (language is not null)
        {
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        return await ReadProblemAsync(response);
    }

    private static async Task<IEnumerable<KeyValuePair<string, (string Message, string Code)>>> PostProblemAsync(HttpClient client, string path, HttpContent content)
    {
        using var response = await client.PostAsync(path, content, TestContext.Current.CancellationToken);

        return await ReadProblemAsync(response);
    }

    /// <summary>Reads a validation problem as each member's single message and its code, in the order the body lists them.</summary>
    private static async Task<IEnumerable<KeyValuePair<string, (string Message, string Code)>>> ReadProblemAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, body);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = JsonDocument.Parse(body).RootElement;
        var codes = problem.GetProperty("errorCodes");

        return [.. problem.GetProperty("errors").EnumerateObject().Select(member => new KeyValuePair<string, (string, string)>(
            member.Name,
            (member.Value.EnumerateArray().Single().GetString()!, codes.GetProperty(member.Name).GetString()!)))];
    }

    private HttpClient Client(string style) => style == "after" ? host.After : host.HostBuilder;
}
