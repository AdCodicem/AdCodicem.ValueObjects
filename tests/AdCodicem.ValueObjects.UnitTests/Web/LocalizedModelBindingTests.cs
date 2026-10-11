using System.Net;
using System.Text;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AspNetCore.ModelBinding;
using AdCodicem.ValueObjects.Identifiers;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// The messages of the value objects MVC refuses, read from the application's resource by the code of the rule, in the
/// culture of the request: <see cref="ValidationMessages"/> holds French alone, and English is kept everywhere else. The
/// codes are the same in every language.
/// </summary>
public sealed class LocalizedModelBindingTests : IAsyncLifetime
{
    private WebApplication _application = null!;
    private HttpClient _client = null!;

    /// <summary>The refusals of route and query values, each with the member, the code and the French message.</summary>
    public static TheoryData<string, string, string, string> FrenchRefusals => new()
    {
        { "/messages/country?country=ZZ", "country", ValueObjectErrorCodes.NotAKnownValue, "« country » doit être l'une des valeurs suivantes : FR; BE; LU." },
        { "/messages/purchase-orders?reference=PO-1042-AND-MORE", "reference", ValueObjectErrorCodes.TooLong, "« reference » : au plus 12 caractères." },
        { "/messages/total?total=-1", "total", ValueObjectErrorCodes.OutOfRange, "« total » doit valoir au moins 0." },
        { "/messages/scores?score=200", "score", ValueObjectErrorCodes.OutOfRange, "« score » doit valoir au plus 100." },
        { "/messages/birth-dates?date=1800-01-01", "date", ValueObjectErrorCodes.OutOfRange, "« date » doit être compris entre 01/01/1900 et 31/12/2100." },
        { "/messages/counter?counter=x", "counter", ValueObjectErrorCodes.NotParsable, "« counter » n'a pas la forme attendue." },
        { "/messages/customers/not-a-guid", "id", ValueObjectErrorCodes.NotParsable, "« id » n'a pas la forme attendue." },
    };

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        _application = await StartAsync(static mvc => mvc.AddValueObjects(static messages => messages.UseResource<ValidationMessages>()));
        _client = _application.GetTestClient();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _application.DisposeAsync();
    }

    [Theory]
    [MemberData(nameof(FrenchRefusals))]
    public async Task A_refused_value_is_answered_in_the_language_of_the_request_with_an_unchanged_code(string url, string member, string code, string message)
    {
        var problem = await GetProblemAsync(_client, url);

        problem.Should().Be(new ReadProblem(new() { [member] = [message] }, new() { [member] = code }));
    }

    /// <summary>
    /// A request in a culture the resource has no message for keeps the English message of the rule, with the same code.
    /// </summary>
    [Theory]
    [MemberData(nameof(FrenchRefusals))]
    public async Task A_culture_the_resource_does_not_hold_keeps_the_English_message(string url, string member, string code, string message)
    {
        var problem = await GetProblemAsync(_client, url + (url.Contains('?', StringComparison.Ordinal) ? "&" : "?") + "culture=en-US");

        problem.Codes.Should().Equal(new Dictionary<string, string> { [member] = code });
        problem.Messages[member].Should().ContainSingle().Which.Should().NotBe(message).And.StartWith("The ");
    }

    /// <summary>
    /// A header is listed under the name its attribute gives it and quoted, as any value MVC binds, by the display name of
    /// its member; an element of a header array by its type's. A culture the resource does not hold keeps the English
    /// message, with the same code.
    /// </summary>
    [Theory]
    [InlineData("/probe/headers/country", "X-Country", "ZZ", ValueObjectErrorCodes.NotAKnownValue, "« country » doit être l'une des valeurs suivantes : FR; BE; LU.")]
    [InlineData("/probe/headers/quantities", "X-Quantity", "1,-1", ValueObjectErrorCodes.OutOfRange, "« Quantity » doit être compris entre 0 et 1000.")]
    public async Task A_refused_header_is_answered_in_the_language_of_the_request(string url, string header, string value, string code, string message)
    {
        var french = await GetProblemAsync(_client, url, (header, value));
        var english = await GetProblemAsync(_client, url + "?culture=en-US", (header, value));

        french.Should().Be(new ReadProblem(new() { [header] = [message] }, new() { [header] = code }));
        english.Codes.Should().Equal(new Dictionary<string, string> { [header] = code });
        english.Messages[header].Should().ContainSingle().Which.Should().NotBe(message).And.StartWith("The ");
    }

    /// <summary>
    /// The UI culture chooses the message and the culture formats its arguments: a request asking for French messages
    /// and English formats quotes a date, a decimal and the separator of the known values as English writes them, in a
    /// route or query value and in a body alike.
    /// </summary>
    [Theory]
    [InlineData("/messages/birth-dates?date=1800-01-01", null, "date", ValueObjectErrorCodes.OutOfRange, "« date » doit être compris entre 1/1/1900 et 12/31/2100.")]
    [InlineData("/messages/rates?rate=7", null, "rate", ValueObjectErrorCodes.NotAKnownValue, "« rate » doit être l'une des valeurs suivantes : 20.0, 5.5.")]
    [InlineData("/messages/invoices", """{"rate":7}""", "$.rate", ValueObjectErrorCodes.NotAKnownValue, "« VatRate » doit être l'une des valeurs suivantes : 20.0, 5.5.")]
    public async Task The_UI_culture_chooses_the_message_and_the_culture_formats_its_arguments(string url, string? json, string member, string code, string message)
    {
        url += (url.Contains('?', StringComparison.Ordinal) ? "&" : "?") + "culture=en-US&ui-culture=fr-FR";

        var problem = json is null ? await GetProblemAsync(_client, url) : await PostProblemAsync(_client, url, json);

        problem.Should().Be(new ReadProblem(new() { [member] = [message] }, new() { [member] = code }));
    }

    /// <summary>
    /// Each element of an array is reported under the array, quoting the display name MVC gives an element, the type's.
    /// </summary>
    [Fact]
    public async Task Each_refused_element_of_an_array_is_answered_in_the_language_of_the_request()
    {
        var problem = await GetProblemAsync(_client, "/messages/batches?quantities=5000&quantities=-1");

        problem.Should().Be(new ReadProblem(
            new() { ["quantities"] = ["« Quantity » doit être compris entre 0 et 1000.", "« Quantity » doit être compris entre 0 et 1000."] },
            new() { ["quantities"] = ValueObjectErrorCodes.OutOfRange }));
    }

    /// <summary>
    /// Blank text says no more than no text, and keeps the framework's message, which MVC's own message provider
    /// localises.
    /// </summary>
    [Fact]
    public async Task Blank_text_keeps_the_framework_s_message()
    {
        var problem = await GetProblemAsync(_client, "/messages/customers/%20");

        problem.Should().Be(new ReadProblem(new() { ["id"] = ["The value ' ' is invalid."] }, new() { ["id"] = ValueObjectErrorCodes.Required }));
    }

    /// <summary>
    /// A value object a body refuses is reported under its JSON path, quoting the name of the value object, never the path,
    /// which holds the key a dictionary refused; a type the registry does not describe has the key of its code alone.
    /// </summary>
    [Theory]
    [InlineData("/messages/orders", """{"reference":"x"}""", "$.reference", ValueObjectErrorCodes.TooShort, "« OrderReference » : au moins 3 caractères.")]
    [InlineData("/messages/references", """{"reference":"PO-1042-AND-MORE"}""", "$.reference", ValueObjectErrorCodes.TooLong, "« Reference » : au plus 12 caractères.")]
    [InlineData("/messages/links", """{"target":1}""", "$.target", ValueObjectErrorCodes.NotParsable, "« AnyEntityId » n'a pas la forme attendue.")]
    public async Task A_value_object_a_body_refuses_is_answered_in_the_language_of_the_request(string url, string json, string member, string code, string message)
    {
        var problem = await PostProblemAsync(_client, url, json);

        problem.Should().Be(new ReadProblem(new() { [member] = [message] }, new() { [member] = code }));
    }

    /// <summary>
    /// With the input formatter's messages turned off, MVC answers every error of a body with the same text, and so does
    /// a localized application, with the code still recorded: no message is looked up, so that an application that
    /// registers no localization answers too.
    /// </summary>
    [Fact]
    public async Task A_body_keeps_the_framework_s_answer_where_it_hides_the_formatter_s_message()
    {
        await using var application = await StartAsync(
            static mvc => mvc.AddValueObjects(static messages => messages.UseResource<ValidationMessages>()),
            localization: false,
            json: static options => options.AllowInputFormatterExceptionMessages = false);

        var problem = await PostProblemAsync(application.GetTestClient(), "/messages/orders", """{"reference":"x"}""");

        problem.Should().Be(new ReadProblem(new() { ["$.reference"] = ["The input was not valid."] }, new() { ["$.reference"] = ValueObjectErrorCodes.TooShort }));
    }

    /// <summary>
    /// A resource lacking the key of a range of one bound gives the key of the code instead; one quoting an argument its
    /// code does not supply fails the request, the application's error to see.
    /// </summary>
    [Fact]
    public async Task A_missing_specific_key_falls_back_to_the_key_of_the_code_and_a_malformed_template_throws()
    {
        await using var application = await StartAsync(static mvc => mvc.AddValueObjects(static messages => messages.UseResource<SparseMessages>()));
        using var client = application.GetTestClient();

        (await GetProblemAsync(client, "/messages/total?total=-1")).Messages["total"].Should().Equal("« total » : de 0 à .");
        (await GetProblemAsync(client, "/messages/country?country=ZZ")).Messages["country"]
            .Should().Equal(MinimalApiProblemDetailsTests.Refusal<CountryCode, string>("ZZ").Message);

        var malformed = () => client.GetAsync("/messages/total?total=abc", TestContext.Current.CancellationToken);
        await malformed.Should().ThrowAsync<FormatException>();
    }

    [Fact]
    public async Task Messages_read_without_localization_registered_fail_naming_the_call()
    {
        await using var application = await StartAsync(
            static mvc => mvc.AddValueObjects(static messages => messages.UseResource<ValidationMessages>()),
            localization: false);

        var refused = () => application.GetTestClient().GetAsync("/messages/total?total=-1", TestContext.Current.CancellationToken);

        (await refused.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("AddLocalization()");
    }

    /// <summary>
    /// The options hold one binder provider, which a later call configures: messages configured on the options, the
    /// issue's wiring, survive a call on the builder that registers the JSON options, and a provider configured without
    /// a resource keeps the English messages.
    /// </summary>
    [Fact]
    public async Task The_options_hold_one_binder_provider_whatever_the_calls()
    {
        await using var application = await StartAsync(
            static mvc => mvc.AddMvcOptions(static options => options.AddValueObjects(static messages => messages.UseResource<ValidationMessages>()))
                .AddValueObjects());
        await using var english = await StartAsync(static mvc => mvc.AddValueObjects(static _ => { }));

        application.Services.GetRequiredService<IOptions<MvcOptions>>().Value.ModelBinderProviders.OfType<ValueObjectModelBinderProvider>()
            .Should().ContainSingle();
        (await GetProblemAsync(application.GetTestClient(), "/messages/scores?score=200")).Messages["score"].Should().Equal("« score » doit valoir au plus 100.");
        (await PostProblemAsync(application.GetTestClient(), "/messages/orders", """{"reference":"x"}""")).Messages["$.reference"]
            .Should().Equal("« OrderReference » : au moins 3 caractères.");
        (await GetProblemAsync(english.GetTestClient(), "/messages/scores?score=200")).Messages["score"]
            .Should().Equal(MinimalApiProblemDetailsTests.Refusal<Score, byte>("200").Message);
        (await PostProblemAsync(english.GetTestClient(), "/messages/orders", """{"reference":"x"}""")).Messages["$.reference"]
            .Should().Equal("The value is not a valid OrderReference: The value must be at least 3 characters long.");
    }

    /// <summary>
    /// Messages configured on the options alone reach the binder; a body is read by the formatter the builder puts in
    /// place, which is not there, and keeps the converter's message.
    /// </summary>
    [Fact]
    public async Task Messages_configured_on_the_options_alone_reach_the_binder_and_not_a_body()
    {
        await using var application = await StartAsync(
            static mvc => mvc.AddMvcOptions(static options => options.AddValueObjects(static messages => messages.UseResource<ValidationMessages>())));

        (await GetProblemAsync(application.GetTestClient(), "/messages/scores?score=200")).Messages["score"].Should().Equal("« score » doit valoir au plus 100.");
        var body = await PostProblemAsync(application.GetTestClient(), "/messages/orders", """{"reference":"x"}""");
        body.Messages["$.reference"].Should().Equal("The value is not a valid OrderReference: The value must be at least 3 characters long.");
        body.Codes.Should().BeEmpty("no formatter of the package records a code");
    }

    [Fact]
    public void The_message_options_and_the_wiring_refuse_null()
    {
        var options = new ValueObjectMessageOptions();

        FluentActions.Invoking(() => options.UseResource(null!)).Should().Throw<ArgumentNullException>().WithParameterName("resourceType");
        options.UseResource<ValidationMessages>().Should().BeSameAs(options);
        options.ResourceType.Should().Be<ValidationMessages>();
        FluentActions.Invoking(() => ((MvcOptions)null!).AddValueObjects(static _ => { })).Should().Throw<ArgumentNullException>().WithParameterName("options");
        FluentActions.Invoking(() => new MvcOptions().AddValueObjects(null!)).Should().Throw<ArgumentNullException>().WithParameterName("configure");
        FluentActions.Invoking(() => ((IMvcBuilder)null!).AddValueObjects(static _ => { })).Should().Throw<ArgumentNullException>().WithParameterName("builder");
        FluentActions.Invoking(() => new ServiceCollection().AddControllers().AddValueObjects(null!)).Should().Throw<ArgumentNullException>().WithParameterName("configure");
    }

    /// <summary>
    /// Starts an MVC application holding <see cref="MessagesProbeController"/>, whose requests choose their culture, French
    /// by default and English through <c>?culture=en-US</c>.
    /// </summary>
    private static async Task<WebApplication> StartAsync(Action<IMvcBuilder> configure, bool localization = true, Action<JsonOptions>? json = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        if (localization)
        {
            builder.Services.AddLocalization();
        }

        var mvc = builder.Services.AddControllers()
            .ConfigureApplicationPartManager(static manager =>
            {
                manager.ApplicationParts.Clear();
                manager.ApplicationParts.Add(new AssemblyPart(typeof(MessagesProbeController).Assembly));
            });
        if (json is not null)
        {
            mvc.AddJsonOptions(json);
        }

        configure(mvc);
        builder.Services.Configure<ApiBehaviorOptions>(static options => options.AddValueObjectProblemDetails());

        var application = builder.Build();
        application.UseRequestLocalization(static options => options
            .SetDefaultCulture("fr-FR")
            .AddSupportedCultures("fr-FR", "en-US")
            .AddSupportedUICultures("fr-FR", "en-US"));
        application.MapControllers();
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

        return await ReadProblemAsync(response);
    }

    private static async Task<ReadProblem> PostProblemAsync(HttpClient client, string url, string json)
    {
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(url, body, TestContext.Current.CancellationToken);

        return await ReadProblemAsync(response);
    }

    /// <summary>Reads the problem a response answers, with no codes when it carries none.</summary>
    private static async Task<ReadProblem> ReadProblemAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, text);
        var problem = JsonDocument.Parse(text).RootElement;

        return new ReadProblem(
            problem.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!,
            problem.TryGetProperty(ValueObjectProblemDetails.ExtensionName, out var codes)
                ? codes.Deserialize<Dictionary<string, string>>()!
                : []);
    }
}

