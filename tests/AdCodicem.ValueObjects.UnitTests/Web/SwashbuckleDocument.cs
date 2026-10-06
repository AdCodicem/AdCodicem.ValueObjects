using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.OpenApi;
using AdCodicem.ValueObjects.Swashbuckle;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// The OpenAPI documents Swashbuckle builds for the endpoints the built-in stack's document describes, plus a body of
/// the members Swashbuckle describes its own way, with value objects documented through
/// <see cref="ValueObjectSwaggerGenExtensions.AddValueObjects(SwaggerGenOptions)"/>, under the web defaults ASP.NET Core
/// writes with, which read numbers from text.
/// </summary>
public sealed class SwashbuckleDocument : IAsyncLifetime
{
    /// <summary>Gets the documents.</summary>
    public SwashbuckleDocuments Documents { get; private set; } = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
        => Documents = await SwashbuckleDocuments.BuildAsync(static options => options.AddValueObjects());

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// The document Swashbuckle builds and the one the built-in stack builds for the same application, whose minimal API
/// options read and write numbers as numbers only, where both describe a number as a number.
/// </summary>
public sealed class SwashbuckleParityDocument : IAsyncLifetime
{
    /// <summary>Gets the documents.</summary>
    public SwashbuckleDocuments Documents { get; private set; } = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
        => Documents = await SwashbuckleDocuments.BuildAsync(
            static options => options.AddValueObjects(),
            static services => services.ConfigureHttpJsonOptions(static json => json.SerializerOptions.NumberHandling = JsonNumberHandling.Strict),
            builtIn: true);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// The OpenAPI 3.0 and 3.1 documents Swashbuckle serves for one application, and the built-in stack's, when it is
/// asked for.
/// </summary>
/// <param name="V30">The OpenAPI 3.0 document, Swashbuckle's default.</param>
/// <param name="V31">The OpenAPI 3.1 document.</param>
/// <param name="BuiltIn">The built-in stack's document, an OpenAPI 3.1 one, if any.</param>
public sealed record SwashbuckleDocuments(JsonElement V30, JsonElement V31, JsonElement? BuiltIn)
{
    /// <summary>The versions a test runs over.</summary>
    public static TheoryData<string> Versions => ["3.0", "3.1"];

    /// <summary>Gets the document of a version.</summary>
    /// <param name="version"><c>3.0</c> or <c>3.1</c>.</param>
    /// <returns>The document.</returns>
    public JsonElement Of(string version) => version == "3.0" ? V30 : V31;

    /// <summary>Gets the schema of a component.</summary>
    /// <param name="version"><c>3.0</c> or <c>3.1</c>.</param>
    /// <param name="id">The component's identifier.</param>
    /// <returns>Its schema.</returns>
    public JsonElement Schema(string version, string id) => Of(version).GetProperty("components").GetProperty("schemas").GetProperty(id);

    /// <summary>Gets the schema of a property of a component.</summary>
    /// <param name="version"><c>3.0</c> or <c>3.1</c>.</param>
    /// <param name="id">The component's identifier.</param>
    /// <param name="name">The property's name, as written in JSON.</param>
    /// <returns>Its schema.</returns>
    public JsonElement Property(string version, string id, string name) => Schema(version, id).GetProperty("properties").GetProperty(name);

    /// <summary>Gets a parameter of a <c>GET</c> operation.</summary>
    /// <param name="version"><c>3.0</c> or <c>3.1</c>.</param>
    /// <param name="path">Route template of the operation.</param>
    /// <param name="name">Name of the parameter.</param>
    /// <returns>The parameter.</returns>
    public JsonElement Parameter(string version, string path, string name)
        => Of(version).GetProperty("paths").GetProperty(path).GetProperty("get").GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == name);

