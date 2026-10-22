using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// The OpenAPI document of an application taking every value object of the domain in a body, and value objects as
/// route, query and header parameters of minimal APIs and of MVC actions, built in memory, once, through ASP.NET
/// Core's own document generation.
/// </summary>
public sealed class OpenApiDocument : IAsyncLifetime
{
    /// <summary>Gets the schemas of the document's components, keyed by type name.</summary>
    public JsonElement Schemas { get; private set; }

    /// <summary>Gets the paths of the document, keyed by route template.</summary>
    public JsonElement Paths { get; private set; }

    /// <summary>Gets the schema of one component.</summary>
    /// <param name="name">Name of the component, the type's name.</param>
    /// <returns>Its schema.</returns>
    public JsonElement Schema(string name) => Schemas.GetProperty(name);

    /// <summary>Gets the schema of a property of the request body, a component itself.</summary>
    /// <param name="name">Name of the property, as written in JSON.</param>
    /// <returns>Its schema.</returns>
    public JsonElement BodyProperty(string name) => Schema(nameof(EveryValueObject)).GetProperty("properties").GetProperty(name);

    /// <summary>Gets the schema of a parameter of a <c>GET</c> operation.</summary>
    /// <param name="path">Route template of the operation.</param>
    /// <param name="name">Name of the parameter.</param>
    /// <returns>Its schema.</returns>
    public JsonElement Parameter(string path, string name)
        => Paths.GetProperty(path).GetProperty("get").GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == name)
            .GetProperty("schema");

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers()
            .ConfigureApplicationPartManager(static manager =>
            {
                manager.ApplicationParts.Clear();
                manager.ApplicationParts.Add(new ControllerPart(typeof(DocumentedAccountsController), typeof(HidingQueriesController)));
            })
            .AddValueObjects();
        builder.Services.AddOpenApi(static options =>
        {
            // A transformer that runs first and describes each value object as the object it is in memory, as a
            // generator blind to its converter would. The value object transformer must replace that shape.
            options.AddSchemaTransformer(static (schema, context, _) =>
            {
                if (ValueObjectRegistry.IsValueObject(context.JsonTypeInfo.Type))
                {
                    schema.Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["value"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    };
                    schema.Required = new HashSet<string> { "value" };
                }

                return Task.CompletedTask;
            });

            options.AddValueObjects();
        });

        await using var application = builder.Build();
        application.MapPost("/everything", static (EveryValueObject body) => Results.Ok(body));
        application.MapGet(
            "/accounts/{iban}",
            static (
                Iban iban,
                Quantity quantity,
                CountryCode? country,
                [FromHeader(Name = "X-Page")] PageNumber page,
                Duration? within,
                NoticeChannel<AccountFilter> notice,
                string note,
                int count) => Results.Ok());
        application.MapGet("/ledgers/{account}", static ([AsParameters] LedgerQuery query) => Results.Ok());
        application.MapGet("/quantities", static (Quantity[] quantity) => Results.Ok());
        application.MapGet(
            "/constrained/{page:range(5,50)}/{quantity:max(5000)}/{iban:length(20,30)}/{country:minlength(1):maxlength(5):regex(^[A-Z]+$)}",
            static (PageNumber page, Quantity quantity, Iban iban, CountryCode country) => Results.Ok());
        MapText(application);

        // A construction over a type of this document's own, which nothing but the transformer describes.
        application.MapGet("/text-generic", static ([ParsesAs(typeof(Reference<TextOwner>))] string? reference) => Results.Ok());
        application.MapControllers();
        application.MapOpenApi();
        await application.StartAsync();

        using var client = application.GetTestClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Schemas = document.GetProperty("components").GetProperty("schemas").Clone();
        Paths = document.GetProperty("paths").Clone();

        await application.StopAsync();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Maps the endpoints taking the text of value objects held to their rules with <c>[ParsesAs]</c>: in bodies, as
    /// route, query and header parameters, gathered with <c>[AsParameters]</c> in a class and in a record.
    /// </summary>
    /// <param name="application">The application.</param>
    internal static void MapText(WebApplication application)
    {
        application.MapPost("/text", static (EveryValueObjectAsText body) => Results.Ok());
        application.MapPost("/text-members", static (TextMembers body) => Results.Ok());
        application.MapGet(
            "/text/{iban:length(20,30)}",
            static (
                [ParsesAs(typeof(Iban))] string iban,
                [ParsesAs(typeof(Quantity))] string? quantity,
                [FromHeader(Name = "X-Country"), ParsesAs(typeof(CountryCode))] string? country,
                [ParsesAs<PageNumber, int>] string? page,
                [ParsesAs(typeof(Iban))] string[]? ibans,
                string? note) => Results.Ok());
        application.MapGet("/text-criteria", static ([AsParameters] TextCriteria criteria) => Results.Ok());
        application.MapGet("/text-search", static ([AsParameters] PropertyTargetedSearch search) => Results.Ok());
    }

    /// <summary>An application part holding the controllers the document describes, and those alone.</summary>
    /// <param name="controllers">The controllers.</param>
    private sealed class ControllerPart(params Type[] controllers) : ApplicationPart, IApplicationPartTypeProvider
    {
        /// <inheritdoc />
        public override string Name => nameof(OpenApiDocument);

        /// <inheritdoc />
        public IEnumerable<TypeInfo> Types => controllers.Select(static controller => controller.GetTypeInfo());
    }

    /// <summary>
    /// The owner of the references of <c>/text-generic</c>, private to this document, so that nothing else in the process
    /// has the registry describe <c>Reference&lt;TextOwner&gt;</c>.
    /// </summary>
    private sealed class TextOwner;
}

