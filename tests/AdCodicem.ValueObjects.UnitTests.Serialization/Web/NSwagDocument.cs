using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.NJsonSchema;
using AdCodicem.ValueObjects.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema.Generation;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using NJsonSchemaType = NJsonSchema.SchemaType;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>How the application whose documents NSwag builds serializes, and which document NSwag builds.</summary>
public enum NSwagMode
{
    /// <summary>MVC and the minimal APIs on System.Text.Json, an OpenAPI 3.0 document.</summary>
    SystemTextJson,

    /// <summary>MVC on Newtonsoft.Json with <c>ValueObjectConverter</c>, an OpenAPI 3.0 document.</summary>
    Newtonsoft,

    /// <summary>MVC and the minimal APIs on System.Text.Json, a Swagger 2.0 document.</summary>
    Swagger2,

    /// <summary>The built-in stack's document beside NSwag's, numbers read and written as numbers only.</summary>
    Parity,

    /// <summary>The minimal APIs alone, NSwag left to choose its settings.</summary>
    MinimalApisAlone,

    /// <summary>The minimal APIs alone, NSwag handed their System.Text.Json options.</summary>
    MinimalApisOwnSettings,
}

/// <summary>
/// The OpenAPI document NSwag builds, over TestHost, for the endpoints and controllers the built-in stack's document
/// describes, with value objects described through <c>settings.SchemaSettings.AddValueObjects()</c>.
/// </summary>
/// <param name="Document">NSwag's document.</param>
/// <param name="BuiltIn">The built-in stack's document, for <see cref="NSwagMode.Parity"/>.</param>
/// <param name="Settings">The type of the schema settings NSwag chose.</param>
public sealed record NSwagDocuments(JObject Document, JObject? BuiltIn, Type Settings)
{
    /// <summary>How long a document request may take, failing the test rather than the job's time limit.</summary>
    private static readonly TimeSpan DocumentTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Gets the components, or the definitions of a Swagger 2.0 document.</summary>
    public JObject Schemas => (JObject)(Document["components"]?["schemas"] ?? Document["definitions"])!;

    /// <summary>Gets a component.</summary>
    /// <param name="name">The component's name.</param>
    /// <returns>Its schema, compact.</returns>
    public string Schema(string name) => Schemas[name]!.ToString(Formatting.None);

    /// <summary>Gets a property of a component.</summary>
    /// <param name="component">The component's name.</param>
    /// <param name="name">The property's name.</param>
    /// <returns>Its schema.</returns>
    public JObject Property(string component, string name) => (JObject)Schemas[component]!["properties"]![name]!;

    /// <summary>Gets a parameter of a <c>GET</c> operation.</summary>
    /// <param name="path">Route template of the operation.</param>
    /// <param name="name">Name of the parameter.</param>
    /// <returns>The parameter.</returns>
    public JObject Parameter(string path, string name)
        => (JObject)((JArray)Document["paths"]![path]!["get"]!["parameters"]!).Single(parameter => (string?)parameter["name"] == name);