/// <summary>
/// Actions taking value objects from the route, the query string and a body, for the localized MVC applications of
/// <see cref="LocalizedModelBindingTests"/>.
/// </summary>
[ApiController]
[Route("messages")]
public sealed class MessagesProbeController : ControllerBase
{
    /// <summary>Echoes an optional country.</summary>
    /// <param name="country">The country, if any.</param>
    /// <returns>The country, or <c>none</c>.</returns>
    [HttpGet("country")]
    public IActionResult Country([FromQuery] CountryCode? country) => Ok(country?.Value ?? "none");

    /// <summary>Echoes a customer read from the route.</summary>
    /// <param name="id">The customer.</param>
    /// <returns>The customer.</returns>
    [HttpGet("customers/{id}")]
    public IActionResult Customer(CustomerId id) => Ok(id.ToString());

    /// <summary>Echoes an amount, which declares a minimum alone.</summary>
    /// <param name="total">The amount.</param>
    /// <returns>The amount.</returns>
    [HttpGet("total")]
    public IActionResult Total([FromQuery] Amount total) => Ok(total.Value);

    /// <summary>Echoes a score, which declares a maximum alone.</summary>
    /// <param name="score">The score.</param>
    /// <returns>The score.</returns>
    [HttpGet("scores")]
    public IActionResult Scores([FromQuery] Score score) => Ok(score.Value);