/// <summary>
/// A query model whose members its derived model hides with <c>new</c>, each with another type: MVC binds the most
/// derived one.
/// </summary>
public class TypedPagingQuery
{
    /// <summary>Gets or sets the text of a quantity, which the derived model hides with a number.</summary>
    [ParsesAs(typeof(Quantity))]
    public string? Page { get; set; }

    /// <summary>Gets or sets a number, which the derived model hides with the text of an account.</summary>
    public int Account { get; set; }

    /// <summary>Gets or sets the text of a country, which the derived model inherits as it is.</summary>
    [ParsesAs(typeof(CountryCode))]
    public string? Country { get; set; }
}

/// <summary>
/// A query model hiding the members of its base with <c>new</c>, of other types, which leaves both visible to
/// <see cref="Type.GetProperty(string)"/>: one without <c>[ParsesAs]</c>, one with it.
/// </summary>
public sealed class HidingPagingQuery : TypedPagingQuery
{
    /// <summary>Gets or sets the page, a number, held to no value object.</summary>
    public new int Page { get; set; }

    /// <summary>Gets or sets the text of an account.</summary>
    [ParsesAs(typeof(Iban))]
    public new string? Account { get; set; }
}

/// <summary>
/// An MVC action binding a query model that hides inherited members, which the built-in stack's document describes and
/// Swashbuckle, which looks a member of a model up by its name alone, cannot.
/// </summary>
[ApiController]
[Route("mvc/hiding")]
public sealed class HidingQueriesController : ControllerBase
{
    /// <summary>Searches by criteria whose model hides inherited members.</summary>
    /// <param name="query">The criteria.</param>
    /// <returns>No content.</returns>
    [HttpGet("search")]
    public IActionResult Search([FromQuery] HidingPagingQuery query) => NoContent();
}

/// <summary>
/// The parameters of a minimal API gathered in one type, each from its own source.
/// </summary>
/// <param name="Account">The account, from the route.</param>
/// <param name="Above">The least amount, from the query string, if any.</param>
/// <param name="Grade">A grade, from a header.</param>
public sealed record LedgerQuery(
    AccountId Account,
    Amount? Above,
    [property: FromHeader(Name = "X-Grade")] Grade Grade);

/// <summary>The criteria of an MVC search, bound from the query string as one model.</summary>
/// <param name="Least">The least quantity.</param>
/// <param name="Country">The country, if any.</param>
public sealed record AccountFilter(Quantity Least, CountryCode? Country);

/// <summary>
/// An MVC action taking value objects from the route, the query string and a header, which the document describes.
/// </summary>
[ApiController]
[Route("mvc/accounts")]
public sealed class DocumentedAccountsController : ControllerBase
{
    /// <summary>Accepts an account and what describes it.</summary>
    /// <param name="iban">The account, from the route.</param>
    /// <param name="quantity">A quantity, from the query string.</param>
    /// <param name="country">The country, if any, from the query string.</param>
    /// <param name="page">A page, from a header.</param>
    /// <returns>No content.</returns>
    [HttpGet("{iban}")]
    public IActionResult Get(
        Iban iban,
        [FromQuery] Quantity quantity,
        [FromQuery] CountryCode? country,
        [FromHeader(Name = "X-Page")] PageNumber page) => NoContent();