    /// <summary>Builds the application and reads its documents.</summary>
    /// <param name="mode">How the application serializes, and which document NSwag builds.</param>
    /// <returns>The documents.</returns>
    public static async Task<NSwagDocuments> BuildAsync(NSwagMode mode)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        var minimalApisAlone = mode is NSwagMode.MinimalApisAlone or NSwagMode.MinimalApisOwnSettings;
        if (!minimalApisAlone)
        {
            // Without MVC's System.Text.Json output formatter, NSwag describes Newtonsoft.Json's settings whenever
            // Microsoft.AspNetCore.Mvc.NewtonsoftJson can be loaded, as it can in this suite.
            var mvc = builder.Services.AddControllers()
                .ConfigureApplicationPartManager(static manager =>
                {
                    manager.ApplicationParts.Clear();
                    manager.ApplicationParts.Add(new ControllerPart(typeof(DocumentedAccountsController)));
                })
                .AddValueObjects();
            if (mode == NSwagMode.Newtonsoft)
            {
                mvc.AddNewtonsoftJson().AddValueObjectsNewtonsoftJson();
            }
            else
            {
                mvc.AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.AddValueObjects();
                    if (mode == NSwagMode.Parity)
                    {
                        options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
                    }
                });
            }
        }

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.AddValueObjects();
            if (mode == NSwagMode.Parity)
            {
                options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
            }
        });
        builder.Services.AddEndpointsApiExplorer();
        Type? settings = null;
        if (mode == NSwagMode.Swagger2)
        {
            builder.Services.AddSwaggerDocument(document => settings = document.SchemaSettings.AddValueObjects().GetType());
        }
        else if (mode == NSwagMode.MinimalApisOwnSettings)
        {
            builder.Services.AddOpenApiDocument((document, services) =>
            {
                document.SchemaSettings = new SystemTextJsonSchemaGeneratorSettings
                {
                    SchemaType = NJsonSchemaType.OpenApi3,
                    SerializerOptions = services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions,
                };
                settings = document.SchemaSettings.AddValueObjects().GetType();
            });
        }
        else
        {
            builder.Services.AddOpenApiDocument(document => settings = document.SchemaSettings.AddValueObjects().GetType());
        }

        if (mode == NSwagMode.Parity)
        {
            builder.Services.AddOpenApi(static options => options.AddValueObjects());
        }

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
        application.MapGet("/raw", static (int raw, decimal amount) => Results.Ok());
        OpenApiDocument.MapText(application);
        application.MapGet("/limited", static ([AsParameters] LimitedSearch search) => Results.Ok());
        if (!minimalApisAlone)
        {
            application.MapControllers();
        }

        application.UseOpenApi();
        if (mode == NSwagMode.Parity)
        {
            application.MapOpenApi();
        }

        await application.StartAsync();

        using var client = application.GetTestClient();
        var document = await ReadAsync(client, "/swagger/v1/swagger.json");
        var builtIn = mode == NSwagMode.Parity ? await ReadAsync(client, "/openapi/v1.json") : null;

        await application.StopAsync();

        return new NSwagDocuments(document, builtIn, settings!);
    }

    /// <summary>Reads a document, failing the test when it is not served in time.</summary>
    private static async Task<JObject> ReadAsync(HttpClient client, string path)
    {
        var json = await client.GetStringAsync(path, TestContext.Current.CancellationToken).WaitAsync(DocumentTimeout, TestContext.Current.CancellationToken);
        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };

        return JObject.Load(reader);
    }

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

/// <summary>NSwag's documents, built once for the tests that read them.</summary>
public sealed class NSwagDocument : IAsyncLifetime
{
    /// <summary>Gets the System.Text.Json application's document.</summary>
    public NSwagDocuments SystemTextJson { get; private set; } = null!;

    /// <summary>Gets the Newtonsoft.Json application's document.</summary>
    public NSwagDocuments Newtonsoft { get; private set; } = null!;

    /// <summary>Gets the Swagger 2.0 document.</summary>
    public NSwagDocuments Swagger2 { get; private set; } = null!;

    /// <summary>Gets NSwag's document beside the built-in stack's.</summary>
    public NSwagDocuments Parity { get; private set; } = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        SystemTextJson = await NSwagDocuments.BuildAsync(NSwagMode.SystemTextJson);
        Newtonsoft = await NSwagDocuments.BuildAsync(NSwagMode.Newtonsoft);
        Swagger2 = await NSwagDocuments.BuildAsync(NSwagMode.Swagger2);
        Parity = await NSwagDocuments.BuildAsync(NSwagMode.Parity);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>A query model whose text a validation attribute of its own bounds beside the value object's rules.</summary>
public sealed class LimitedSearch
{
    /// <summary>Gets or sets the text of an account, at most 20 characters long.</summary>
    [ParsesAs(typeof(Iban)), StringLength(20)]
    public string? Account { get; set; }
}