    /// <summary>
    /// Builds an application whose documents describe the domain's value objects, and reads its documents.
    /// </summary>
    /// <param name="configure">Configures Swashbuckle, the document <c>v1</c> declared.</param>
    /// <param name="services">Configures the application's services, if anything more is needed.</param>
    /// <param name="builtIn">Whether to build the built-in stack's document too, with its value object transformer, for
    /// the endpoints both describe: the body of <see cref="SwashbuckleMembers"/> is left out.</param>
    /// <returns>The documents.</returns>
    public static async Task<SwashbuckleDocuments> BuildAsync(
        Action<SwaggerGenOptions> configure,
        Action<IServiceCollection>? services = null,
        bool builtIn = false)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers()
            .ConfigureApplicationPartManager(static manager =>
            {
                manager.ApplicationParts.Clear();
                manager.ApplicationParts.Add(new ControllerPart(typeof(DocumentedAccountsController)));
            })
            .AddValueObjects();
        builder.Services.AddEndpointsApiExplorer();
        services?.Invoke(builder.Services);
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Value objects", Version = "v1" });
            configure(options);
        });
        if (builtIn)
        {
            builder.Services.AddOpenApi(static options => options.AddValueObjects());
        }

        await using var application = builder.Build();
        application.MapPost("/everything", static (EveryValueObject body) => Results.Ok(body));
        if (!builtIn)
        {
            // The built-in stack writes a [DefaultValue] as the member's type, which the text of a nullable value object's
            // is not: it fails to build its document.
            application.MapPost("/members", static (SwashbuckleMembers body) => Results.Ok(body));
        }

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
        application.MapControllers();
        application.UseSwagger();
        application.UseSwagger(static options =>
        {
            options.RouteTemplate = "swagger31/{documentName}/swagger.json";
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
        });
        if (builtIn)
        {
            application.MapOpenApi();
        }

        await application.StartAsync();

        using var client = application.GetTestClient();
        var v30 = Compact(await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json"));
        var v31 = Compact(await client.GetFromJsonAsync<JsonElement>("/swagger31/v1/swagger.json"));
        JsonElement? builtInDocument = builtIn ? Compact(await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json")) : null;

        await application.StopAsync();

        return new SwashbuckleDocuments(v30, v31, builtInDocument);
    }

    /// <summary>Writes a document without the indentation it was served with, so that a schema's text is its JSON.</summary>
    private static JsonElement Compact(JsonElement document) => JsonElement.Parse(JsonSerializer.Serialize(document));

    /// <summary>An application part holding one controller, so that the document describes that one alone.</summary>
    /// <param name="controller">The controller.</param>
    private sealed class ControllerPart(Type controller) : ApplicationPart, IApplicationPartTypeProvider
    {
        /// <inheritdoc />
        public override string Name => controller.Name;

        /// <inheritdoc />
        public IEnumerable<TypeInfo> Types => [controller.GetTypeInfo()];
    }
}

/// <summary>A shade, an enumeration Swashbuckle describes itself, beside the value objects.</summary>
public enum Shade
{
    /// <summary>Light.</summary>
    Light,

    /// <summary>Dark.</summary>
    Dark,
}

/// <summary>
/// A request body of the members Swashbuckle describes its own way: nullable value objects as properties, elements and
/// dictionary values, beside a nullable primitive and a nullable enumeration it describes itself; members carrying
/// attributes Swashbuckle reads, on the member or on the type its metadata names; a dictionary of a type of its own;
/// value objects written by hand and a construction of a generic one; and a member of each underlying number type, to
/// which Swashbuckle gives the schema a value object over that type is documented with, but for a 128-bit integer,
/// which travels as a string.
/// </summary>
public sealed record SwashbuckleMembers(
    Iban? Alternate,
    CountryCode? Country,
    [property: Required] Quantity? Demanded,
    [property: Obsolete("The alternate replaces it.")] Iban? Former,
    [property: DefaultValue("FR")] CountryCode? Fallback,
    [property: MaxLength(5)] Iban Bounded,
    [property: MaxLength(5)] Iban? Limited,
    [property: MaxLength(5)] string? RawLimited,
    Dictionary<string, Quantity?> MaybePerSku,
    Dictionary<Shade, Quantity?> PerShade,
    Dictionary<Shade, int?> RawPerShade,
    SwashbuckleStock Stock,
    SwashbuckleAnnotated Annotated,
    List<Quantity?> Optional,
    Hashtable Untyped,
    int? Raw,
    Shade? RawShade,
    List<int?> RawOptional,
    HandWrittenLevel Level,
    HandWrittenCounter Counter,
    HandWrittenTag<PurchaseOrder> Tag,
    Reference<PurchaseOrder> Order,
    sbyte RawSByte,
    byte RawByte,
    short RawInt16,
    ushort RawUInt16,
    int RawInt32,
    uint RawUInt32,
    long RawInt64,
    ulong RawUInt64,
    decimal RawDecimal,
    double RawDouble,
    float RawSingle,
    Int128 RawInt128)
{
    /// <summary>Gets an account only ever written, never read from a request.</summary>
    public Iban? Shown { get; }

    /// <summary>Sets an account only ever read from a request, never written.</summary>
    public Iban? Replacement { private get; set; }
}

/// <summary>A stock per country, a dictionary whose key Swashbuckle finds among the interfaces of its base type.</summary>
public sealed class SwashbuckleStock : Dictionary<CountryCode, int>;

/// <summary>A body whose rules sit on the type its metadata names, where Swashbuckle reads them too.</summary>
/// <param name="Demanded">A quantity its metadata marks required.</param>
[ModelMetadataType(typeof(SwashbuckleAnnotations))]
public sealed record SwashbuckleAnnotated(Quantity? Demanded);

/// <summary>The metadata of <see cref="SwashbuckleAnnotated"/>.</summary>
public sealed class SwashbuckleAnnotations
{
    /// <summary>Gets a quantity that must be given.</summary>
    [Required]
    public Quantity? Demanded { get; init; }
}