    /// <summary>Searches accounts by criteria bound from the query string as one model.</summary>
    /// <param name="filter">The criteria.</param>
    /// <returns>No content.</returns>
    [HttpGet("search")]
    public IActionResult Search([FromQuery] AccountFilter filter) => NoContent();

    /// <summary>Accepts the text of an account from the query string, held to its rules.</summary>
    /// <param name="iban">The account.</param>
    /// <returns>No content.</returns>
    [HttpGet("text")]
    public IActionResult Text([FromQuery, ParsesAs(typeof(Iban))] string? iban) => NoContent();

    /// <summary>Searches by the text of value objects, bound from the query string as one model, a class.</summary>
    /// <param name="search">The criteria.</param>
    /// <returns>No content.</returns>
    [HttpGet("text-search")]
    public IActionResult TextSearch([FromQuery] RawSearch search) => NoContent();

    /// <summary>Searches by the text of value objects, bound from the query string as one model, a record.</summary>
    /// <param name="search">The criteria.</param>
    /// <returns>No content.</returns>
    [HttpGet("text-search-record")]
    public IActionResult TextSearchRecord([FromQuery] RawSearchRecord search) => NoContent();
}

/// <summary>
/// A request body holding one of each value object of the domain, so that the document describes them all, and
/// collections and dictionaries of value objects.
/// </summary>
public sealed record EveryValueObject(
    Iban Iban,
    EmailAddress Email,
    Amount Amount,
    Percentage Percentage,
    CustomerId Customer,
    CountryCode Country,
    BirthDate Born,
    Quantity Quantity,
    Ordering.OrderReference Order,
    AccountId Account,
    LedgerEntryId LedgerEntry,
    Consent Consent,
    Grade Grade,
    Adjustment Adjustment,
    Score Score,
    Port Port,
    PageNumber Page,
    SequenceNumber Sequence,
    FileSize Size,
    ByteCount Bytes,
    LedgerBalance Balance,
    Fingerprint Fingerprint,
    Latitude Latitude,
    Ratio Ratio,
    OpeningTime Opens,
    RecordedAt Recorded,
    OccurredAt Occurred,
    Duration Took,
    PhoneNumber Phone,
    Label Label,
    DocumentStatus Status,
    Mass Mass,
    TransferLimit Limit,
    Luminance Luminance,
    Priority Priority,
    StorageQuota Quota,
    VatRate VatRate,
    VoteWeight VoteWeight,
    Opacity Opacity,
    TermsAccepted Terms,
    HttpStatus HttpStatus,
    BlockSize BlockSize,
    CutOffDate CutOff,
    ShiftStart Shift,
    LaunchMoment Launch,
    Answer Answer,
    List<Iban> Alternates,
    Quantity[] Quantities,
    Dictionary<string, Quantity> PerSku,
    List<List<Iban>> Groups,
    IReadOnlyList<CountryCode> Countries,
    List<Quantity?> Optional,
    List<CountryCode?> OptionalCountries,
    Dictionary<string, CountryCode?> MaybeCountryPerSite,
    Dictionary<CountryCode, Quantity> StockPerCountry,
    Dictionary<Quantity, int> CountPerQuantity,
    List<ServiceHour> Hours,
    NoticeChannel<AccountFilter> Notice);

// No summary, so no description: its bounds, written as text, are all its description says.
[ValueObject<TimeOnly>]
public readonly partial struct ServiceHour : IValueObjectMinimum<TimeOnly>, IValueObjectMaximum<TimeOnly>
{
    public static TimeOnly Minimum => new(8, 0);

    public static TimeOnly Maximum => new(18, 0);
}

/// <summary>The channel a notice about a record of one kind goes out on.</summary>
/// <typeparam name="TRecord">The kind of record.</typeparam>
[ValueObject<string>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct NoticeChannel<TRecord>
{
    [KnownValue(Description = "Sent to the address on file.")]
    public static readonly NoticeChannel<TRecord> Email = Known("email");

    [KnownValue]
    public static readonly NoticeChannel<TRecord> Sms = Known("sms");
}