    /// <summary>Echoes a birth date, bounded by two dates.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The date.</returns>
    [HttpGet("birth-dates")]
    public IActionResult BirthDates([FromQuery] BirthDate date) => Ok(date.ToString());

    /// <summary>Echoes a counter written by hand, whose parser refuses without a reason.</summary>
    /// <param name="counter">The counter.</param>
    /// <returns>The counter.</returns>
    [HttpGet("counter")]
    public IActionResult Counter([FromQuery] HandWrittenCounter counter) => Ok(counter.Value);

    /// <summary>Echoes a rate, one of two fractions.</summary>
    /// <param name="rate">The rate.</param>
    /// <returns>The rate.</returns>
    [HttpGet("rates")]
    public IActionResult Rates([FromQuery] VatRate rate) => Ok(rate.Value);

    /// <summary>Counts the quantities read from the query string.</summary>
    /// <param name="quantities">The quantities.</param>
    /// <returns>How many.</returns>
    [HttpGet("batches")]
    public IActionResult Batches([FromQuery] Quantity[] quantities) => Ok(quantities.Length);

    /// <summary>Echoes a reference of a construction of a generic value object.</summary>
    /// <param name="reference">The reference.</param>
    /// <returns>The reference.</returns>
    [HttpGet("purchase-orders")]
    public IActionResult PurchaseOrders([FromQuery] Reference<PurchaseOrder> reference) => Ok(reference.Value);

