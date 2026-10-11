using System.Net;
using System.Text;
using AdCodicem.ValueObjects.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// The problem details of a minimal API, with the messages of the application's resource, read by the code of the rule
/// in the culture of the request: through the endpoint filter in Production, and through the exception handler in
/// Development, which runs outside the request localization middleware, as it does in the usual order. Each theory runs
/// in both.
/// </summary>
public sealed class LocalizedMinimalApiProblemDetailsTests
{
    /// <summary>The two settings of <c>RouteHandlerOptions.ThrowOnBadRequest</c>.</summary>
    public static TheoryData<bool> Modes => [false, true];

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Every_refused_value_object_is_answered_in_the_language_of_the_request(bool throwOnBadRequest)
    {
        await using var application = await StartAsync(throwOnBadRequest);
        using var client = application.GetTestClient();

        (await GetProblemAsync(client, "/api/accounts/XX")).Should().Be(Problem(
            ("iban", ValueObjectErrorCodes.TooShort, "« iban » : au moins 15 caractères.")));
        (await GetProblemAsync(client, "/api/lines?quantity=5000&rate=7")).Should().Be(Problem(
            ("quantity", ValueObjectErrorCodes.OutOfRange, "« quantity » doit être compris entre 0 et 1000."),
            ("rate", ValueObjectErrorCodes.NotAKnownValue, "« rate » doit être l'une des valeurs suivantes : 20,0; 5,5.")));
        (await GetProblemAsync(client, "/api/lines")).Should().Be(Problem(
            ("quantity", ValueObjectErrorCodes.Required, "« quantity » est obligatoire.")));
        (await GetProblemAsync(client, "/api/pages?page=0")).Should().Be(Problem(
            ("page", ValueObjectErrorCodes.OutOfRange, "« page » doit valoir au moins 1.")));
        (await GetProblemAsync(client, "/api/scores?score=200")).Should().Be(Problem(
            ("score", ValueObjectErrorCodes.OutOfRange, "« score » doit valoir au plus 100.")));
        (await GetProblemAsync(client, "/api/customers/current", ("X-Customer", "nope"))).Should().Be(Problem(
            ("X-Customer", ValueObjectErrorCodes.NotParsable, "« X-Customer » n'a pas la forme attendue.")));
        (await GetProblemAsync(client, "/api/search?Page=5000&c=ZZ")).Should().Be(Problem(
            ("Page", ValueObjectErrorCodes.OutOfRange, "« Page » doit être compris entre 0 et 1000."),
            ("c", ValueObjectErrorCodes.NotAKnownValue, "« c » doit être l'une des valeurs suivantes : FR; BE; LU."),
            ("X-Customer", ValueObjectErrorCodes.Required, "« X-Customer » est obligatoire.")));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Each_refused_element_of_an_array_is_answered_in_the_language_of_the_request(bool throwOnBadRequest)
    {
        await using var application = await StartAsync(throwOnBadRequest);

        var problem = await GetProblemAsync(application.GetTestClient(), "/api/batches?quantities=5000&quantities=x");

        problem.Should().Be(new ReadProblem(
            new() { ["quantities"] = ["« quantities » doit être compris entre 0 et 1000.", "« quantities » n'a pas la forme attendue."] },
            new() { ["quantities"] = ValueObjectErrorCodes.OutOfRange }));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task A_culture_the_resource_does_not_hold_keeps_the_English_messages(bool throwOnBadRequest)
    {
        await using var application = await StartAsync(throwOnBadRequest);

        var problem = await GetProblemAsync(application.GetTestClient(), "/api/lines?quantity=5000&culture=en-US");

        problem.Should().Be(Problem(("quantity", ValueObjectErrorCodes.OutOfRange, MinimalApiProblemDetailsTests.Refusal<Quantity, short>("5000").Message)));
    }

    /// <summary>
    /// The UI culture chooses the message and the culture formats its arguments: a request asking for French messages and
    /// English formats quotes a decimal and the separator of the known values as English writes them, through the filter
    /// and through the exception handler, which applies both cultures of the request.
    /// </summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public async Task The_UI_culture_chooses_the_message_and_the_culture_formats_its_arguments(bool throwOnBadRequest)
    {
        await using var application = await StartAsync(throwOnBadRequest);

        var problem = await GetProblemAsync(application.GetTestClient(), "/api/lines?quantity=5&rate=7&culture=en-US&ui-culture=fr-FR");

        problem.Should().Be(Problem(("rate", ValueObjectErrorCodes.NotAKnownValue, "« rate » doit être l'une des valeurs suivantes : 20.0, 5.5.")));
    }

    /// <summary>
    /// A request body is explained only by the exception handler, with <c>ThrowOnBadRequest</c> on: the message quotes the
    /// name of the value object, never the JSON path, and a type the registry does not describe has the key of its code
    /// alone. Its arguments are formatted in the culture of the request, which may differ from its UI culture.
    /// </summary>
    [Theory]
    [InlineData("/api/orders", """{"reference":"x"}""", "$.reference", ValueObjectErrorCodes.TooShort, "« OrderReference » : au moins 3 caractères.")]
    [InlineData("/api/references", """{"reference":"PO-1042-AND-MORE"}""", "$.reference", ValueObjectErrorCodes.TooLong, "« Reference » : au plus 12 caractères.")]
    [InlineData("/api/links", """{"target":1}""", "$.target", ValueObjectErrorCodes.NotParsable, "« AnyEntityId » n'a pas la forme attendue.")]
    [InlineData("/api/invoices?culture=en-US&ui-culture=fr-FR", """{"rate":7}""", "$.rate", ValueObjectErrorCodes.NotAKnownValue, "« VatRate » doit être l'une des valeurs suivantes : 20.0, 5.5.")]
    [InlineData("/api/english/orders", """{"reference":"x"}""", "$.reference", ValueObjectErrorCodes.TooShort, "The value is not a valid OrderReference: The value must be at least 3 characters long.")]
    public async Task A_value_object_a_body_refuses_is_answered_in_the_language_of_the_request(string url, string json, string member, string code, string message)
    {
        await using var application = await StartAsync(throwOnBadRequest: true);

        var problem = await PostProblemAsync(application.GetTestClient(), url, json);

        problem.Should().Be(Problem((member, code, message)));
    }

    /// <summary>
    /// The options belong to the endpoint: covered by its group and by itself, it takes its own, and a call without
    /// options keeps the group's. Options naming no resource keep the English messages.
    /// </summary>
    [Theory]
    [MemberData(nameof(Modes))]
    public async Task The_endpoint_s_own_options_win_over_its_group_s(bool throwOnBadRequest)
    {
        await using var application = await StartAsync(throwOnBadRequest);
        using var client = application.GetTestClient();

        (await GetProblemAsync(client, "/api/sparse/totals?total=-1")).Should().Be(Problem(
            ("total", ValueObjectErrorCodes.OutOfRange, "« total » : de 0 à .")));
        (await GetProblemAsync(client, "/api/kept/scores?score=200")).Should().Be(Problem(
            ("score", ValueObjectErrorCodes.OutOfRange, "« score » doit valoir au plus 100.")));
        (await GetProblemAsync(client, "/api/english/scores?score=200")).Should().Be(Problem(
            ("score", ValueObjectErrorCodes.OutOfRange, MinimalApiProblemDetailsTests.Refusal<Score, byte>("200").Message)));
        (await GetProblemAsync(client, "/api/english/scores")).Should().Be(Problem(
            ("score", ValueObjectErrorCodes.Required, "A value is required.")));
        (await GetProblemAsync(client, "/api/sparse/totals?total=-1&culture=en-US")).Should().Be(Problem(
            ("total", ValueObjectErrorCodes.OutOfRange, MinimalApiProblemDetailsTests.Refusal<Amount, decimal>("-1").Message)));
    }

    /// <summary>
    /// A template quoting an argument its code does not supply is the application's error, and so is a resource read
    /// without localization registered: both fail the request rather than hide behind the English text.
    /// </summary>
    [Fact]
    public async Task A_malformed_template_and_missing_localization_fail_the_request()
    {
        await using var malformed = await StartAsync(throwOnBadRequest: false, exceptionHandler: false);
        await using var unregistered = await StartAsync(throwOnBadRequest: false, exceptionHandler: false, localization: false);

        var format = () => malformed.GetTestClient().GetAsync("/api/sparse/totals?total=abc", TestContext.Current.CancellationToken);
        var localization = () => unregistered.GetTestClient().GetAsync("/api/scores?score=200", TestContext.Current.CancellationToken);

        await format.Should().ThrowAsync<FormatException>();
        (await localization.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("AddLocalization()");
    }

    [Fact]
    public async Task The_convention_with_messages_refuses_null()
    {
        await using var application = WebApplication.CreateSlimBuilder().Build();
        var group = application.MapGroup("/api");

        FluentActions.Invoking(() => ((RouteGroupBuilder)null!).WithValueObjectProblemDetails(static _ => { }))
            .Should().Throw<ArgumentNullException>().WithParameterName("builder");
        FluentActions.Invoking(() => group.WithValueObjectProblemDetails(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("configure");
    }

    /// <summary>
    /// Starts a minimal API whose <c>/api</c> group answers in French, by default, through
    /// <see cref="ValidationMessages"/>, and in English through <c>?culture=en-US</c>. The exception handler middleware
    /// comes before the request localization middleware, the usual order, in which the culture is restored by the time an
    /// exception reaches the handler.
    /// </summary>
    private static async Task<WebApplication> StartAsync(bool throwOnBadRequest, bool exceptionHandler = true, bool localization = true)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = throwOnBadRequest);
        builder.Services.AddProblemDetails(static options =>
            options.CustomizeProblemDetails = static context => context.ProblemDetails.Extensions.Remove("traceId"));
        builder.Services.AddValueObjectHttpProblemDetails();
        if (localization)
        {
            builder.Services.AddLocalization();
        }

        var application = builder.Build();
        if (exceptionHandler)
        {
            application.UseExceptionHandler();
        }

        application.UseRequestLocalization(static options => options
            .SetDefaultCulture("fr-FR")
            .AddSupportedCultures("fr-FR", "en-US")
            .AddSupportedUICultures("fr-FR", "en-US"));

        var api = application.MapGroup("/api").WithValueObjectProblemDetails(static messages => messages.UseResource<ValidationMessages>());
        api.MapGet("/accounts/{iban}", static (Iban iban) => iban.Value);
        api.MapGet("/lines", static (Quantity quantity, VatRate? rate) => $"{quantity} {rate}");
        api.MapGet("/pages", static (PageNumber page) => page.ToString());
        api.MapGet("/scores", static (Score score) => score.ToString());
        api.MapGet("/customers/current", static ([FromHeader(Name = "X-Customer")] CustomerId customer) => customer.ToString());
        api.MapGet("/search", static ([AsParameters] SearchQuery query) => $"{query.Page} {query.Country} {query.Customer}");
        api.MapGet("/batches", static (Quantity[] quantities) => quantities.Length);
        api.MapPost("/orders", static (ProbeOrder order) => order.Reference.Value);
        api.MapPost("/references", static (ProbeReference body) => body.Reference.Value);
        api.MapPost("/links", static (ProbeLink link) => link.Target.ToString());
        api.MapPost("/invoices", static (ProbeInvoice invoice) => invoice.Rate.Value);

        // Covered again by the endpoint itself: with a resource of its own, without options, and with options naming none.
        api.MapGet("/sparse/totals", static (Amount total) => total.ToString())
            .WithValueObjectProblemDetails(static messages => messages.UseResource<SparseMessages>());
        api.MapGet("/kept/scores", static (Score score) => score.ToString()).WithValueObjectProblemDetails();
        api.MapGet("/english/scores", static (Score score) => score.ToString()).WithValueObjectProblemDetails(static _ => { });
        api.MapPost("/english/orders", static (ProbeOrder order) => order.Reference.Value).WithValueObjectProblemDetails(static _ => { });

        await application.StartAsync(TestContext.Current.CancellationToken);

        return application;
    }

    private static async Task<ReadProblem> GetProblemAsync(HttpClient client, string url, params (string Name, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var (name, value) in headers)
        {
            request.Headers.Add(name, value);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        return await MinimalApiProblemDetailsTests.ReadProblemAsync(response);
    }

    private static async Task<ReadProblem> PostProblemAsync(HttpClient client, string url, string json)
    {
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(url, body, TestContext.Current.CancellationToken);

        return await MinimalApiProblemDetailsTests.ReadProblemAsync(response);
    }

    private static ReadProblem Problem(params (string Member, string Code, string Message)[] refusals)
        => new(
            refusals.ToDictionary(static refusal => refusal.Member, static refusal => new[] { refusal.Message }),
            refusals.ToDictionary(static refusal => refusal.Member, static refusal => refusal.Code));
}
