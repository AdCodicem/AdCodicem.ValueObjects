using System.CodeDom.Compiler;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;

namespace AdCodicem.ValueObjects.RdgTests;

/// <summary>
/// A value object the code the Request Delegate Generator writes refuses, answered with problem details carrying the
/// code of the rule it broke: through the endpoint filter in Production, and through the exception handler in
/// Development. Each theory runs in both.
/// </summary>
public sealed class ProblemDetailsTests(ProductionProblemApplication production, DevelopmentProblemApplication development)
    : IClassFixture<ProductionProblemApplication>, IClassFixture<DevelopmentProblemApplication>
{
    private const string Required = "A value is required.";

    /// <summary>The two settings of <c>RouteHandlerOptions.ThrowOnBadRequest</c>.</summary>
    public static TheoryData<bool> Modes => [false, true];

    /// <summary>
    /// The filter tells the endpoints the RDG wrote by the mark it adds to their metadata, and binds empty text as the
    /// RDG does on them alone: a change of that mark in the SDK fails here first.
    /// </summary>
    [Fact]
    public void Every_endpoint_the_Request_Delegate_Generator_writes_carries_its_mark()
    {
        var endpoints = production.Endpoints.OfType<RouteEndpoint>()
            .Where(static endpoint => endpoint.RoutePattern.RawText!.StartsWith("/problems/", StringComparison.Ordinal))
            .ToList();

        endpoints.Should().HaveCount(11);
        endpoints.Should().AllSatisfy(static endpoint => endpoint.Metadata.OfType<GeneratedCodeAttribute>()
            .Should().Contain(static attribute => attribute.Tool!.StartsWith("Microsoft.AspNetCore.Http.RequestDelegateGenerator,", StringComparison.Ordinal)));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task A_refused_route_value_is_listed_with_the_rule_it_broke(bool throwOnBadRequest)
    {
        (await GetProblemAsync(throwOnBadRequest, "/problems/items/AB12")).Should().Be(Problem(("item", Refusal<Sku, string>("AB12"))));
        (await GetProblemAsync(throwOnBadRequest, "/problems/warehouses/X12345"))
            .Should().Be(Problem(("warehouse", Refusal<Code<Warehouse>, string>("X12345"))));
        (await GetProblemAsync(throwOnBadRequest, "/problems/places/A")).Should().Be(Problem(("place", Refusal<Catalog.Shelf, string>("A"))));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Refused_query_values_and_an_absent_required_one_are_listed(bool throwOnBadRequest)
    {
        (await GetProblemAsync(throwOnBadRequest, "/problems/lines?amount=0&product=AB12"))
            .Should().Be(Problem(("amount", Refusal<Quantity, int>("0")), ("product", Refusal<Sku, string>("AB12"))));
        (await GetProblemAsync(throwOnBadRequest, "/problems/lines")).Should().Be(Problem(("amount", (Required, ValueObjectErrorCodes.Required))));
    }

    /// <summary>
    /// The RDG refuses no empty query text: it binds <see langword="null"/> to a nullable value object, and the default
    /// instance, which no rule checked, to an element of an array. Empty text is never reported, even beside a refusal.
    /// </summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Empty_text_the_Request_Delegate_Generator_binds_is_never_reported(bool throwOnBadRequest)
    {
        (await GetStringAsync(throwOnBadRequest, "/problems/lines?amount=5&product=")).Should().Be("5 ");
        (await GetStringAsync(throwOnBadRequest, "/problems/lines?amount=&product="))
            .Should().Be("0 ", "the default instance, below the minimum of 1, reaches the handler unchecked");
        (await GetProblemAsync(throwOnBadRequest, "/problems/lines?amount=x&product="))
            .Should().Be(Problem(("amount", Refusal<Quantity, int>("x"))));
        (await GetProblemAsync(throwOnBadRequest, "/problems/batches?counts=&counts=0&counts=101"))
            .Should().Be(new ReadProblem(
                new Dictionary<string, string[]> { ["counts"] = [Refusal<Quantity, int>("0").Message, Refusal<Quantity, int>("101").Message] },
                new Dictionary<string, string> { ["counts"] = ValueObjectErrorCodes.OutOfRange }));
    }

    /// <summary>
    /// The RDG reads every value of a scalar query key, or of a header sent twice, as their comma-joined text, which it
    /// parses, as the reflection-based binding does. It takes an empty header for an absent one, and refuses a required
    /// one as not provided, where it binds empty query text without a refusal.
    /// </summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public async Task A_repeated_query_key_or_header_is_reported_as_the_joined_text_and_an_empty_header_as_absent(bool throwOnBadRequest)
    {
        var owner = CustomerId.New().Value;
        using var twice = new HttpRequestMessage(HttpMethod.Get, "/problems/owner");
        twice.Headers.TryAddWithoutValidation("X-Owner", [owner, owner]);

        (await GetProblemAsync(throwOnBadRequest, "/problems/lines?amount=50&amount=1"))
            .Should().Be(Problem(("amount", Refusal<Quantity, int>("50,1"))));
        (await GetProblemAsync(throwOnBadRequest, "/problems/lines?amount=1&amount=1"))
            .Should().Be(Problem(("amount", Refusal<Quantity, int>("1,1"))));
        (await SendForProblemAsync(throwOnBadRequest, twice)).Should().Be(Problem(("X-Owner", Refusal<CustomerId, string>($"{owner},{owner}"))));
        (await SendWithEmptyHeaderAsync(throwOnBadRequest, "/problems/owner", "X-Owner")).Should().Be(Problem(("X-Owner", (Required, ValueObjectErrorCodes.Required))));
        (await GetProblemAsync(throwOnBadRequest, "/problems/lines?amount=&product=AB12"))
            .Should().Be(Problem(("product", Refusal<Sku, string>("AB12"))));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Named_values_headers_and_the_members_of_an_AsParameters_record_are_listed_under_their_names(bool throwOnBadRequest)
    {
        (await GetProblemAsync(throwOnBadRequest, "/problems/named/0?q=101"))
            .Should().Be(Problem(("n", Refusal<Quantity, int>("0")), ("q", Refusal<Quantity, int>("101"))));

        using var owner = new HttpRequestMessage(HttpMethod.Get, "/problems/owner") { Headers = { { "X-Owner", "cus_0" } } };
        (await SendForProblemAsync(throwOnBadRequest, owner)).Should().Be(Problem(("X-Owner", Refusal<CustomerId, string>("cus_0"))));

        using var search = new HttpRequestMessage(HttpMethod.Get, "/problems/search?page=0&s=AB12");
        (await SendForProblemAsync(throwOnBadRequest, search)).Should().Be(Problem(
            ("Page", Refusal<Quantity, int>("0")),
            ("s", Refusal<Sku, string>("AB12")),
            ("X-Buyer", (Required, ValueObjectErrorCodes.Required))));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task A_refusal_no_value_object_made_keeps_the_frameworks_answer(bool throwOnBadRequest)
    {
        using var response = await Client(throwOnBadRequest).GetAsync("/problems/mixed/abc?size=5", TestContext.Current.CancellationToken);

        await ShouldBeTheFrameworksAnswerAsync(response, throwOnBadRequest);
        (await GetProblemAsync(throwOnBadRequest, "/problems/mixed/abc?size=0")).Should().Be(Problem(("size", Refusal<Quantity, int>("0"))));
    }

    [Fact]
    public async Task A_refused_body_keeps_the_bare_400_in_Production()
    {
        using var response = await production.Client.PostAsJsonAsync("/problems/orders", new { product = "AB12", amount = 3 }, TestContext.Current.CancellationToken);

        await ShouldBeTheFrameworksAnswerAsync(response, throwOnBadRequest: false);
    }

    /// <summary>
    /// With <c>ThrowOnBadRequest</c> on, a refused body is listed under its JSON path; a value object read as the body,
    /// which the RDG reads with the serializer, is listed under the root, and its parameter is not parsed from text.
    /// </summary>
    [Fact]
    public async Task A_refused_body_is_listed_under_its_JSON_path_in_Development()
    {
        using (var response = await development.Client.PostAsJsonAsync("/problems/orders", new { product = "AB12", amount = 3 }, TestContext.Current.CancellationToken))
        {
            (await ReadProblemAsync(response)).Codes.Should().Equal(new Dictionary<string, string> { ["$.product"] = ValueObjectErrorCodes.InvalidFormat });
        }

        using (var content = new StringContent("\"AB12\"", Encoding.UTF8, "application/json"))
        using (var response = await development.Client.PostAsync("/problems/parcels", content, TestContext.Current.CancellationToken))
        {
            (await ReadProblemAsync(response)).Codes.Should().Equal(new Dictionary<string, string> { ["$"] = ValueObjectErrorCodes.InvalidFormat });
        }
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Values_that_bind_reach_the_handler(bool throwOnBadRequest)
    {
        (await GetStringAsync(throwOnBadRequest, "/problems/items/AB-12")).Should().Be("AB-12");
        (await GetStringAsync(throwOnBadRequest, "/problems/batches?counts=1&counts=2")).Should().Be("1,2");
    }

    private static (string Message, string Code) Refusal<TSelf, TValue>(string text)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        TSelf.TryParse(text, CultureInfo.InvariantCulture, out _, out var validation).Should().BeFalse($"'{text}' is refused");

        return (validation.ErrorMessage!, validation.ErrorCode!);
    }

    private static ReadProblem Problem(params (string Member, (string Message, string Code) Refusal)[] refusals)
        => new(
            refusals.ToDictionary(static refusal => refusal.Member, static refusal => new[] { refusal.Refusal.Message }),
            refusals.ToDictionary(static refusal => refusal.Member, static refusal => refusal.Refusal.Code));

    private static async Task<ReadProblem> ReadProblemAsync(HttpResponseMessage response)
        => ProblemOf((int)response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

    private static ReadProblem ProblemOf(int status, string body)
    {
        status.Should().Be(StatusCodes.Status400BadRequest, body);
        var problem = JsonDocument.Parse(body).RootElement;

        return new ReadProblem(
            problem.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!,
            problem.GetProperty(ValueObjectProblemDetails.ExtensionName).Deserialize<Dictionary<string, string>>()!);
    }

    private static async Task ShouldBeTheFrameworksAnswerAsync(HttpResponseMessage response, bool throwOnBadRequest)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(throwOnBadRequest ? HttpStatusCode.InternalServerError : HttpStatusCode.BadRequest, body);
        body.Should().NotContain(ValueObjectProblemDetails.ExtensionName);
    }

    private HttpClient Client(bool throwOnBadRequest) => throwOnBadRequest ? development.Client : production.Client;

    private Task<string> GetStringAsync(bool throwOnBadRequest, string url)
        => Client(throwOnBadRequest).GetStringAsync(url, TestContext.Current.CancellationToken);

    private async Task<ReadProblem> GetProblemAsync(bool throwOnBadRequest, string url)
    {
        using var response = await Client(throwOnBadRequest).GetAsync(url, TestContext.Current.CancellationToken);

        return await ReadProblemAsync(response);
    }

    /// <summary>
    /// Sends a request built on the server's own context, which carries an empty header as Kestrel hands one over, where
    /// <see cref="HttpClient"/> drops it.
    /// </summary>
    private async Task<ReadProblem> SendWithEmptyHeaderAsync(bool throwOnBadRequest, string path, string header)
    {
        var context = await (throwOnBadRequest ? (ProblemApplication)development : production).Server.SendAsync(
            request =>
            {
                request.Request.Method = HttpMethods.Get;
                request.Request.Path = path;
                request.Request.Headers[header] = string.Empty;
            },
            TestContext.Current.CancellationToken);
        using var reader = new StreamReader(context.Response.Body);

        return ProblemOf(context.Response.StatusCode, await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    private async Task<ReadProblem> SendForProblemAsync(bool throwOnBadRequest, HttpRequestMessage request)
    {
        using var response = await Client(throwOnBadRequest).SendAsync(request, TestContext.Current.CancellationToken);

        return await ReadProblemAsync(response);
    }
}

/// <summary>The messages and the codes a validation problem lists for each member, compared as values.</summary>
/// <param name="Messages">The <c>errors</c> member.</param>
/// <param name="Codes">The <c>errorCodes</c> member.</param>
public sealed record ReadProblem(Dictionary<string, string[]> Messages, Dictionary<string, string> Codes)
{
    /// <inheritdoc />
    public bool Equals(ReadProblem? other)
        => other is not null
           && Messages.Count == other.Messages.Count
           && Messages.All(entry => other.Messages.TryGetValue(entry.Key, out var messages) && messages.SequenceEqual(entry.Value))
           && Codes.Count == other.Codes.Count
           && Codes.All(entry => other.Codes.TryGetValue(entry.Key, out var code) && code == entry.Value);

    /// <inheritdoc />
    public override int GetHashCode() => Codes.Count;

    /// <inheritdoc />
    public override string ToString()
        => string.Join("; ", Messages.Select(entry => $"{entry.Key}: [{string.Join(" | ", entry.Value)}] {Codes.GetValueOrDefault(entry.Key)}"));
}