    /// <summary>Echoes the reference of an order read from the body.</summary>
    /// <param name="order">The order.</param>
    /// <returns>Its reference.</returns>
    [HttpPost("orders")]
    public IActionResult Orders([FromBody] ProbeOrder? order) => Ok(order?.Reference.Value);

    /// <summary>Echoes the rate of an invoice read from the body.</summary>
    /// <param name="invoice">The invoice.</param>
    /// <returns>Its rate.</returns>
    [HttpPost("invoices")]
    public IActionResult Invoices([FromBody] ProbeInvoice? invoice) => Ok(invoice?.Rate.Value);

    /// <summary>Echoes a reference of a construction of a generic value object read from the body.</summary>
    /// <param name="body">The body.</param>
    /// <returns>Its reference.</returns>
    [HttpPost("references")]
    public IActionResult References([FromBody] ProbeReference? body) => Ok(body?.Reference.Value);

    /// <summary>Echoes the identifier of any entity read from the body.</summary>
    /// <param name="link">The link.</param>
    /// <returns>Its target.</returns>
    [HttpPost("links")]
    public IActionResult Links([FromBody] ProbeLink? link) => Ok(link?.Target.ToString());
}

/// <summary>A body holding a rate, one of two fractions.</summary>
/// <param name="Rate">The rate.</param>
public sealed record ProbeInvoice(VatRate Rate);

/// <summary>A body holding a construction of a generic value object.</summary>
/// <param name="Reference">The reference.</param>
public sealed record ProbeReference(Reference<PurchaseOrder> Reference);

/// <summary>A body holding the identifier of any entity, which no descriptor describes.</summary>
/// <param name="Target">The identifier.</param>
public sealed record ProbeLink(AnyEntityId Target);
